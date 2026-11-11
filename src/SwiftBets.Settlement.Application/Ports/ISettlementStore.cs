using SwiftBets.Settlement.Application.Integrity;

namespace SwiftBets.Settlement.Application.Ports;

public interface ISettlementStore
{
    Task<ISettlementTransaction> BeginAsync();

    Task<StoredCouponState?> GetCouponStateAsync(Guid couponId, CancellationToken cancellationToken);

    /// <summary>The latest settlement of each coupon that has one; coupons never settled are left out.</summary>
    Task<IReadOnlyList<SettlementDigest>> GetSettlementDigestAsync(IReadOnlyList<Guid> couponIds, CancellationToken cancellationToken);

    /// <summary>Coupons fully evaluated but not settled since their latest evaluation, quiet for at least <paramref name="quietFor"/>.</summary>
    Task<IReadOnlyList<Guid>> FindUnsettledCouponsAsync(TimeSpan quietFor, int limit, CancellationToken cancellationToken);

    /// <summary>Legs whose fixture has a settleable result they were never evaluated against.</summary>
    Task<IReadOnlyList<UnevaluatedLeg>> FindUnevaluatedLegsAsync(int limit, CancellationToken cancellationToken);
}
