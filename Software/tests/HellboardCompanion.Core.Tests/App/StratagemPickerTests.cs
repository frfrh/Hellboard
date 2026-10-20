// SPDX-License-Identifier: GPL-3.0-or-later

using AwesomeAssertions;
using HellboardCompanion.App.Services;
using HellboardCompanion.App.ViewModels;
using HellboardCompanion.Core.Hellboard;

namespace HellboardCompanion.Core.Tests.App;

public sealed class StratagemPickerTests
{
    private static readonly StratagemCatalog Catalog = new(
    [
        new("flamethrower", "Flamethrower", "Support weapon", "blue", "DLUDU", "FLAM-40 Flamethrower"),
        new("cremator", "Cremator", "Support weapon", "blue", "DLURL", "B/FLAM-80 Cremator"),
        new("orbital_laser", "Orbital Laser", "Orbital", "red", "RDURD"),
        new("eagle_airstrike", "Eagle Airstrike", "Eagle", "red", "URDR"),
        new("supply_pack", "Supply Pack", "Backpack", "blue", null, "B-1 Supply Pack"),
        new("hellbomb", "Hellbomb", "Mission", "yellow", "DULDURDU"),
    ]);

    private static readonly IReadOnlySet<string> NoMission = Catalog.MissionOnlyIds();

    [Fact]
    public void Groups_follow_the_category_order_and_are_sorted_by_short_name()
    {
        var groups = StratagemPicker.Filter(Catalog.Entries, null, null, NoMission);

        groups.Select(g => g.Category).Should().Equal("Orbital", "Eagle", "Support weapon", "Backpack");
        groups[2].Entries.Select(e => e.Id).Should().Equal("cremator", "flamethrower");
    }

    [Fact]
    public void Mission_stratagems_are_not_offered_for_player_slots()
    {
        StratagemPicker.Filter(Catalog.Entries, "hell", null, NoMission).Should().BeEmpty();
        StratagemPicker.Filter(Catalog.Entries, "hell", null, new HashSet<string>()).Should().ContainSingle();
    }

    [Theory]
    [InlineData("flam-80", "cremator")]       // full name
    [InlineData("CREMATOR", "cremator")]      // short name, any case
    [InlineData("orbital_l", "orbital_laser")] // id
    [InlineData("  supply ", "supply_pack")]  // surrounding blanks
    public void Search_matches_full_name_short_name_and_id(string search, string expected) =>
        StratagemPicker.Filter(Catalog.Entries, search, null, NoMission)
            .SelectMany(g => g.Entries).Should().ContainSingle().Which.Id.Should().Be(expected);

    [Fact]
    public void Category_limits_the_result()
    {
        StratagemPicker.Filter(Catalog.Entries, null, "Support weapon", NoMission).Should().ContainSingle()
            .Which.Entries.Should().HaveCount(2);
        StratagemPicker.Filter(Catalog.Entries, "laser", "Eagle", NoMission).Should().BeEmpty();
    }

    [Fact]
    public void Unknown_categories_follow_the_known_ones_alphabetically()
    {
        StratagemPicker.OrderCategories(["Zeta", "Mission", "Alpha", "Orbital", "Orbital"])
            .Should().Equal("Orbital", "Mission", "Alpha", "Zeta");
    }

    [Fact]
    public void Picker_marks_other_slots_missing_sequences_and_the_current_slot()
    {
        var picker = CreatePicker(["orbital_laser", "flamethrower", null, null], slot: 0);

        var items = picker.Groups.SelectMany(g => g.Items).ToDictionary(i => i.Id);
        items["flamethrower"].Note.Should().Be("slot 2");
        items["flamethrower"].IsDimmed.Should().BeTrue();
        items["supply_pack"].Note.Should().Be("no input sequence");
        items["orbital_laser"].IsCurrent.Should().BeTrue();
        items.Should().NotContainKey("hellbomb");
        picker.Categories.Select(c => c.Label).Should().Equal("All", "Orbital", "Eagle", "Support weapon", "Backpack");
    }

    [Fact]
    public void Recently_used_row_is_hidden_while_searching_or_filtering()
    {
        var picker = CreatePicker([null, null, null, null], slot: 0, recent: ["eagle_airstrike", "hellbomb", "unknown_id"]);

        picker.Recent.Select(i => i.Id).Should().Equal("eagle_airstrike");
        picker.ShowRecent.Should().BeTrue();

        picker.SearchText = "laser";
        picker.ShowRecent.Should().BeFalse();
        picker.Groups.SelectMany(g => g.Items).Should().ContainSingle().Which.Id.Should().Be("orbital_laser");

        picker.SearchText = string.Empty;
        picker.SelectCategoryCommand.Execute(picker.Categories[2]);
        picker.ShowRecent.Should().BeFalse();
        picker.Groups.Should().ContainSingle().Which.Title.Should().Be("Eagle");
    }

    [Fact]
    public void Choosing_raises_the_id_and_enter_takes_the_first_match()
    {
        var picker = CreatePicker([null, null, null, null], slot: 1);
        var chosen = new List<string?>();
        picker.Chosen += (_, id) => chosen.Add(id);

        picker.SearchText = "flam";
        picker.ChooseFirstCommand.Execute(null);
        picker.ClearCommand.Execute(null);

        chosen.Should().Equal("cremator", null);
    }

    [Fact]
    public void Recognized_stratagem_is_offered_only_when_it_differs_from_the_slot()
    {
        var chosen = new List<string?>();
        var picker = CreatePicker(["flamethrower", null, null, null], slot: 0, recognized: "cremator");
        picker.Chosen += (_, id) => chosen.Add(id);

        picker.CanUseRecognized.Should().BeTrue();
        picker.UseRecognizedText.Should().Be("Use recognized: B/FLAM-80 Cremator");
        picker.UseRecognizedCommand.Execute(null);
        chosen.Should().Equal("cremator");

        CreatePicker(["cremator", null, null, null], slot: 0, recognized: "cremator").CanUseRecognized.Should().BeFalse();
    }

    private static StratagemPickerViewModel CreatePicker(string?[] shown, int slot, string? recognized = null, string[]? recent = null) =>
        new(Catalog, new StratagemImages(null), shown, slot, recognized, recent ?? []);
}
