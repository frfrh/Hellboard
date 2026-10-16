// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;
using AwesomeAssertions;
using HellboardCompanion.Core.Hellboard;
using Microsoft.Extensions.Logging.Abstractions;

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
    public void Every_stratagem_is_a_complete_json_object_on_its_own_line()
    {
        // The firmware reads doc/stratagems.json line by line (decision F11).
        using var stream = typeof(StratagemCatalog).Assembly.GetManifestResourceStream("HellboardCompanion.Core.Hellboard.stratagems.json")!;
        var lines = new StreamReader(stream).ReadToEnd().Split('\n').Select(l => l.Trim().TrimEnd(',')).ToList();

        var entries = lines.Where(l => l.StartsWith("{\"id\":", StringComparison.Ordinal)).ToList();

        entries.Should().HaveCount(Catalog.Entries.Count);
        entries.Should().OnlyContain(l => System.Text.Json.JsonDocument.Parse(l, default).RootElement.GetProperty("id").GetString() != null);
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

    [Fact]
    public void User_entries_replace_built_in_ones_and_add_new_ones()
    {
        var builtIn = ToStream("""{"version": 1, "categories": {"Mission": "yellow"}, "stratagems": [{"id": "a", "name": "A", "category": "Mission", "code": "U"}, {"id": "b", "name": "B", "category": "Mission", "code": "D"}]}""");
        var user = ToStream("""{"$comment": "x", "version": 1, "categories": {"Custom": "white"}, "stratagems": [{"id": "b", "name": "B fixed", "category": "Mission", "code": "LR"}, {"id": "c", "name": "C", "category": "Custom", "code": null}]}""");

        var catalog = StratagemCatalog.Parse(builtIn, user);

        catalog.Find("a")!.Sequence.Should().Be("U");
        catalog.Find("b").Should().Be(new CatalogEntry("b", "B fixed", "Mission", "yellow", "LR"));
        catalog.Find("c").Should().Be(new CatalogEntry("c", "C", "Custom", "white", null));
        catalog.UserIds.Should().BeEquivalentTo("b", "c");
    }

    [Fact]
    public void A_missing_user_list_is_created_as_an_empty_template()
    {
        var dir = Directory.CreateTempSubdirectory("hellboard-catalog").FullName;
        try
        {
            var file = Path.Combine(dir, "stratagems.json");

            var catalog = StratagemCatalog.Load(file, NullLogger.Instance);

            catalog.Entries.Should().HaveCount(Catalog.Entries.Count);
            using var template = File.OpenRead(file);
            StratagemCatalog.Load(file, NullLogger.Instance).UserIds.Should().BeEmpty();
            using var builtIn = typeof(StratagemCatalog).Assembly.GetManifestResourceStream("HellboardCompanion.Core.Hellboard.stratagems.json")!;
            StratagemCatalog.Parse(builtIn, template).Entries.Should().HaveCount(Catalog.Entries.Count);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void An_invalid_user_list_is_ignored()
    {
        var dir = Directory.CreateTempSubdirectory("hellboard-catalog").FullName;
        try
        {
            var file = Path.Combine(dir, "stratagems.json");
            File.WriteAllText(file, """{"version": 1, "stratagems": [{"id": "machine_gun", "name": "MG", "category": "Mission", "code": "XYZ"}]}""");

            var catalog = StratagemCatalog.Load(file, NullLogger.Instance);

            catalog.Find("machine_gun")!.Sequence.Should().Be("DLDUR");
            catalog.UserIds.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static StratagemCatalog Parse(string stratagems) =>
        StratagemCatalog.Parse(ToStream($$"""{"version": 1, "categories": {"Mission": "yellow"}, "stratagems": [{{stratagems}}]}"""));

    private static MemoryStream ToStream(string json) => new(Encoding.UTF8.GetBytes(json));
}
