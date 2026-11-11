using SwiftBets.BuildingBlocks.Core;
using SwiftBets.Contracts.Settlement;
using SwiftBets.Settlement.Application.Ports;

namespace SwiftBets.Settlement.Application.Handlers;

/// <summary>The Redis counter gates the SQL settle; a redelivery on a complete coupon still runs the settler, covering a crash before its commit.</summary>
public sealed class SettleCouponHandler(ISettlementStore store, IProgressCounter counter, CouponSettler settler, IFaultPoint faults)
{
    /// <summary>Simulates a lost evaluation, leaving the coupon for the reconciler.</summary>
    public const string FaultDrop = "settlement.settler.drop";

    public async Task<CouponSettledV2?> HandleAsync(LegEvaluatedV1 evaluated)
    {
        try
        {
            await faults.HitAsync(FaultDrop);
        }
        catch (FaultInjectedException)
        {
            return null;
        }

        var progress = await counter.RecordAsync(evaluated.CouponId, evaluated.LegId, evaluated.ResultVersion);
        if (progress.CouponIsFinal)
        {
            return null;
        }

        await using (var transaction = await store.BeginAsync())
        {
            var coupon = await transaction.LockCouponAsync(evaluated.CouponId);
            if (coupon is null || progress.ResolvedLegs < coupon.LegCount)
            {
                return null;
            }
        }

        return await settler.SettleAsync(evaluated.CouponId);
    }
}
