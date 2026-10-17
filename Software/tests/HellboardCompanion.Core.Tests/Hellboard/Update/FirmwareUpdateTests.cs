// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using HellboardCompanion.Core.Hellboard;
using HellboardCompanion.Core.Hellboard.Update;
using HellboardCompanion.Core.Platform;
using Microsoft.Extensions.Logging.Abstractions;

namespace HellboardCompanion.Core.Tests.Hellboard.Update;

public sealed class FirmwareUpdateTests
{
    private static readonly StratagemCatalog Catalog = StratagemCatalog.LoadSeed();

    [Fact]
    public void The_embedded_bundle_contains_the_firmware_and_its_version()
    {
        var bundle = FirmwareBundle.LoadEmbedded(Catalog);

        bundle.Version.Should().NotBeNullOrWhiteSpace();
        bundle.Files.Select(f => f.Target).Should().Contain(["main.py", "app.py", "protocol.py", "keyboard.py", "config.json", "stratagems.json", "version.json"]);
        bundle.Files[^1].Target.Should().Be("main.py");
        var version = JsonDocument.Parse(bundle.Files.Single(f => f.Target == "version.json").Content).RootElement.GetProperty("version").GetString();
        version.Should().Be(bundle.Version);
        bundle.Obsolete.Should().Contain("macro.py");
    }

    [Fact]
    public void The_board_gets_the_merged_stratagem_list_one_entry_per_line()
    {
        using var builtIn = typeof(StratagemCatalog).Assembly.GetManifestResourceStream("HellboardCompanion.Core.Hellboard.stratagems.json")!;
        using var user = new MemoryStream(Encoding.UTF8.GetBytes("""{"version": 1, "stratagems": [{"id": "my_flag", "name": "My Flag", "category": "Mission", "code": "UUDD"}]}"""));
        var catalog = StratagemCatalog.Parse(builtIn, user);

        var text = Encoding.UTF8.GetString(FirmwareBundle.LoadEmbedded(catalog).Files.Single(f => f.Target == "stratagems.json").Content);

        var lines = text.Split('\n').Select(l => l.Trim().TrimEnd(',')).ToList();
        lines.Should().Contain(l => l.StartsWith("\"categories\"", StringComparison.Ordinal));
        var entries = lines.Where(l => l.StartsWith("{\"id\":", StringComparison.Ordinal)).ToList();
        entries.Should().HaveCount(catalog.Entries.Count);
        entries.Should().Contain(l => l.Contains("\"my_flag\"") && l.Contains("\"UUDD\""));
        StratagemCatalog.Parse(new MemoryStream(Encoding.UTF8.GetBytes(text))).Entries.Should().HaveCount(catalog.Entries.Count);
    }

    [Fact]
    public async Task The_link_installs_the_firmware_and_connects_again()
    {
        var replBoard = new FakeMicroPythonBoard();
        var protocolBoard = new SimulatedHellboard();
        using var link = new HellboardLink(
            new HellboardOptions { Mode = HellboardConnectionMode.Serial },
            new MultiFunctionOptions(),
            Catalog,
            NullLogger<HellboardLink>.Instance,
            new FakeDiscovery("COM8"),
            _ => protocolBoard,
            new BoardOptions { Adopted = true },
            _ => replBoard);
        link.Start();
        await WaitFor(() => link.State.Status == HellboardStatus.Connected);
        var bundle = new FirmwareBundle("9.9.9", [new FirmwareFile("version.json", Encoding.UTF8.GetBytes("""{"version":"9.9.9"}""")), new FirmwareFile("main.py", Encoding.UTF8.GetBytes("import app"))]);
        var steps = new List<string>();

        await link.UpdateFirmwareAsync(bundle, new SynchronousProgress(steps));

        replBoard.Files.Keys.Should().Contain(["main.py", "version.json"]);
        replBoard.SoftResets.Should().Be(1);
        steps.Should().Contain(s => s.StartsWith("Writing main.py", StringComparison.Ordinal));
        await WaitFor(() => link.State.Status == HellboardStatus.Connected);
    }

    [Fact]
    public async Task Updating_without_a_board_fails()
    {
        using var link = new HellboardLink(new HellboardOptions { Mode = HellboardConnectionMode.Serial }, new MultiFunctionOptions(), Catalog, NullLogger<HellboardLink>.Instance, new FakeDiscovery());
        link.Start();

        var update = () => link.UpdateFirmwareAsync(FirmwareBundle.LoadEmbedded(Catalog));

        await update.Should().ThrowAsync<InvalidOperationException>();
    }

    private static async Task WaitFor(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Condition not met in time.");
            }

            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
    }

    private sealed class SynchronousProgress(List<string> steps) : IProgress<string>
    {
        public void Report(string value)
        {
            lock (steps)
            {
                steps.Add(value);
            }
        }
    }

    private sealed class FakeDiscovery(params string[] ports) : ISerialPortDiscovery
    {
        public IReadOnlyList<string> FindPorts(string usbVendorId) => ports;
    }
}
