using Dapper;
using Microsoft.Data.SqlClient;
using SwiftBets.BuildingBlocks.Outbox;
using SwiftBets.BuildingBlocks.Persistence;
using SwiftBets.Settlement.Application.Integrity;
using SwiftBets.Settlement.Application.Ports;
using SwiftBets.Settlement.Domain;

namespace SwiftBets.Settlement.Infrastructure.Persistence;

public sealed class SqlSettlementStore(ISqlConnectionFactory connections, IOutbox outbox, IInboxStore inbox, TimeProvider time) : ISettlementStore
{
    private static readonly SqlResources Sql = SqlResources.For<SqlSettlementStore>();

    public async Task<ISettlementTransaction> BeginAsync()
    {
        var connection = await connections.OpenAsync(CancellationToken.None);
        return new SqlSettlementTransaction(connection, (SqlTransaction)await connection.BeginTransactionAsync(), outbox, inbox, time);
    }

    public async Task<IReadOnlyList<Guid>> FindUnsettledCouponsAsync(TimeSpan quietFor, int limit, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        return [.. await connection.QueryAsync<Guid>(new CommandDefinition(Sql.Get("Settle.FindUnsettled"), new { Limit = limit, Cutoff = time.GetUtcNow() - quietFor }, cancellationToken: cancellationToken))];
    }

    public async Task<IReadOnlyList<UnevaluatedLeg>> FindUnevaluatedLegsAsync(int limit, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<UnevaluatedLegRow>(
            new CommandDefinition(Sql.Get("Settle.FindUnevaluated"), new { Limit = limit, Since = time.GetUtcNow().AddHours(-1) }, cancellationToken: cancellationToken));
        return [.. rows.Select(r => r.ToPort())];
    }

    public async Task<IReadOnlyList<SettlementDigest>> GetSettlementDigestAsync(IReadOnlyList<Guid> couponIds, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<(Guid CouponId, int Version, byte Outcome, long Payout, DateTimeOffset SettledAt)>(
            new CommandDefinition(Sql.Get("Settle.IntegrityDigest"), new { Ids = couponIds }, cancellationToken: cancellationToken));
        return [.. rows.Select(r => new SettlementDigest(r.CouponId, r.Version, (CouponOutcome)r.Outcome, r.Payout, r.SettledAt))];
    }

    public async Task<StoredCouponState?> GetCouponStateAsync(Guid couponId, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        using var reader = await connection.QueryMultipleAsync(new CommandDefinition(Sql.Get("Settle.CouponState"), new { CouponId = couponId }, cancellationToken: cancellationToken));
        var coupon = await reader.ReadSingleOrDefaultAsync<(Guid CouponId, int LegCount, bool SettlementPending, DateTimeOffset? LastEvaluatedAt)?>();
        if (coupon is not { } c)
        {
            return null;
        }

        var legs = (await reader.ReadAsync<(Guid LegId, string FixtureId, string SelectionId, decimal Odds, int? LatestResultVersion, byte? Outcome)>())
            .Select(l => new LegStateView(l.LegId, l.FixtureId, l.SelectionId, l.Odds, l.LatestResultVersion, l.Outcome is { } o ? ((LegOutcome)o).ToString() : null))
            .ToList();
        var settlements = (await reader.ReadAsync<(int Version, byte Outcome, long Payout, DateTimeOffset SettledAt)>())
            .Select(s => new SettlementStateView(s.Version, ((CouponOutcome)s.Outcome).ToString(), s.Payout, s.SettledAt))
            .ToList();
        return new StoredCouponState(c.CouponId, c.LegCount, c.SettlementPending, c.LastEvaluatedAt, legs, settlements);
    }

    internal sealed record UnevaluatedLegRow(
        Guid LegId, Guid CouponId, string FixtureId, string MarketId, string SelectionId, decimal Odds,
        string ResultFixtureId, int ResultVersion, byte State, int HomeGoals, int AwayGoals, int? HomeGames, int? AwayGames, string? Winners, string? Builder)
    {
        public UnevaluatedLeg ToPort() => new(
            new IndexedLeg(LegId, CouponId, FixtureId, MarketId, SelectionId, Odds, Builder: Builder),
            new SqlSettlementTransaction.ResultRow(ResultFixtureId, ResultVersion, State, HomeGoals, AwayGoals, HomeGames, AwayGames, Winners).ToDomain());
    }
}
