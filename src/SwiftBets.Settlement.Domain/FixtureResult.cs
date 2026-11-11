namespace SwiftBets.Settlement.Domain;

/// <summary>A published result; the score is goals, points, runs or (tennis) sets, with games and winners for the rest.</summary>
public sealed record FixtureResult(string FixtureId, int Version, ResultState State, int HomeGoals, int AwayGoals, int? HomeGames = null, int? AwayGames = null, IReadOnlyList<string>? Winners = null)
{
    // A later version always wins; within a version the stronger status does (void over correction over official over provisional).
    public bool Outranks(FixtureResult? current) =>
        current is null || Version > current.Version || (Version == current.Version && State > current.State);

    public bool IsSettleable => State != ResultState.Provisional;
}
