namespace SwiftBets.Settlement.Application.Ports;

/// <summary>Per-coupon progress in Redis, guarded by a token per leg evaluation so a redelivery cannot count twice.</summary>
public interface IProgressCounter
{
    Task<LegProgress> RecordAsync(Guid couponId, Guid legId, int resultVersion);

    Task<int> ResolvedLegsAsync(Guid couponId);

    /// <summary>Stops any further evaluation counting towards a cashed-out coupon.</summary>
    Task MarkFinalAsync(Guid couponId);

    Task RebuildAsync(Guid couponId, IReadOnlyList<LegEvaluation> evaluations);
}

public sealed record LegProgress(bool Applied, int ResolvedLegs, bool CouponIsFinal = false)
{
    public static readonly LegProgress Final = new(false, 0, CouponIsFinal: true);
}
