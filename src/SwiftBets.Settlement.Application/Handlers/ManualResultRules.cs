using SwiftBets.Contracts.Offer;
using SwiftBets.Contracts.Trading;
using SwiftBets.Settlement.Application.Ports;
using SwiftBets.Settlement.Domain;

namespace SwiftBets.Settlement.Application.Handlers;

/// <summary>Which legs a manual result touches and what it makes of them; shared by applying it now and by indexing later.</summary>
public static class ManualResultRules
{
    public static bool InScope(StoredManualResult manual, IndexedLeg leg) => manual.Scope switch
    {
        ManualResultScope.Fixture => leg.FixtureId == manual.FixtureId,
        ManualResultScope.Market => leg.FixtureId == manual.FixtureId && (leg.MarketId == manual.MarketId || (leg.BuilderLeg?.Components.Any(c => c.MarketId == manual.MarketId) ?? false)),
        ManualResultScope.Coupon => leg.CouponId == manual.CouponId,
        _ => false,
    };

    // A time-void touches only coupons placed at or after the cut-off; an unknown placement time is never guessed.
    public static bool AppliesTo(StoredManualResult manual, DateTimeOffset? placedAt) =>
        manual.Action != ManualResultAction.TimeVoid || (manual.VoidFrom is { } from && placedAt >= from);

    public static LegOutcome Outcome(StoredManualResult manual, IndexedLeg leg) =>
        manual.Action is ManualResultAction.Void or ManualResultAction.TimeVoid ? LegOutcome.Void
        : leg.SelectionId == manual.WinningSelectionId ? LegOutcome.Won : LegOutcome.Lost;

    /// <summary>A coupon-scoped result judges a bet builder as a whole; otherwise each component it covers takes its outcome and the rest wait for the feed.</summary>
    public static LegVerdict Verdict(StoredManualResult manual, IndexedLeg leg, FixtureResult? feed)
    {
        if (leg.BuilderLeg is not { } builder || manual.Scope == ManualResultScope.Coupon)
        {
            return new LegVerdict(Outcome(manual, leg));
        }

        var isVoid = manual.Action is ManualResultAction.Void or ManualResultAction.TimeVoid;
        return BuilderRules.Evaluate(builder, ComponentOutcome);

        LegOutcome? ComponentOutcome(BuilderComponentV1 component)
        {
            if (component.MarketId == manual.MarketId || (manual.MarketId is null && isVoid))
            {
                return Outcome(manual, leg with { MarketId = component.MarketId, SelectionId = component.SelectionId });
            }

            if (manual.MarketId is null && component.SelectionId == manual.WinningSelectionId)
            {
                return LegOutcome.Won;
            }

            return feed is { IsSettleable: true } ? LegRules.Evaluate(component.MarketId, component.SelectionId, feed) : null;
        }
    }

    public static StoredManualResult From(ManualResultV1 manual, int version) =>
        new(manual.ManualResultId, manual.FixtureId, manual.Scope, manual.Action, manual.MarketId, manual.CouponId, manual.WinningSelectionId, manual.VoidFrom, version, manual.IssuedAt);
}
