// SPDX-License-Identifier: GPL-3.0-or-later

using System.Net;
using AwesomeAssertions;
using HellboardCompanion.Core.Catalog;

namespace HellboardCompanion.Core.Tests.Catalog;

public sealed class WikiClientTests
{
    private const string Down = "<span class=\\\"Stratagemcodeicon icon-outline\\\">[[File:Stratagem Arrow Down.svg|link=]]</span>";
    private const string Up = "<span class=\\\"Stratagemcodeicon icon-outline\\\">[[File:Stratagem Arrow Up.svg|link=]]</span>";
    private const string Left = "[[File:Stratagem Arrow Left.svg|link=]]";
    private const string Right = "[[File:Stratagem Arrow Right.svg|link=]]";

    /// <summary>Answer of the Cargo query in the format of helldivers.wiki.gg (2026-10-19), shortened.</summary>
    private static readonly string Answer = """
        {"cargoquery": [
          {"title": {"page": "B/FLAM-80 Cremator", "title": "B/FLAM-80 Cremator", "code": "@Down@@Down@@Right@@Down@@Up@@Up@", "type": "Support Weapon", "image": "Cremator Stratagem Icon Background.svg"}},
          {"title": {"page": "A/MG-43 Machine Gun Sentry", "title": "A/MG-43 Machine Gun Sentry", "code": "@Down@@Up@@Right@@Right@@Up@", "type": "Sentry", "image": "Machine Gun Sentry Stratagem Icon Background.svg"}},
          {"title": {"page": "SSSD Delivery", "title": "SSSD Delivery", "code": "@Down@@Down@@Down@@Up@@Up@", "type": "Objective", "image": "Upload Data Stratagem Icon Background.svg"}},
          {"title": {"page": "SSSD Delivery", "title": "SSSD Delivery", "code": "@Down@@Down@@Down@@Down@@Down@@Up@@Up@", "type": "Objective", "image": "Upload Data Stratagem Icon Background.svg"}},
          {"title": {"page": "AMW-20 Apex", "title": "AMW-20 Apex", "code": "", "type": "Support Weapon", "image": "Support Weapon Stratagem Fallback Icon.svg"}},
          {"title": {"page": "Seize Industrial Complex", "title": "Super Earth Flag", "code": "@Down@@Up@@Down@@Up@", "type": "", "image": "Super Earth Flag Stratagem Icon Background.svg"}},
          {"title": {"page": "Eagle 500kg Bomb", "title": "Eagle 500kg Bomb", "code": "@Up@@Right@@Down@@Down@@Down@", "type": "Eagle", "image": "Eagle_500kg_Bomb Stratagem Icon Background.svg"}},
          {"title": {"page": "Stratagem &amp; Co", "title": "Stratagem &amp; Co", "code": "@Left@", "type": "Other", "image": ""}}
        ]}
        """.Replace("@Down@", Down).Replace("@Up@", Up).Replace("@Left@", Left).Replace("@Right@", Right);

    [Fact]
    public void Rows_are_read_with_code_category_and_icon()
    {
        var stratagems = WikiClient.ParseStratagems(Answer);

        var cremator = stratagems[0];
        cremator.Should().Be(new WikiStratagem("B/FLAM-80 Cremator", "B/FLAM-80 Cremator", "Support Weapon", "DDRDUU", "Cremator Stratagem Icon Background.svg"));
        cremator.Category.Should().Be("Support weapon");
        cremator.IconName.Should().Be("Cremator");
        stratagems[1].Category.Should().Be("Defensive");
        stratagems.Single(s => s.Page == "Eagle 500kg Bomb").IconName.Should().Be("Eagle 500kg Bomb");
        stratagems.Single(s => s.Page == "Stratagem & Co").Category.Should().Be("Mission");   // HTML entities decoded
    }

    [Fact]
    public void Rows_without_type_are_skipped_and_several_codes_are_kept()
    {
        var stratagems = WikiClient.ParseStratagems(Answer);

        stratagems.Should().NotContain(s => s.Page == "Seize Industrial Complex");
        stratagems.Where(s => s.Page == "SSSD Delivery").Select(s => s.Code).Should().Equal("DDDUU", "DDDDDUU");
    }

    [Fact]
    public void An_unreleased_stratagem_has_no_code_and_no_icon()
    {
        var apex = WikiClient.ParseStratagems(Answer).Single(s => s.Page == "AMW-20 Apex");

        apex.Code.Should().BeNull();
        apex.IconName.Should().BeEmpty();
    }

    [Theory]
    [InlineData("""{"error": {"code": "badparams", "info": "Table Stratagems not found."}}""", "*Table Stratagems not found*")]
    [InlineData("""{"cargoquery": []}""", "*no rows with a code*")]
    [InlineData("""{"cargoquery": [{"title": {"page": "X", "type": "Eagle", "code": "{{Stratagem Code|U|D}}"}}]}""", "*no rows with a code*")]
    [InlineData("""{"cargoquery": [{"page": "X"}]}""", "*Unexpected answer*")]
    [InlineData("<html>maintenance</html>", "*Unexpected answer*")]
    public void A_changed_wiki_structure_is_reported(string json, string message)
    {
        var parse = () => WikiClient.ParseStratagems(json);

        parse.Should().Throw<InvalidDataException>().WithMessage(message);
    }

    [Fact]
    public async Task The_stratagem_table_is_queried_through_the_cargo_api()
    {
        var handler = new FakeWiki(Answer);

        var stratagems = await new WikiClient(new HttpClient(handler)).GetStratagemsAsync(TestContext.Current.CancellationToken);

        stratagems.Should().HaveCount(7);
        handler.Request!.Host.Should().Be("helldivers.wiki.gg");
        handler.Request.Query.Should().Contain("action=cargoquery").And.Contain("tables=Stratagems");
    }

    private sealed class FakeWiki(string answer) : HttpMessageHandler
    {
        public Uri? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(answer) });
        }
    }
}
