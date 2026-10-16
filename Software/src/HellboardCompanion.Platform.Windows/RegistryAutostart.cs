// SPDX-License-Identifier: GPL-3.0-or-later

using HellboardCompanion.Core;
using HellboardCompanion.Core.Platform;
using Microsoft.Win32;

namespace HellboardCompanion.Platform.Windows;

/// <summary>Autostart via <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c> (per user, no admin rights).</summary>
public sealed class RegistryAutostart : IAutostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private static string Command => $"\"{Environment.ProcessPath}\"";

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(AppInfo.ProductName) is string value && value == Command;
        }
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
        {
            key.SetValue(AppInfo.ProductName, Command);   // also updates the path after the EXE was moved
        }
        else
        {
            key.DeleteValue(AppInfo.ProductName, throwOnMissingValue: false);
        }
    }
}
