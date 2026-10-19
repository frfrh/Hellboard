// SPDX-License-Identifier: GPL-3.0-or-later

using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using HellboardCompanion.Core.Catalog;
using HellboardCompanion.Core.Hellboard;
using HellboardCompanion.Core.Platform;
using Microsoft.Extensions.Logging.Abstractions;

namespace HellboardCompanion.Core.Tests.Catalog;

/// <summary>The wiki layer of the stratagem catalog and its store (wiki-source-plan §3.2).</summary>
public sealed class WikiLayerTests : IDisposable
{
    private const string BuiltIn = """
        {"version": 1, "categories": {"Eagle": "red", "Support weapon": "blue", "Mission": "yellow"}, "stratagems": [
          {"id": "cremator", "name": "Cremator", "designation": "B/FLAM-80 Cremator", "category": "Support weapon", "code": "DDRDUU", "notes": "Verified in game"},
          {"id": "eagle_500kg_bomb", "name": "Eagle 500kg Bomb", "designation": "Eagle 500kg Bomb", "category": "Eagle", "code": "URDDD", "notes": null},
          {"id": "apex", "name": "Apex", "designation": "AMW-20 Apex", "category": "Support weapon", "code": null, "notes": null},
          {"id": "sssd_delivery", "name": "SSSD Delivery", "designation": "SSSD Delivery", "category": "Mission", "code": "DDDUU", "notes": null}
        ]}
        """;

    private readonly string _root = Directory.CreateTempSubdirectory("hellboard-wiki").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static WikiStratagem Row(string page, string type, string? code, string image = "") => new(page, page, type, code, image);

    private static StratagemCatalog Parse(IReadOnlyList<WikiStratagem> wiki, string? user = null) => StratagemCatalog.Parse(
        new MemoryStream(Encoding.UTF8.GetBytes(BuiltIn)), user is null ? null : new MemoryStream(Encoding.UTF8.GetBytes(user)), wiki);

    [Fact]
    public void A_new_stratagem_is_added_with_short_name_category_and_code()
    {
        var catalog = Parse([Row("LAS-44 Nova", "Support Weapon", "DLURR", "Nova Stratagem Icon Background.svg")]);

        catalog.Find("nova").Should().Be(new CatalogEntry("nova", "Nova", "Support weapon", "blue", "DLURR", "LAS-44 Nova"));
        catalog.WikiIds.Should().Equal("nova");
        catalog.WikiProposals.Should().BeEmpty();
    }

    [Fact]
    public void A_missing_code_is_filled_in()
    {
        var catalog = Parse([Row("AMW-20 Apex", "Support Weapon", "LLUDR")]);

        catalog.Find("apex")!.Sequence.Should().Be("LLUDR");
        catalog.WikiIds.Should().Equal("apex");
    }

    [Fact]
    public void A_different_code_is_only_proposed()
    {
        var catalog = Parse([Row("B/FLAM-80 Cremator", "Support Weapon", "DLULUU")]);

        catalog.Find("cremator")!.Sequence.Should().Be("DDRDUU");
        catalog.WikiProposals.Should().Equal(new WikiProposal("cremator", "DDRDUU", "DLULUU"));
        catalog.Note("cremator").Should().Be("Verified in game");
        catalog.WikiIds.Should().BeEmpty();
    }

    [Fact]
    public void No_proposal_when_one_of_several_wiki_codes_is_in_use()
    {
        var catalog = Parse([Row("SSSD Delivery", "Objective", "DDDDDUU"), Row("SSSD Delivery", "Objective", "DDDUU")]);

        catalog.WikiProposals.Should().BeEmpty();
    }

    [Fact]
    public void Designation_and_short_name_follow_the_wiki_page()
    {
        // Matched by slug (eagle_500kg_bomb), the page was renamed.
        var catalog = Parse([Row("Eagle 500KG Bomb", "Eagle", "URDDD")]);

        catalog.Find("eagle_500kg_bomb")!.FullName.Should().Be("Eagle 500KG Bomb");
        catalog.Find("eagle_500kg_bomb")!.Name.Should().Be("Eagle 500KG Bomb");
        catalog.Entries.Should().HaveCount(4, "a renamed page is not a new stratagem");
    }

    [Fact]
    public void The_user_list_wins_and_proposals_compare_with_its_code()
    {
        const string user = """{"version": 1, "stratagems": [{"id": "apex", "name": "My Apex", "category": "Support weapon", "code": "UUUU"}]}""";

        var catalog = Parse([Row("AMW-20 Apex", "Support Weapon", "LLUDR")], user);

        catalog.Find("apex").Should().Be(new CatalogEntry("apex", "My Apex", "Support weapon", "blue", "UUUU", "AMW-20 Apex"));
        catalog.WikiIds.Should().BeEmpty("the user's entry replaces the wiki's");
        catalog.WikiProposals.Should().Equal(new WikiProposal("apex", "UUUU", "LLUDR"));
    }

