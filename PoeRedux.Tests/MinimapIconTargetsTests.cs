using PoeRedux.Services;

namespace PoeRedux.Tests;

public class MinimapIconTargetsTests
{
    [Fact]
    public void Rewrite_ReplacesExistingIcon_LikeStrongbox()
    {
        // Real body from metadata/chests/strongboxes/strongbox.ot.
        var text = "MinimapIcon\r\n{\r\n\ticon = \"Strongbox\"\r\n}\r\n\r\nStateMachine\r\n{\r\n}";

        var patched = MinimapIconTargets.Rewrite(text, "LootFilterLargeYellowDiamond");

        Assert.Contains("icon = \"LootFilterLargeYellowDiamond\"", patched);
        Assert.DoesNotContain("\"Strongbox\"", patched);
        Assert.Contains("StateMachine", patched);
    }

    [Fact]
    public void Rewrite_InsertsIcon_WhenBlockIsMultilineEmpty()
    {
        // Real body from metadata/chests/leagueheist/heistchestbase.ot: MinimapIcon with no icon key.
        var text = "extends \"Metadata/Chests/Chest\"\r\n\r\nMinimapIcon\r\n{\r\n}\r\n\r\nInteractionAction {}";

        var patched = MinimapIconTargets.Rewrite(text, "LootFilterLargeYellowDiamond");

        Assert.Contains("MinimapIcon\r\n{\r\n\ticon = \"LootFilterLargeYellowDiamond\"\r\n}", patched);
    }

    [Fact]
    public void Rewrite_InsertsIcon_WhenBraceIsOnTheSameLine()
    {
        // Real body from metadata/chests/leaguedeepwater/currencytreasurechest.ot.
        var text = "Chest {}\r\n\r\nMinimapIcon {}\r\n\r\nInteractionAction {}";

        var patched = MinimapIconTargets.Rewrite(text, "LootFilterLargeYellowDiamond");

        Assert.Matches(@"MinimapIcon \{\r\n\ticon = ""LootFilterLargeYellowDiamond""\r\n\}", patched);
    }

    [Fact]
    public void Block_Matches_BraceOnSameLineAsHeader()
    {
        var text = "Chest {}\r\n\r\nMinimapIcon {}\r\n\r\nInteractionAction {}";

        Assert.Matches(MinimapIconTargets.Block, text);
    }

    [Fact]
    public void CategoryOf_NoIconKey_IsItsOwnCategory()
    {
        var category = MinimapIconTargets.CategoryOf("~NoIcon:metadata/chests/leaguedeepwater/currencytreasurechest.ot");

        Assert.Equal("No Default Icon", category);
    }

    [Fact]
    public void DisplayNameOf_NoIconKey_StripsPrefixAndExtension()
    {
        var name = MinimapIconTargets.DisplayNameOf("~NoIcon:metadata/chests/leaguedeepwater/currencytreasurechest.ot");

        Assert.Equal("metadata/chests/leaguedeepwater/currencytreasurechest", name);
    }

    [Fact]
    public void DisplayNameOf_RealIcon_IsUnchanged()
    {
        Assert.Equal("Strongbox", MinimapIconTargets.DisplayNameOf("Strongbox"));
    }
}
