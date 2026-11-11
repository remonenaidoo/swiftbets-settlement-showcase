using SwiftBets.Contracts.Placement;
using SwiftBets.Settlement.Application.Ports;
using SwiftBets.Settlement.Domain;

namespace SwiftBets.Settlement.Application.Handlers;

/// <summary>Indexes a placed coupon's legs by fixture; a leg whose result already landed is evaluated in the same transaction.</summary>
public sealed class IndexCouponHandler(ISettlementStore store, TimeProvider time)
{
    public const string ConsumerV2 = "settlement.indexer.v2";

    public async Task HandleAsync(Guid eventId, CouponPlacedV2 placed)
    {
        var legs = placed.Legs.Select((l, i) => new IndexedLeg(l.LegId, placed.CouponId, l.FixtureId, l.MarketId, l.SelectionId, l.Odds, l.IsBanker, i, IndexedLeg.Serialize(l.Builder))).ToList();
        var coupon = new IndexedCoupon(placed.CouponId, placed.PunterId, placed.TotalStake.MinorUnits, placed.TotalStake.Currency, legs.Count, placed.PlacedAt);
        List<SettlementBet> bets = [.. placed.Bets.Select(b => new SettlementBet(b.BetId, b.Folds, b.UnitStake.MinorUnits, b.AccaBoostPercent))];
        List<string> fixtureIds = [.. legs.Select(l => l.FixtureId).Distinct(StringComparer.Ordinal)];

        await using var transaction = await store.BeginAsync();
        if (!await transaction.TryRecordInboxAsync(ConsumerV2, eventId))
        {
            return;
        }

        var results = await transaction.LockResultsAsync(fixtureIds);
        if (!await transaction.TryInsertCouponAsync(coupon, legs, bets))
        {
            if (bets is [{ BetId: var id } bet] && id != Guid.Empty)
            {
                await transaction.AdoptBetIdAsync(coupon.CouponId, bet);
            }

            await transaction.CommitAsync();
            return;
        }

        foreach (var leg in legs)
        {
            if (results.TryGetValue(leg.FixtureId, out var result) && result.IsSettleable)
            {
                await transaction.RecordEvaluationAsync(leg, result.Version, BuilderRules.Evaluate(leg.MarketId, leg.SelectionId, leg.BuilderLeg, result), time);
            }
        }

        // A trader's result that landed before this placement event still applies, above any feed version.
        foreach (var manual in (await transaction.GetManualResultsAsync(fixtureIds)).Where(m => ManualResultRules.AppliesTo(m, coupon.PlacedAt)))
        {
            foreach (var leg in legs.Where(l => ManualResultRules.InScope(manual, l)))
            {
                await transaction.RecordEvaluationAsync(leg, manual.Version, ManualResultRules.Verdict(manual, leg, results.GetValueOrDefault(leg.FixtureId)), time);
            }
        }

        await transaction.CommitAsync();
    }
}
