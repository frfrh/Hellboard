// SPDX-License-Identifier: GPL-3.0-or-later
//
// Renders the overview of Hellboard Companion with simulated data (game running, Hellboard connected,
// loadout locked) into a PNG for the README. Uses Avalonia's headless platform with Skia, so it
// draws the real main window without a screen. The stratagem icons are downloaded into a temporary
// folder like the app does at runtime.
//
// Usage (from Software/):  dotnet run tools/screenshot/overview-screenshot.cs -- <output.png> [width] [long]
// width: window width (default 1200; the window's minimum is 900). "long": long status details and a
// long multi-function list, to check wrapping (UI polish plan §5).

#:project ../../src/HellboardCompanion.App/HellboardCompanion.App.csproj
#:property ManagePackageVersionsCentrally=false
#:property PublishAot=false
#:property JsonSerializerIsReflectionEnabledByDefault=true
#:package Avalonia.Headless@12.1.3
#:package Avalonia.Skia@12.1.3

using Avalonia;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using HellboardCompanion.App.Services;
using HellboardCompanion.App.ViewModels;
using HellboardCompanion.App.Views;
using HellboardCompanion.Core.Catalog;
using HellboardCompanion.Core.Hellboard;
using HellboardCompanion.Core.Platform;
using Microsoft.Extensions.Logging.Abstractions;

var output = Path.GetFullPath(args.Length > 0 ? args[0] : "overview.png");
var width = args.Length > 1 ? int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture) : 1200;
var longTexts = args.Length > 2 && args[2] == "long";

// Before Avalonia starts: its dispatcher would become the synchronization context and the download would wait forever.
var data = Path.Combine(Path.GetTempPath(), "hellboard-screenshot");
var icons = new StratagemIconStore(new PlatformPaths(data, data), NullLogger<StratagemIconStore>.Instance);
icons.UpdateAsync().GetAwaiter().GetResult();

AppBuilder.Configure<ScreenshotApp>()
    .UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .SetupWithoutStarting();

var catalog = StratagemCatalog.LoadSeed();
var images = new StratagemImages(icons, catalog);
StratagemTileViewModel Tile(string id) => new(id, catalog.Find(id)!.Name, images.Get(id));

var viewModel = MainWindowViewModel.DesignInstance;
var overview = viewModel.Overview;
overview.GameLine = new("Helldivers 2 running", "DISPLAY1 · 2560×1440", StatusLevel.Good);
overview.IsGameRunning = true;
overview.CanRecognize = true;
overview.RecognitionLine = new("Loadout confirmed", "Ready", StatusLevel.Good);
overview.HellboardLine = longTexts
    ? new("Firmware outdated", "COM8 · 2.0.0 → 2.1.1, update on the Hellboard tab", StatusLevel.Attention)
    : new("Hellboard connected", "COM8", StatusLevel.Good);
overview.LoadoutTitle = "Locked in at 21:14:07 (Ready)";
overview.Warning = null;
string[] loadout = ["eagle_500kg_bomb", "grenade_launcher", "supply_pack", "gas_mortar_sentry"];
for (var i = 0; i < loadout.Length; i++)
{
    overview.Stratagems[i] = Tile(loadout[i]);
}

// Default settings: fixed list "SOS Beacon", the rest detected on the loadout screen.
string[] multiFunction = longTexts
    ? ["sos_beacon", "seaf_artillery", "eagle_rearm", "hellbomb", "super_earth_flag", "upload_data", "seismic_probe", "orbital_illumination_flare", "dark_fluid_vessel"]
    : ["sos_beacon", "seaf_artillery", "eagle_rearm", "hellbomb"];
overview.SetMultiFunction(multiFunction.Select(Tile).ToList());
viewModel.Hellboard.DefaultResult = null;
viewModel.Hellboard.CanSetDefault = true;   // a connected board enables it

var window = new MainWindow { DataContext = viewModel, Width = width, Height = longTexts ? 780 : 600 };
window.Show();
Dispatcher.UIThread.RunJobs();
using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("Nothing rendered.");
frame.Save(output, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
Console.WriteLine($"Screenshot written to {output}");

sealed class ScreenshotApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        RequestedThemeVariant = ThemeVariant.Dark;
    }
}
