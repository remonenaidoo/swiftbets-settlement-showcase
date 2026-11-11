using SwiftBets.Settlement.Application.Ports;

namespace SwiftBets.Settlement.Application.Handlers;

public sealed class CouponStateQuery(ISettlementStore store, IProgressCounter counter)
{
    public async Task<CouponStateView?> GetAsync(Guid couponId, CancellationToken cancellationToken)
    {
        if (await store.GetCouponStateAsync(couponId, cancellationToken) is not { } state)
        {
            return null;
        }

        return new CouponStateView(state.CouponId, state.LegCount, state.SettlementPending, state.LastEvaluatedAt, state.Legs, state.Settlements,
            await counter.ResolvedLegsAsync(couponId));
    }
}
