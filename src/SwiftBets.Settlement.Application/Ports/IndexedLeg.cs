using System.Text.Json;
using SwiftBets.Contracts.Offer;
using SwiftBets.Contracts.Serialization;

namespace SwiftBets.Settlement.Application.Ports;

/// <summary>Position is the leg's place on the coupon; Builder is a bet builder's legs as JSON, null for any other leg.</summary>
public sealed record IndexedLeg(Guid LegId, Guid CouponId, string FixtureId, string MarketId, string SelectionId, decimal Odds, bool IsBanker = false, int Position = 0, string? Builder = null)
{
    public BetBuilderLegV1? BuilderLeg { get; } = Builder is null ? null : JsonSerializer.Deserialize<BetBuilderLegV1>(Builder, ContractJson.Options);

    public static string? Serialize(BetBuilderLegV1? builder) => builder is null ? null : JsonSerializer.Serialize(builder, ContractJson.Options);
}
