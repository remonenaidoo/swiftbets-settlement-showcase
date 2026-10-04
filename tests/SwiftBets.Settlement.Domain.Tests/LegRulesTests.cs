namespace SwiftBets.Settlement.Domain.Tests;

public sealed class LegRulesTests
{
    [Fact]
    public void Home_selection_wins_when_the_home_side_scores_more() =>
        LegRules.Evaluate("f-1x2", "home", new FixtureResult("f", 1, ResultState.Official, 2, 1)).ShouldBe(LegOutcome.Won);

    [Fact]
    public void Any_selection_on_a_void_result_is_void() =>
        LegRules.Evaluate("f-ou25", "over", new FixtureResult("f", 1, ResultState.Void, 4, 0)).ShouldBe(LegOutcome.Void);

    [Fact]
    public void A_handicap_on_a_whole_line_pushes_to_void_and_a_half_line_total_is_decided()
    {
        var result = new FixtureResult("f", 1, ResultState.Official, 110, 103);

        LegRules.Evaluate("f-hcp", "home:-7.0", result).ShouldBe(LegOutcome.Void);
        LegRules.Evaluate("f-hcp", "away:+7.5", result).ShouldBe(LegOutcome.Won);
        LegRules.Evaluate("f-pts", "under:212.5", result).ShouldBe(LegOutcome.Lost);
        LegRules.Evaluate("f-games", "over:22.5", result with { HomeGames = 13, AwayGames = 10 }).ShouldBe(LegOutcome.Won);
        LegRules.Evaluate("f-winner", "team-bulls", result with { Winners = ["team-bulls"] }).ShouldBe(LegOutcome.Won);
    }

    [Fact]
    public void A_two_way_market_that_ends_level_and_an_outright_with_no_winner_are_void()
    {
        var level = new FixtureResult("f", 1, ResultState.Official, 160, 160);

        LegRules.Evaluate("f-mw", "home", level).ShouldBe(LegOutcome.Void);
        LegRules.Evaluate("f-topbat", "player-jos-buttler", level).ShouldBe(LegOutcome.Void);
    }

    private static FixtureResult Score(int home, int away) => new("f", 1, ResultState.Official, home, away);

    [Theory]
    [InlineData("f-btts", "yes", 1, 1, LegOutcome.Won)]
    [InlineData("f-btts", "yes", 2, 0, LegOutcome.Lost)]
    [InlineData("f-dc", "homeDraw", 1, 1, LegOutcome.Won)]
    [InlineData("f-dc", "drawAway", 2, 1, LegOutcome.Lost)]
    [InlineData("f-dnb", "away", 0, 1, LegOutcome.Won)]
    [InlineData("f-dnb", "home", 0, 1, LegOutcome.Lost)]
    [InlineData("f-ou15", "over:1.5", 1, 1, LegOutcome.Won)]
    [InlineData("f-ou15", "over:1.5", 1, 0, LegOutcome.Lost)]
    [InlineData("f-ou35", "under:3.5", 2, 1, LegOutcome.Won)]
    [InlineData("f-ou35", "under:3.5", 3, 1, LegOutcome.Lost)]
    [InlineData("f-cs", "score:2-1", 2, 1, LegOutcome.Won)]
    [InlineData("f-cs", "score:2-1", 1, 2, LegOutcome.Lost)]
    [InlineData("f-cs", "other", 4, 0, LegOutcome.Won)]
    [InlineData("f-cs", "other", 3, 3, LegOutcome.Lost)]
    public void Football_markets_settle_from_the_final_score(string marketId, string selectionId, int home, int away, LegOutcome expected) =>
        LegRules.Evaluate(marketId, selectionId, Score(home, away)).ShouldBe(expected);

    [Fact]
    public void Draw_no_bet_on_a_draw_is_void_and_an_unknown_selection_is_refused()
    {
        LegRules.Evaluate("f-dnb", "home", Score(1, 1)).ShouldBe(LegOutcome.Void);
        Should.Throw<ArgumentOutOfRangeException>(() => LegRules.Evaluate("f-btts", "maybe", Score(1, 1)));
        Should.Throw<ArgumentOutOfRangeException>(() => LegRules.Evaluate("f-cs", "2-1", Score(2, 1)));
    }
}
