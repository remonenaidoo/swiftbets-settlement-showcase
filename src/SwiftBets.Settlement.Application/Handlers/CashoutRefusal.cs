namespace SwiftBets.Settlement.Application.Handlers;

/// <summary>Refusal codes the cashout service receives over gRPC.</summary>
public static class CashoutRefusal
{
    public const string CouponNotFound = "coupon_not_found";
    public const string CashoutIdReused = "cashout_id_reused";
    public const string PunterMismatch = "punter_mismatch";
    public const string CouponCashedOut = "coupon_cashed_out";
    public const string AmountInvalid = "amount_invalid";
    public const string CouponSettled = "coupon_settled";
    public const string NotSingleLine = "not_single_line";
}
