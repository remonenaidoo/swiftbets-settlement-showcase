using SwiftBets.Contracts.Messaging;
using SwiftBets.Contracts.Settlement;
using SwiftBets.Settlement.Application.Ports;
using SwiftBets.Settlement.Domain;

namespace SwiftBets.Settlement.Application.Handlers;

/// <summary>Repairs Redis/SQL divergence from SQL: legs a result never evaluated, and coupons evaluated but never (re)settled.</summary>
public sealed class ReconcileHandler(ISettlementStore store, IProgressCounter counter, CouponSettler settler, TimeProvider time)
{
    private const int BatchSize = 200;

    public async Task<int> ReconcileAsync(TimeSpan quietFor, CancellationToken cancellationToken)
    {
        var repaired = 0;
        foreach (var (leg, result) in await store.FindUnevaluatedLegsAsync(BatchSize, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var transaction = await store.BeginAsync();
            await transaction.RecordEvaluationAsync(leg, result.Version, BuilderRules.Evaluate(leg.MarketId, leg.SelectionId, leg.BuilderLeg, result), time);
            await transaction.CommitAsync();
        }

        foreach (var couponId in await store.FindUnsettledCouponsAsync(quietFor, BatchSize, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            repaired += await RepairAsync(couponId, "evaluated_but_unsettled") ? 1 : 0;
        }

        return repaired;
    }

    /// <summary>Rebuilds the coupon's Redis progress from SQL and settles it; also backs the operator refresh command.</summary>
    public async Task<bool> RepairAsync(Guid couponId, string reason)
    {
        IReadOnlyList<LegEvaluation> evaluations;
        int legCount;
        await using (var read = await store.BeginAsync())
        {
            var coupon = await read.LockCouponAsync(couponId);
            if (coupon is null)
            {
                return false;
            }

            legCount = coupon.LegCount;
            evaluations = await read.GetLatestEvaluationsAsync(couponId);
        }

        var redisBefore = await counter.ResolvedLegsAsync(couponId);
        await counter.RebuildAsync(couponId, evaluations);
        var settled = await settler.SettleAsync(couponId);

        await using var report = await store.BeginAsync();
        await report.EnqueueAsync(Topics.StuckCoupon, couponId.ToString(),
            new StuckCouponV1(couponId, $"{reason}; redis had {redisBefore} of {legCount} legs", evaluations.Count, legCount, settled is not null, time.GetUtcNow()));
        await report.CommitAsync();
        return settled is not null;
    }
}
