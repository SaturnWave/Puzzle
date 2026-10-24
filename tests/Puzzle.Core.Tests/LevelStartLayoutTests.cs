using Puzzle.Client.Layout;

namespace Puzzle.Core.Tests;

/// <summary>
/// Where the card before a level hangs on a page of the museum and where its parts stand on it, before anything is
/// drawn: the card of the design (LevelStartTR) with its nameplate and the green button that reaches under it, on the
/// screens the game is made for and on screens with a cutout and a system bar. <c>LevelStartLayout</c> and
/// <c>PageLayout</c> are the Unity project's source files, compiled into these tests: they have no Unity dependency.
/// </summary>
public class LevelStartLayoutTests
{
    /// <summary>
    /// How wide a two-figure number is at the goals' 34 points, and a three-figure one, in Paytone One: a figure is
    /// about 0.56 of the type size (the lettering sheet measures 1.250 at 40.8 points in a type of 16.5).
    /// </summary>
    private const float TwoFigures = 2 * 0.56f * LevelStartLayout.GoalNumberSize;
    private const float ThreeFigures = 3 * 0.56f * LevelStartLayout.GoalNumberSize;

    /// <summary>The tall phone the canvas is drawn for, the tight 9:16 one, the open fold, the tall phone with its cutout and system bar, and the three iPads upright.</summary>
    public static TheoryData<int, int, int, int> Screens() => new TheoryData<int, int, int, int>
    {
        { 1080, 2340, 0, 0 },
        { 1080, 1920, 0, 0 },
        { 2076, 2152, 0, 0 },
        { 1080, 2340, 110, 60 },
        { 1080, 1920, 90, 48 },
        { 720, 1280, 0, 0 },
        { 2048, 2732, 48, 40 },
        { 1640, 2360, 48, 40 },
        { 1488, 2266, 48, 40 },
    };

    [Theory]
    [MemberData(nameof(Screens))]
    public void The_card_with_its_nameplate_and_its_green_button_hangs_inside_the_usable_screen(int width, int height, int topInset, int bottomInset)
    {
        PageLayout page = PageLayout.Of(new ScreenShape(width, height, topInset, bottomInset));

        foreach (bool hasRewardLine in new[] { true, false })
        {
            CardPlace place = LevelStartLayout.PlaceOn(page, hasRewardLine);
            float bodyMiddle = place.Middle - place.Scale * (LevelStartLayout.RidesAbove(hasRewardLine) - LevelStartLayout.RidesBelow) / 2f;
            float top = bodyMiddle + place.Scale * (LevelStartLayout.BodyHeight / 2f + LevelStartLayout.RidesAbove(hasRewardLine));
            float lipOfTheButton = bodyMiddle - place.Scale * (LevelStartLayout.BodyHeight / 2f + LevelStartLayout.RidesBelow);

            Assert.True(top <= page.View.UsableTop - PageLayout.CardMarginAboveAndBelow + 0.01f, $"the nameplate runs under the cutout ({top} over {page.View.UsableTop})");
            Assert.True(lipOfTheButton >= page.View.UsableBottom + PageLayout.CardMarginAboveAndBelow - 0.01f, $"Oyna runs under the system bar ({lipOfTheButton} under {page.View.UsableBottom})");
            Assert.True(place.Scale * LevelStartLayout.BodyWidth <= page.View.Width - 2 * PageLayout.CardSideMargin + 0.01f, "the card is cut off at the sides");
        }
    }

    [Theory]
    [MemberData(nameof(Screens))]
    public void A_screen_too_small_for_the_card_draws_all_of_it_smaller_and_cuts_nothing_off(int width, int height, int topInset, int bottomInset)
    {
        PageLayout page = PageLayout.Of(new ScreenShape(width, height, topInset, bottomInset));
        float room = page.View.UsableTop - page.View.UsableBottom - 2 * PageLayout.CardMarginAboveAndBelow;
        float widest = (page.View.Width - 2 * PageLayout.CardSideMargin) / LevelStartLayout.BodyWidth;

        CardPlace place = LevelStartLayout.PlaceOn(page, hasRewardLine: true);

        // The canvas' card is 362 wide on a canvas of 390: on a phone 360 points wide it is drawn at 332 / 362.
        Assert.Equal(Math.Min(1f, Math.Min(widest, room / LevelStartLayout.Height(hasRewardLine: true))), place.Scale, precision: 4);
    }

    [Theory]
    [InlineData(1080, 2340)]
    [InlineData(1080, 1920)]
    public void On_a_phone_360_points_wide_the_card_is_drawn_at_the_share_the_canvas_card_has_of_its_screen(int width, int height)
    {
        PageLayout page = PageLayout.Of(new ScreenShape(width, height));

        Assert.Equal(332f / 362f, LevelStartLayout.PlaceOn(page, hasRewardLine: true).Scale, precision: 4);
    }

    [Fact]
    public void The_green_button_stands_over_the_cards_lower_edge_and_reaches_under_it_as_drawn()
    {
        // LevelStartTR: the card's body 392 high; Oyna's middle 5 over its foot; a pill of 72 with a lip of 7.
        Assert.Equal(38f, LevelStartLayout.RidesBelow, precision: 3);
        Assert.Equal(392f + 25f + 38f, LevelStartLayout.Height(hasRewardLine: true), precision: 3);
        Assert.Equal(392f + 20f + 38f, LevelStartLayout.Height(hasRewardLine: false), precision: 3);
    }

