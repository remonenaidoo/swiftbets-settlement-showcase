using Microsoft.Extensions.Logging;
using SwiftBets.Contracts.Messaging;
using SwiftBets.Contracts.Trading;
using SwiftBets.Settlement.Application.Ports;

namespace SwiftBets.Settlement.Application.Handlers;

/// <summary>A trader's result evaluates every leg in scope at a version above any feed version, so a later feed result cannot undo it.</summary>
public sealed partial class ApplyManualResultHandler(ISettlementStore store, TimeProvider time, ILogger<ApplyManualResultHandler> logger)
{
    public const string Consumer = "settlement.manual-results";

    private static readonly DateTimeOffset VersionEpoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    // Feed versions stay far below a million; later manual results get higher versions.
    public static int ManualVersion(DateTimeOffset issuedAt) => 1_000_000 + (int)Math.Max(0, (issuedAt - VersionEpoch).TotalSeconds);

    public async Task<int> HandleAsync(ManualResultV1 manual)
    {
        if (manual.Action == ManualResultAction.TimeVoid && manual.VoidFrom is null)
        {
            LogTimeVoidWithoutCutOff(logger, manual.ManualResultId);
            return 0;
        }

        var version = ManualVersion(manual.IssuedAt);
        var evaluated = 0;
        await using var transaction = await store.BeginAsync();
        if (!await transaction.TryRecordInboxAsync(Consumer, manual.ManualResultId))
        {
            return 0;
        }

        var stored = ManualResultRules.From(manual, version);
        await transaction.SaveManualResultAsync(stored);
        var touched = await LegsTouchedAsync(transaction, manual, stored);
        var feed = await transaction.LockResultAsync(manual.FixtureId);
        foreach (var couponId in touched.Where(t => t.FinalState is not null).Select(t => t.Leg.CouponId).Distinct())
        {
            await transaction.EnqueueAsync(Topics.ManualResultRejected, couponId.ToString(), new ManualResultRejectedV1(manual.ManualResultId, couponId,
                ManualResultRejectedV1.CouponCashedOut, "The coupon was cashed out; its settlement is final.", time.GetUtcNow()));
        }

        foreach (var leg in touched.Where(t => t.FinalState is null).Select(t => t.Leg))
        {
            if (await transaction.RecordEvaluationAsync(leg, version, ManualResultRules.Verdict(stored, leg, feed), time))
            {
                evaluated++;
            }
        }

        await transaction.CommitAsync();
        return evaluated;
    }

    // Cashed-out coupons are included so the caller rejects them explicitly rather than skipping them silently.
    private async Task<IReadOnlyList<TimedLeg>> LegsTouchedAsync(ISettlementTransaction transaction, ManualResultV1 manual, StoredManualResult stored)
    {
        var inScope = (await transaction.GetTimedLegsForFixtureAsync(manual.FixtureId)).Where(t => ManualResultRules.InScope(stored, t.Leg)).ToList();
        if (manual.Action != ManualResultAction.TimeVoid)
        {
            return inScope;
        }

        var unknown = inScope.Count(t => t.PlacedAt is null);
        if (unknown > 0)
        {
            LogPlacementTimeUnknown(logger, manual.ManualResultId, unknown);
        }

        return [.. inScope.Where(t => ManualResultRules.AppliesTo(stored, t.PlacedAt))];
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Manual result {ManualResultId} is a time-void without a VoidFrom cut-off; skipped.")]
    private static partial void LogTimeVoidWithoutCutOff(ILogger logger, Guid manualResultId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Time-void {ManualResultId} left {Legs} legs alone: their coupons were indexed before placement times were stored.")]
    private static partial void LogPlacementTimeUnknown(ILogger logger, Guid manualResultId, int legs);
}
