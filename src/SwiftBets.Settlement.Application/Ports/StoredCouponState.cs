namespace SwiftBets.Settlement.Application.Ports;

public sealed record StoredCouponState(
    Guid CouponId,
    int LegCount,
    bool SettlementPending,
    DateTimeOffset? LastEvaluatedAt,
    IReadOnlyList<LegStateView> Legs,
    IReadOnlyList<SettlementStateView> Settlements);
