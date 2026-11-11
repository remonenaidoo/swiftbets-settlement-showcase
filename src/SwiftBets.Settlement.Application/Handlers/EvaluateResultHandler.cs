using SwiftBets.BuildingBlocks.Core;
using SwiftBets.Contracts.Offer;
using SwiftBets.Settlement.Application.Ports;
using SwiftBets.Settlement.Domain;

namespace SwiftBets.Settlement.Application.Handlers;

/// <summary>Stage one: a result that outranks the stored one evaluates every leg on its fixture; anything else is a no-op.</summary>
public sealed class EvaluateResultHandler(ISettlementStore store, IFaultPoint faults, TimeProvider time)
{
    public const string Consumer = "settlement.evaluator";
    public const string FaultAfterCommit = "settlement.evaluator.after-commit";

    public async Task<int> HandleAsync(Guid eventId, ResultPublishedV1 published)
    {
        var result = new FixtureResult(published.FixtureId, published.ResultVersion, SettlementMapping.State(published.Status), published.HomeGoals, published.AwayGoals,
            published.Detail?.HomeGames, published.Detail?.AwayGames, published.Detail?.Winners);
        var evaluated = 0;
        await using (var transaction = await store.BeginAsync())
        {
            if (!await transaction.TryRecordInboxAsync(Consumer, eventId))
            {
                return 0;
            }

            if (!result.Outranks(await transaction.LockResultAsync(result.FixtureId)))
            {
                await transaction.CommitAsync();
                return 0;
            }

            await transaction.SaveResultAsync(result);
            if (result.IsSettleable)
            {
                foreach (var leg in await transaction.GetLegsForFixtureAsync(result.FixtureId))
                {
                    var verdict = BuilderRules.Evaluate(leg.MarketId, leg.SelectionId, leg.BuilderLeg, result);
                    if (await transaction.RecordEvaluationAsync(leg, result.Version, verdict, time))
                    {
                        evaluated++;
                    }
                }
            }

            await transaction.CommitAsync();
        }

        await faults.HitAsync(FaultAfterCommit);
        return evaluated;
    }
}
