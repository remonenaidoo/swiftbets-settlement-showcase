using SwiftBets.Contracts.Offer;

namespace SwiftBets.Settlement.Domain;

/// <summary>A leg's judgement: its outcome (null while a part is still unknown) and, for a repriced bet builder, its new odds.</summary>
public sealed record LegVerdict(LegOutcome? Outcome, decimal? Odds = null);

/// <summary>A bet builder settles as one leg; void components are dropped and the rest repriced, and fewer than two left voids it.</summary>
public static class BuilderRules
{
    public static LegVerdict Evaluate(BetBuilderLegV1 builder, Func<BuilderComponentV1, LegOutcome?> outcomeOf)
    {
        var outcomes = builder.Components.Select(c => (Component: c, Outcome: outcomeOf(c))).ToList();
        if (outcomes.Any(o => o.Outcome == LegOutcome.Lost))
        {
            return new LegVerdict(LegOutcome.Lost);
        }

        if (outcomes.Any(o => o.Outcome is null))
        {
            return new LegVerdict(null);
        }

        var voided = outcomes.Where(o => o.Outcome == LegOutcome.Void).Select(o => o.Component.SelectionId).ToHashSet(StringComparer.Ordinal);
        if (voided.Count == 0)
        {
            return new LegVerdict(LegOutcome.Won);
        }

        return BetBuilderPricing.Reprice(builder, voided) is { } odds ? new LegVerdict(LegOutcome.Won, odds) : new LegVerdict(LegOutcome.Void);
    }

    public static LegVerdict Evaluate(string marketId, string selectionId, BetBuilderLegV1? builder, FixtureResult result) =>
        builder is null ? new LegVerdict(LegRules.Evaluate(marketId, selectionId, result)) : Evaluate(builder, c => LegRules.Evaluate(c.MarketId, c.SelectionId, result));
}
