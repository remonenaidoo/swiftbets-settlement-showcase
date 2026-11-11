using SwiftBets.Settlement.Domain;

namespace SwiftBets.Settlement.Application.Ports;

public sealed record StoredSettlement(int Version, CouponOutcome Outcome, long Payout)
{
    public bool Matches(CouponSettlement settlement) => settlement.Outcome == Outcome && settlement.Payout == Payout;
}
