// SPDX-License-Identifier: GPL-3.0-or-later

using CommunityToolkit.Mvvm.ComponentModel;
using HellboardCompanion.Core;
using HellboardCompanion.Core.Platform;

namespace HellboardCompanion.App.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    public MainWindowViewModel(IPlatformInfo platform, IPlatformPaths paths)
    {
        OperatingSystem = platform.OperatingSystem;
        SupportNote = platform.SupportNote;
        ConfigDirectory = paths.ConfigDirectory;
        LogDirectory = paths.LogDirectory;
    }

    public string Title => $"{AppInfo.ProductName} {AppInfo.Version}";

    public string OperatingSystem { get; }

    public string? SupportNote { get; }

    public bool HasSupportNote => SupportNote is not null;

    public string ConfigDirectory { get; }

    public string LogDirectory { get; }

    /// <summary>Instance with placeholder values for the XAML previewer.</summary>
    public static MainWindowViewModel DesignInstance =>
        new(new DesignPlatformInfo(), new PlatformPaths(@"C:\Users\Design\AppData\Roaming", @"C:\Users\Design\AppData\Local"));

    private sealed class DesignPlatformInfo : IPlatformInfo
    {
        public string OperatingSystem => "Windows 11 (build 26200)";

        public PlatformSupportLevel SupportLevel => PlatformSupportLevel.Full;

        public string? SupportNote => null;
    }
}
