using System.Text.Json;
using SwiftBets.BuildingBlocks.Web;
using SwiftBets.Contracts.Errors;
using SwiftBets.Contracts.Serialization;
using SwiftBets.Settlement.Application.Handlers;
using SwiftBets.Settlement.Application.Ports;

namespace SwiftBets.Settlement.Worker;

internal static class SettlementEndpoints
{
    private const int MaxIntegrityCoupons = 500;

    public static void MapSettlementEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/coupons/{couponId:guid}/state", GetCouponStateAsync).RequireAuthorization(Roles.OperatorOrService);
        app.MapPost("/coupons/{couponId:guid}/refresh", RefreshAsync).RequireAuthorization(Roles.OperatorOrService);
        app.MapPost("/internal/integrity/settlements", GetSettlementDigestAsync).RequireAuthorization(Roles.Service);
    }

    private static async Task<IResult> GetCouponStateAsync(Guid couponId, CouponStateQuery query, HttpContext context, CancellationToken cancellationToken) =>
        await query.GetAsync(couponId, cancellationToken) is { } state
            ? Results.Json(state, ContractJson.Options)
            : Error.NotFound("coupon_not_found", "Settlement has not indexed that coupon.").ToHttpResult(context);

    private static async Task<IResult> RefreshAsync(Guid couponId, ReconcileHandler reconciler) =>
        Results.Ok(new { couponId, resettled = await reconciler.RepairAsync(couponId, "operator_refresh") });

    private static async Task<IResult> GetSettlementDigestAsync(IntegrityRequest request, ISettlementStore store, HttpContext context, CancellationToken cancellationToken)
    {
        if (request.CouponIds is not { Count: > 0 and <= MaxIntegrityCoupons })
        {
            return Error.Validation("invalid_coupon_ids", $"Send 1 to {MaxIntegrityCoupons} coupon ids.").ToHttpResult(context);
        }

        var digest = await store.GetSettlementDigestAsync(request.CouponIds, cancellationToken);
        return Results.Ok(digest.Select(d => new { d.CouponId, d.Version, Outcome = JsonNamingPolicy.CamelCase.ConvertName(d.Outcome.ToString()), d.Payout, d.SettledAt }));
    }
}

internal sealed record IntegrityRequest(IReadOnlyList<Guid>? CouponIds);
