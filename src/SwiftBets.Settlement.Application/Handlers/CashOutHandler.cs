using SwiftBets.Contracts.Messaging;
using SwiftBets.Contracts.Money;
using SwiftBets.Contracts.Settlement;
using SwiftBets.Settlement.Application.Ports;
using SwiftBets.Settlement.Domain;
using ContractOutcome = SwiftBets.Contracts.Settlement.CouponOutcome;

namespace SwiftBets.Settlement.Application.Handlers;

/// <summary>Settles a coupon at the agreed amount as its next settlement version; under the coupon lock, a cashout and a late result cannot both win.</summary>
public sealed class CashOutHandler(ISettlementStore store, IProgressCounter counter, TimeProvider time)
{
    public sealed record Request(Guid CashoutId, Guid CouponId, Guid PunterId, long Amount, string Currency);

    public sealed record Reply(bool Accepted, string? RefusalCode, int SettlementVersion, bool WasApplied)
    {
        public static Reply Applied(int settlementVersion) => new(true, null, settlementVersion, true);

        public static Reply AlreadyApplied(int settlementVersion) => new(true, null, settlementVersion, false);

        public static Reply Refused(string code) => new(false, code, 0, false);
    }

    public async Task<Reply> HandleAsync(Request request)
    {
        Reply reply;
        await using (var transaction = await store.BeginAsync())
        {
            reply = await CashOutAsync(transaction, request);
        }

        if (reply.Accepted)
        {
            await counter.MarkFinalAsync(request.CouponId);
        }

        return reply;
    }

    private async Task<Reply> CashOutAsync(ISettlementTransaction transaction, Request request)
    {
        var coupon = await transaction.LockCouponAsync(request.CouponId);
        if (coupon is null)
        {
            return Reply.Refused(CashoutRefusal.CouponNotFound);
        }

        if (await transaction.GetCashoutAsync(request.CashoutId) is { } earlier)
        {
            return earlier.CouponId == request.CouponId ? Reply.AlreadyApplied(earlier.SettlementVersion) : Reply.Refused(CashoutRefusal.CashoutIdReused);
        }

        if (coupon.PunterId != request.PunterId)
        {
            return Reply.Refused(CashoutRefusal.PunterMismatch);
        }

        if (await transaction.GetFinalStateAsync(request.CouponId) is not null)
        {
            return Reply.Refused(CashoutRefusal.CouponCashedOut);
        }

        if (!string.Equals(coupon.Currency, request.Currency, StringComparison.Ordinal) || request.Amount < 0)
        {
            return Reply.Refused(CashoutRefusal.AmountInvalid);
        }

        var legs = await transaction.GetCouponLegsAsync(request.CouponId);
        if (legs.All(l => l.Outcome is not null))
        {
            return Reply.Refused(CashoutRefusal.CouponSettled);
        }

        var bets = await transaction.GetBetsAsync(request.CouponId);
        if (!CashoutRules.IsSingleLine(bets, legs.Count(l => !l.IsBanker)))
        {
            return Reply.Refused(CashoutRefusal.NotSingleLine);
        }

        var previous = await transaction.GetLatestSettlementAsync(request.CouponId);
        var version = (previous?.Version ?? 0) + 1;
        var now = time.GetUtcNow();
        var settlement = CouponSettlement.CashedOut(bets[0].BetId, coupon.Stake, request.Amount);
        await transaction.MarkFinalAsync(request.CouponId, FinalState.CashedOut);
        await transaction.InsertCashoutAsync(new CashoutRecord(request.CashoutId, request.CouponId, request.Amount, request.Currency, version, now));
        await transaction.InsertSettlementAsync(request.CouponId, version, settlement);
        var stake = new Money(coupon.Stake, coupon.Currency);
        var payout = new Money(request.Amount, coupon.Currency);
        await transaction.EnqueueAsync(Topics.CouponSettledV2, request.CouponId.ToString(), new CouponSettledV2(request.CouponId, coupon.PunterId, version, ContractOutcome.CashedOut,
            stake, payout, [.. settlement.Bets.Select(b => new BetSettlementV2(b.BetId, ContractOutcome.CashedOut, 0, 0, 0, payout))], now));
        await transaction.CommitAsync();
        return Reply.Applied(version);
    }
}
