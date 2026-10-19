// SPDX-License-Identifier: GPL-3.0-or-later

using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using HellboardCompanion.Core.Catalog;
using HellboardCompanion.Core.Hellboard;
using SkiaSharp;

namespace HellboardCompanion.Core.Tests.Catalog;

public sealed class WikiIconDownloaderTests : IDisposable
{
    private const string Plain = """<svg viewBox="0 0 256 256" xmlns="http://www.w3.org/2000/svg"><circle cx="128" cy="128" r="60" fill="#ffe"/></svg>""";

    /// <summary>Like the wiki's icons with background: dark tile, frame in the category color, icon.</summary>
    private const string WithBackground = """
        <svg viewBox="0 0 256 256" xmlns="http://www.w3.org/2000/svg">
          <rect width="256" height="256" fill="#011419"/>
          <path d="M0 0h256v256H0zM15 15v226h226V15z" fill="#55b9d2" fill-rule="evenodd"/>
          <circle cx="128" cy="128" r="60" fill="#ffe"/>
        </svg>
        """;

    private readonly string _dir = Directory.CreateTempSubdirectory("hellboard-wiki-icons").FullName;
    private readonly FakeWiki _wiki = new();
    private readonly Clock _clock = new();
    private readonly WikiIconDownloader _downloader;

    private readonly WikiIconRequest[] _requests =
    [
        new("cremator", "Cremator", "Support Weapon", "Cremator Stratagem Icon Background.svg", true),
        new("meltagun", "Meltagun", "Support Weapon", "Meltagun Stratagem Icon Background.svg", true),
    ];

    public WikiIconDownloaderTests()
    {
        _downloader = new WikiIconDownloader(new HttpClient(_wiki), _clock);
        _wiki.Files["Cremator Stratagem Icon.svg"] = Plain;
        _wiki.Files["Meltagun Stratagem Icon Background.svg"] = WithBackground;   // no plain icon on the wiki
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private Task<IconUpdateResult> Update(bool force = false) =>
        _downloader.UpdateAsync(_dir, _requests, force, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Plain_icons_are_downloaded_and_missing_ones_derived_from_the_icon_with_background()
    {
        var result = await Update();

        result.Added.Should().BeEquivalentTo("Cremator Stratagem Icon.svg", "Meltagun Stratagem Icon.svg");
        var icons = WikiIconDownloader.LoadIcons(_dir);
        icons.Select(i => (i.Id, Path.GetFileName(i.ImagePath))).Should().Equal(
            ("cremator", "Cremator Stratagem Icon.svg"), ("meltagun", "Meltagun Stratagem Icon.png"));

        using var derived = SKBitmap.Decode(icons[1].ImagePath);
        derived.GetPixel(5, 5).Alpha.Should().Be(0, "the frame is removed");
        derived.GetPixel(40, 40).Alpha.Should().Be(0, "the tile is removed");
        derived.GetPixel(128, 128).Should().Be(new SKColor(0xff, 0xff, 0xee), "the icon stays");
    }

    [Fact]
    public async Task The_wiki_is_checked_at_most_once_a_day_and_only_changed_files_are_downloaded()
    {
        await Update();
        _clock.Advance(TimeSpan.FromHours(5));

        (await Update()).Checked.Should().BeFalse();
        _wiki.InfoRequests.Should().Be(2, "plain icons, then the icon with background of the Meltagun");

        _wiki.Files["Cremator Stratagem Icon.svg"] = Plain.Replace("r=\"60\"", "r=\"50\"", StringComparison.Ordinal);
        var result = await Update(force: true);

        result.Updated.Should().Equal("Cremator Stratagem Icon.svg");
        _wiki.Downloads.Should().Be(3);
    }

    [Fact]
    public async Task A_plain_icon_on_the_wiki_replaces_the_derived_one()
    {
        await Update();
        _wiki.Files["Meltagun Stratagem Icon.svg"] = Plain;

        await Update(force: true);

        WikiIconDownloader.LoadIcons(_dir).Single(i => i.Id == "meltagun").ImagePath.Should().EndWith("Meltagun Stratagem Icon.svg");
        File.Exists(Path.Combine(_dir, "Meltagun Stratagem Icon.png")).Should().BeFalse();
    }

    [Fact]
    public void Shared_icons_are_recognizable_only_for_the_stratagem_they_are_named_after()
    {
        WikiStratagem[] wiki =
        [
            new("Upload Data", "Upload Data", "Objective", "LRUUU", "Upload Data Stratagem Icon Background.svg"),
            new("SSSD Delivery", "SSSD Delivery", "Objective", "DDDUU", "Upload Data Stratagem Icon Background.svg"),
            new("Aquifer Drill", "Aquifer Drill", "Objective", "LLLUDRDD", "Drill Stratagem Icon Background.svg"),
            new("Tectonic Drill", "Tectonic Drill", "Objective", "UDUDUD", "Drill Stratagem Icon Background.svg"),
            new("B/FLAM-80 Cremator", "B/FLAM-80 Cremator", "Support Weapon", "DDRDUU", "Cremator Stratagem Icon Background.svg"),
            new("AMW-20 Apex", "AMW-20 Apex", "Support Weapon", null, "Support Weapon Stratagem Fallback Icon.svg"),
        ];

        var requests = WikiIconRequest.From(wiki, StratagemCatalog.LoadSeed(wiki));

        requests.ToDictionary(r => r.Id, r => r.Recognizable).Should().BeEquivalentTo(new Dictionary<string, bool>
        {
            ["upload_data"] = true, ["sssd_delivery"] = false, ["aquifer_drill"] = false, ["tectonic_drill"] = false, ["cremator"] = true,
        });
    }

    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }

    /// <summary>MediaWiki <c>imageinfo</c> (URL and SHA-1 per file) and the file downloads.</summary>
    private sealed class FakeWiki : HttpMessageHandler
    {
        public Dictionary<string, string> Files { get; } = new(StringComparer.Ordinal);

        private int _infoRequests;
        private int _downloads;

        public int InfoRequests => Volatile.Read(ref _infoRequests);

        public int Downloads => Volatile.Read(ref _downloads);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            if (uri.AbsolutePath.EndsWith("api.php", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _infoRequests);
                var titles = Uri.UnescapeDataString(uri.Query.Split("titles=")[1]).Split('|');
                var pages = new JsonObject();
                foreach (var (title, n) in titles.Select((t, n) => (t, n)))
                {
                    var name = title["File:".Length..];
                    pages[$"{-n - 1}"] = Files.TryGetValue(name, out var content)
                        ? new JsonObject
                        {
                            ["title"] = title,
                            ["imageinfo"] = new JsonArray(new JsonObject
                            {
                                ["url"] = $"https://helldivers.wiki.gg/images/{Uri.EscapeDataString(name)}",
                                ["sha1"] = Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes(content))),
                            }),
                        }
                        : new JsonObject { ["title"] = title, ["missing"] = string.Empty };
                }

                var answer = new JsonObject { ["query"] = new JsonObject { ["pages"] = pages } };
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(answer.ToJsonString()) });
            }

            Interlocked.Increment(ref _downloads);
            var file = Uri.UnescapeDataString(uri.AbsolutePath["/images/".Length..]);
            return Task.FromResult(Files.TryGetValue(file, out var svg)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(svg) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
