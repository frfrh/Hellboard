// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;
using AwesomeAssertions;
using HellboardCompanion.Core.Hellboard;

namespace HellboardCompanion.Core.Tests.Hellboard;

/// <summary>Checks <c>doc/stratagems.json</c> (embedded) and its parser.</summary>
public sealed class StratagemCatalogTests
{
    private static readonly StratagemCatalog Catalog = StratagemCatalog.LoadSeed();

    [Fact]
    public void The_embedded_list_is_read_completely()
    {
        Catalog.Entries.Should().HaveCountGreaterThanOrEqualTo(115);
        Catalog.Entries.Should().OnlyContain(e => e.IsTransferable);
    }

    [Theory]
    [InlineData("eagle_500kg_bomb", "Eagle 500KG Bomb", "Eagle", "red", "URDDD")]
    [InlineData("machine_gun", "Machine Gun", "Support weapon", "blue", "DLDUR")]
    [InlineData("anti_tank_emplacement", "Anti-Tank Emplacement", "Defensive", "green", "DULRRR")]
    [InlineData("reinforce", "Reinforce", "Mission", "yellow", "UDRLU")]
    [InlineData("sssd_delivery", "SSSD Delivery", "Mission", "yellow", "DDDUU")]
    public void Entries_carry_name_color_and_sequence(string id, string name, string category, string color, string sequence)
    {
        Catalog.Find(id).Should().Be(new CatalogEntry(id, name, category, color, sequence));
    }

    [Fact]
    public void Codes_are_unique_except_the_documented_overlap()
    {
        // Reinforcement Pods and Upload Data share their code in all sources (doc/stratagems.md).
        var duplicates = Catalog.Entries
            .GroupBy(e => e.Sequence)
            .Where(g => g.Count() > 1)
            .Select(g => string.Join("+", g.Select(e => e.Id).Order()))
            .ToList();

        duplicates.Should().Equal("reinforcement_pods+upload_data");
    }

    [Fact]
    public void An_entry_without_code_is_not_transferable()
    {
        var catalog = Parse("""{"id": "x", "name": "X", "category": "Mission", "code": null}""");

        catalog.Find("x")!.IsTransferable.Should().BeFalse();
    }

    [Theory]
    [InlineData("""{"id": "x", "name": "X", "category": "Mission", "code": "UW"}""", "invalid code")]
    [InlineData("""{"id": "x", "name": "X", "category": "Mission", "code": "UUUUUUUUUUU"}""", "invalid code")]
    [InlineData("""{"id": "x", "name": "X", "category": "Boosters", "code": "U"}""", "unknown category")]
    [InlineData("""{"id": "X y", "name": "X", "category": "Mission", "code": "U"}""", "invalid id")]
    [InlineData("""{"id": "x", "name": "", "category": "Mission", "code": "U"}""", "name missing")]
    [InlineData("""{"id": "x", "name": "X", "category": "Mission", "code": "U"}, {"id": "x", "name": "X", "category": "Mission", "code": "D"}""", "duplicate id")]
    [InlineData("""{"id": "x",""", "not valid JSON")]
    public void Invalid_entries_are_rejected(string stratagems, string message)
    {
        var parse = () => Parse(stratagems);

        parse.Should().Throw<FormatException>().WithMessage($"*{message}*");
    }

    [Fact]
    public void An_unknown_version_is_rejected()
    {
        var parse = () => StratagemCatalog.Parse(ToStream("""{"version": 2, "categories": {}, "stratagems": []}"""));

        parse.Should().Throw<FormatException>().WithMessage("*version 2*");
    }

    private static StratagemCatalog Parse(string stratagems) =>
        StratagemCatalog.Parse(ToStream($$"""{"version": 1, "categories": {"Mission": "yellow"}, "stratagems": [{{stratagems}}]}"""));

    private static MemoryStream ToStream(string json) => new(Encoding.UTF8.GetBytes(json));
}