    [Fact]
    public void Invalid_wiki_codes_and_unusable_rows_are_ignored()
    {
        var catalog = Parse(
        [
            Row("AMW-20 Apex", "Support Weapon", "UDUDUDUDUDU"),   // 11 arrows: longer than the Hellboard allows
            Row("XM-1 Mystery", "Support Weapon", "UD", "Support Weapon Stratagem Fallback Icon.svg"),   // placeholder icon: id from the short name
        ]);

        catalog.Find("apex")!.Sequence.Should().BeNull();
        catalog.Find("mystery")!.Sequence.Should().Be("UD");
        catalog.Entries.Should().HaveCount(5);
    }

    [Fact]
    public void Without_wiki_data_the_catalog_is_the_built_in_list()
    {
        var catalog = Parse([]);

        catalog.Entries.Should().HaveCount(4);
        catalog.WikiIds.Should().BeEmpty();
        catalog.WikiProposals.Should().BeEmpty();
    }

    [Fact]
    public void Shared_icon_names_are_not_used_for_matching()
    {
        WikiStratagem[] rows =
        [
            Row("Aquifer Drill", "Objective", "LLLUDRDD", "Drill Stratagem Icon Background.svg"),
            Row("Tectonic Drill", "Objective", "UDUDUD", "Drill Stratagem Icon Background.svg"),
        ];

        var matches = WikiMatching.Match(rows, [new WikiMatchKey("drill", "Drill", "Drill")]);

        matches.Select(m => m.Id).Should().Equal(null, null);
    }

    // ---- store ------------------------------------------------------------------------------------

    [Fact]
    public async Task The_wiki_is_asked_at_most_once_a_day_unless_forced()
    {
        var (store, wiki, clock) = CreateStore();

        await store.UpdateAsync(cancellationToken: TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromHours(5));
        await store.UpdateAsync(cancellationToken: TestContext.Current.CancellationToken);
        wiki.Requests.Should().Be(1);

        await store.UpdateAsync(force: true, TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromDays(1));
        await store.UpdateAsync(cancellationToken: TestContext.Current.CancellationToken);
        wiki.Requests.Should().Be(3);
        store.Stratagems.Should().ContainSingle(s => s.Page == "B/FLAM-80 Cremator");
    }

    [Fact]
    public async Task Changed_is_raised_only_when_the_table_changed()
    {
        var (store, wiki, _) = CreateStore();
        var changes = 0;
        store.Changed += (_, _) => changes++;

        await store.UpdateAsync(cancellationToken: TestContext.Current.CancellationToken);
        await store.UpdateAsync(force: true, TestContext.Current.CancellationToken);
        wiki.Code = "DLULUU";
        await store.UpdateAsync(force: true, TestContext.Current.CancellationToken);

        changes.Should().Be(2);
    }

    [Fact]
    public async Task The_local_copy_survives_a_failed_check_and_a_restart()
    {
        var (store, wiki, _) = CreateStore();
        await store.UpdateAsync(cancellationToken: TestContext.Current.CancellationToken);
        wiki.Fail = true;

        await store.UpdateAsync(force: true, TestContext.Current.CancellationToken);

        store.LastError.Should().NotBeNull();
        store.Stratagems.Should().HaveCount(1);
        var (restarted, _, _) = CreateStore();
        restarted.Load();
        restarted.Stratagems.Should().Equal(store.Stratagems);
        restarted.CheckedAt.Should().Be(store.CheckedAt);
    }

    [Fact]
    public void A_dismissed_proposal_stays_hidden_until_the_wiki_lists_another_code()
    {
        var (store, _, _) = CreateStore();
        var proposal = new WikiProposal("cremator", "DDRDUU", "DLULUU");

        store.Dismiss(proposal);

        store.IsDismissed(proposal).Should().BeTrue();
        store.IsDismissed(proposal with { WikiCode = "UUUUUU" }).Should().BeFalse();
        var (restarted, _, _) = CreateStore();
        restarted.Load();
        restarted.IsDismissed(proposal).Should().BeTrue();
    }

    private (WikiStratagemStore Store, FakeWiki Wiki, Clock Clock) CreateStore()
    {
        var wiki = new FakeWiki();
        var clock = new Clock();
        return (new WikiStratagemStore(new PlatformPaths(_root, _root), NullLogger<WikiStratagemStore>.Instance, clock, wiki), wiki, clock);
    }

    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }

    private sealed class FakeWiki : HttpMessageHandler
    {
        public int Requests { get; private set; }

        public string Code { get; set; } = "DDRDUU";

        public bool Fail { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            if (Fail)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            }

            var arrows = string.Concat(Code.Select(c => $"[[File:Stratagem Arrow {c switch { 'U' => "Up", 'D' => "Down", 'L' => "Left", _ => "Right" }}.svg|link=]]"));
            var answer = new JsonObject
            {
                ["cargoquery"] = new JsonArray(new JsonObject
                {
                    ["title"] = new JsonObject
                    {
                        ["page"] = "B/FLAM-80 Cremator", ["title"] = "B/FLAM-80 Cremator", ["code"] = arrows,
                        ["type"] = "Support Weapon", ["image"] = "Cremator Stratagem Icon Background.svg",
                    },
                }),
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(answer.ToJsonString()) });
        }
    }
}
