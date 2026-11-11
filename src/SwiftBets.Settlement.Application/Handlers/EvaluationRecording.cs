using SwiftBets.Contracts.Messaging;
using SwiftBets.Contracts.Settlement;
using SwiftBets.Settlement.Application.Ports;
using SwiftBets.Settlement.Domain;

namespace SwiftBets.Settlement.Application.Handlers;

internal static class EvaluationRecording
{
    /// <summary>Stores the leg's outcome at this version and publishes it; false when the verdict is still open or already stored.</summary>
    public static async Task<bool> RecordEvaluationAsync(this ISettlementTransaction transaction, IndexedLeg leg, int resultVersion, LegVerdict verdict, TimeProvider time)
    {
        if (verdict.Outcome is not { } outcome || !await transaction.TryInsertEvaluationAsync(leg, resultVersion, outcome, verdict.Odds))
        {
            return false;
        }

        await transaction.EnqueueAsync(Topics.LegEvaluated, leg.CouponId.ToString(),
            new LegEvaluatedV1(leg.CouponId, leg.LegId, leg.FixtureId, resultVersion, SettlementMapping.Map(outcome), time.GetUtcNow()));
        return true;
    }
}
