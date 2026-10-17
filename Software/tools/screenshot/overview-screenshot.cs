// SPDX-License-Identifier: GPL-3.0-or-later
//
// Renders the overview of Hellboard Companion with simulated data (game running, Hellboard connected,
// loadout locked) into a PNG for the README. Uses Avalonia's headless platform with Skia, so it
// draws the real main window without a screen. The stratagem icons are downloaded into a temporary
// folder like the app does at runtime.
//
// Usage (from Software/):  dotnet run tools/screenshot/overview-screenshot.cs -- <output.png>

#:project ../../src/HellboardCompanion.App/HellboardCompanion.App.csproj
#:property ManagePackageVersionsCentrally=false
#:property PublishAot=false
#:property JsonSerializerIsReflectionEnabledByDefault=true
#:package Avalonia.Headless@12.1.3
#:package Avalonia.Skia@12.1.3

using Avalonia;
using Avalonia.Headless;
using Avalonia.Media;
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

var good = new SolidColorBrush(Color.Parse("#4CAF50"));
var viewModel = MainWindowViewModel.DesignInstance;
var overview = viewModel.Overview;
overview.GameText = "Helldivers 2 runs on DISPLAY1 (2560×1440)";
overview.GameBrush = good;
overview.RecognitionText = "Loadout confirmed (Ready)";
overview.RecognitionBrush = good;
overview.HellboardText = "Hellboard connected (COM8)";
overview.HellboardBrush = good;
overview.LoadoutTitle = "Locked in at 21:14:07 (Ready)";
overview.Warning = null;
string[] loadout = ["eagle_500kg_bomb", "grenade_launcher", "supply_pack", "gas_mortar_sentry"];
for (var i = 0; i < loadout.Length; i++)
{
    overview.Stratagems[i] = Tile(loadout[i]);
}

overview.MultiFunction.Clear();
foreach (var id in new[] { "seaf_artillery", "eagle_rearm", "hellbomb" })
{
    overview.MultiFunction.Add(Tile(id));
}

overview.MultiFunctionTitle = "Multi-function button";
viewModel.Hellboard.DefaultResult = null;
viewModel.Hellboard.CanSetDefault = true;   // a connected board enables it

var window = new MainWindow { DataContext = viewModel, Width = 1200, Height = 560 };
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