    [Fact]
    public void Two_goals_stand_beside_their_numbers_as_drawn_in_the_middle_of_the_goals_column()
    {
        IReadOnlyList<StartGoalPlace> goals = LevelStartLayout.Goals(new[] { TwoFigures, TwoFigures });

        Assert.All(goals, goal => Assert.Equal((54f, 34f, LevelStartLayout.GoalsRowDown), (goal.IconSize, goal.NumberSize, goal.IconDown)));
        Assert.All(goals, goal => Assert.True(goal.NumberRight > goal.IconRight + goal.IconSize / 2f, "a number stands at the right of its icon"));
        float left = goals[0].LeftEdge(TwoFigures);
        float right = goals[1].RightEdge(TwoFigures);
        Assert.Equal(LevelStartLayout.GoalsRight, (left + right) / 2f, precision: 3);
        Assert.Equal(54f + 5f + TwoFigures, goals[0].RightEdge(TwoFigures) - left, precision: 3);
        Assert.Equal(14f, goals[1].LeftEdge(TwoFigures) - goals[0].RightEdge(TwoFigures), precision: 3);
    }

    [Fact]
    public void One_goal_stands_beside_its_number_in_the_middle_of_the_column()
    {
        StartGoalPlace goal = Assert.Single(LevelStartLayout.Goals(new[] { ThreeFigures }));

        Assert.Equal(LevelStartLayout.GoalsRight, (goal.LeftEdge(ThreeFigures) + goal.RightEdge(ThreeFigures)) / 2f, precision: 3);
        Assert.Equal(54f, goal.IconSize);
    }

    [Fact]
    public void Two_goals_with_long_numbers_are_drawn_smaller_to_fit_the_column()
    {
        IReadOnlyList<StartGoalPlace> goals = LevelStartLayout.Goals(new[] { ThreeFigures, ThreeFigures });
        float scale = goals[0].IconSize / LevelStartLayout.GoalIconSize;

        Assert.True(scale < 1f);
        Assert.Equal(LevelStartLayout.GoalsRoom, goals[1].RightEdge(scale * ThreeFigures) - goals[0].LeftEdge(scale * ThreeFigures), precision: 2);
        Assert.Equal(scale * LevelStartLayout.GoalNumberSize, goals[0].NumberSize, precision: 4);
    }

    [Theory]
    [InlineData(TwoFigures)]
    [InlineData(ThreeFigures)]
    public void Three_goals_fit_the_goals_column_each_over_its_number_clear_of_the_medallion_and_the_boosters(float numberWidth)
    {
        IReadOnlyList<StartGoalPlace> goals = LevelStartLayout.Goals(new[] { numberWidth, numberWidth, numberWidth });
        float drawnWidth = numberWidth * goals[0].NumberSize / LevelStartLayout.GoalNumberSize;
        float columnLeft = LevelStartLayout.GoalsRight - LevelStartLayout.GoalsRoom / 2f;
        float columnRight = LevelStartLayout.GoalsRight + LevelStartLayout.GoalsRoom / 2f;
        float medallionLeft = LevelStartLayout.MedallionRight - LevelStartLayout.MedallionSize / 2f;

        Assert.Equal(3, goals.Count);
        Assert.All(goals, goal => Assert.Equal(goal.IconRight, goal.NumberRight));
        Assert.All(goals, goal => Assert.True(goal.NumberDown > goal.IconDown, "a number stands under its icon"));
        Assert.True(goals[0].LeftEdge(drawnWidth) >= columnLeft - 0.01f && goals[2].RightEdge(drawnWidth) <= columnRight + 0.01f, "the goals run out of their column");
        Assert.True(goals[2].RightEdge(drawnWidth) < medallionLeft, "the goals reach the medallion");
        Assert.True(goals[0].RightEdge(drawnWidth) < goals[1].LeftEdge(drawnWidth) && goals[1].RightEdge(drawnWidth) < goals[2].LeftEdge(drawnWidth), "two goals overlap");

        // The boosters' header is a line of 10 points with its diamonds: what is over it keeps clear of its top.
        Assert.All(goals, goal => Assert.True(goal.Bottom < LevelStartLayout.BoostersHeaderDown - 5f, $"a goal reaches {goal.Bottom}, into the boosters' header"));
    }

    [Fact]
    public void A_level_has_one_to_three_goals_to_place()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LevelStartLayout.Goals(Array.Empty<float>()));
        Assert.Throws<ArgumentOutOfRangeException>(() => LevelStartLayout.Goals(new[] { 1f, 1f, 1f, 1f }));
    }

    [Fact]
    public void The_three_discs_stand_in_three_equal_columns_of_the_shelf_as_drawn()
    {
        Assert.Equal(new[] { -104f, 0f, 104f }, Enumerable.Range(0, 3).Select(LevelStartLayout.DiscRight).ToArray());

        // Each disc inside the shelf, which keeps 8 points at its ends.
        float halfShelf = LevelStartLayout.ShelfWidth / 2f;
        Assert.True(LevelStartLayout.DiscRight(2) + LevelStartLayout.DiscSize / 2f <= halfShelf - 8f);
        Assert.True(Math.Abs(LevelStartLayout.DiscDown - LevelStartLayout.ShelfDown) + LevelStartLayout.DiscSize / 2f <= LevelStartLayout.ShelfHeight / 2f);
    }
}
