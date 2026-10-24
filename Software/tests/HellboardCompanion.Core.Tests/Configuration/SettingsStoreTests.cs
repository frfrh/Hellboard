// SPDX-License-Identifier: GPL-3.0-or-later

using AwesomeAssertions;
using HellboardCompanion.Core.Configuration;
using HellboardCompanion.Core.Hellboard;
using HellboardCompanion.Core.Pipeline;
using HellboardCompanion.Core.Platform;

namespace HellboardCompanion.Core.Tests.Configuration;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "HellboardCompanionTests", Guid.NewGuid().ToString("N"));
    private readonly PlatformPaths _paths;

    public SettingsStoreTests() => _paths = new PlatformPaths(Path.Combine(_root, "roaming"), Path.Combine(_root, "local"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void First_start_writes_the_defaults()
    {
        var store = SettingsStore.Load(_paths);

        File.Exists(store.FilePath).Should().BeTrue();
        store.Settings.Recognition.TriggerHotkey.Should().Be("Ctrl+Shift+F9");
        store.Settings.MultiFunction.FixedList.Should().Equal("sos_beacon");
        store.LoadProblem.Should().BeNull();
    }

    [Fact]
    public void Changes_survive_a_restart_and_enums_are_readable_text()
    {
        var store = SettingsStore.Load(_paths);
        store.Settings.Hellboard.Mode = HellboardConnectionMode.Simulator;
        store.Settings.Recognition.Mode = RecognitionMode.ManualOnly;
        store.Settings.MultiFunction.FixedList = ["sos_beacon", "eagle_rearm"];
        store.Save();

        var reloaded = SettingsStore.Load(_paths);

        reloaded.Settings.Hellboard.Mode.Should().Be(HellboardConnectionMode.Simulator);
        reloaded.Settings.Recognition.Mode.Should().Be(RecognitionMode.ManualOnly);
        reloaded.Settings.MultiFunction.FixedList.Should().Equal("sos_beacon", "eagle_rearm");
        File.ReadAllText(store.FilePath).Should().Contain("\"Simulator\"").And.Contain("\"ManualOnly\"").And.Contain("Ctrl+Shift+F9");
    }

    [Fact]
    public void The_window_opens_on_start_and_the_close_button_keeps_the_tray_icon_by_default()
    {
        Directory.CreateDirectory(_paths.ConfigDirectory);
        File.WriteAllText(Path.Combine(_paths.ConfigDirectory, "config.json"), """{ "general": { "startWithWindows": true } }""");   // settings of 0.6.x

        var store = SettingsStore.Load(_paths);

        store.Settings.General.StartMinimized.Should().BeFalse();
        store.Settings.General.CloseAction.Should().Be(CloseAction.KeepRunningInTray);
    }

    [Fact]
    public void Start_and_close_behavior_survive_a_restart()
    {
        var store = SettingsStore.Load(_paths);
        store.Settings.General.StartMinimized = true;
        store.Settings.General.CloseAction = CloseAction.Exit;
        store.Save();

        var reloaded = SettingsStore.Load(_paths);

        reloaded.Settings.General.StartMinimized.Should().BeTrue();
        reloaded.Settings.General.CloseAction.Should().Be(CloseAction.Exit);
        File.ReadAllText(store.FilePath).Should().Contain("\"Exit\"");
    }

    [Fact]
    public void Missing_values_get_their_defaults()
    {
        Directory.CreateDirectory(_paths.ConfigDirectory);
        File.WriteAllText(Path.Combine(_paths.ConfigDirectory, "config.json"), """{ "hellboard": { "mode": "DryRun" } }""");

        var store = SettingsStore.Load(_paths);

        store.Settings.Hellboard.Mode.Should().Be(HellboardConnectionMode.DryRun);
        store.Settings.Recognition.TriggerHotkey.Should().Be("Ctrl+Shift+F9");
    }

    [Fact]
    public void Broken_file_is_kept_and_replaced_by_defaults()
    {
        Directory.CreateDirectory(_paths.ConfigDirectory);
        var file = Path.Combine(_paths.ConfigDirectory, "config.json");
        File.WriteAllText(file, "{ this is not json");

        var store = SettingsStore.Load(_paths);

        store.LoadProblem.Should().Contain("config.json.invalid");
        File.ReadAllText(file + ".invalid").Should().Be("{ this is not json");
        store.Settings.Hellboard.Mode.Should().Be(HellboardConnectionMode.Serial);
    }

    [Fact]
    public void Saving_notifies_the_services()
    {
        var store = SettingsStore.Load(_paths);
        var notified = 0;
        store.Saved += (_, _) => notified++;

        store.Save();

        notified.Should().Be(1);
    }

    [Fact]
    public void Window_placement_is_saved_quietly_and_survives_a_restart()
    {
        var store = SettingsStore.Load(_paths);
        var notified = 0;
        store.Saved += (_, _) => notified++;
        store.Settings.Ui.MainWindow.Should().BeNull();

        store.Settings.Ui.MainWindow = new WindowPlacement(-1700, 120, 1280.5, 860, true);
        store.SaveQuietly();

        notified.Should().Be(0);
        SettingsStore.Load(_paths).Settings.Ui.MainWindow.Should().Be(new WindowPlacement(-1700, 120, 1280.5, 860, true));
    }

    [Theory]
    [InlineData(100, 100, true)]
    [InlineData(-1700, 120, true)]        // on the monitor left of the primary one
    [InlineData(1850, 500, true)]         // mostly outside on the right, title bar still half on the screen
    [InlineData(-3000, 100, false)]       // that monitor is gone
    [InlineData(100, -200, false)]        // title bar above every screen
    [InlineData(-32000, -32000, false)]   // minimized position of Windows
    public void Window_position_is_used_again_only_when_its_title_bar_is_on_a_screen(int x, int y, bool reachable)
    {
        PixelRect[] screens = [new(0, 0, 1920, 1040), new(-1920, 0, 1920, 1040)];

        new WindowPlacement(x, y, 1200, 900, false).IsReachable(screens).Should().Be(reachable);
    }
}
