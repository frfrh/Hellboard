// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security;
using HellboardCompanion.Core;
using HellboardCompanion.Core.Platform;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace HellboardCompanion.Platform.Windows;

/// <summary>
/// Windows toast notifications for the unpackaged app: the app id is registered per user under
/// <c>HKCU\Software\Classes\AppUserModelId</c> (display name in the notification center, no admin rights).
/// </summary>
public sealed class WindowsNotifications(ILogger<WindowsNotifications> logger) : INotifications
{
    private const string AppId = "FrFrh.HellboardCompanion";
    private const string RegistryKey = $@"Software\Classes\AppUserModelId\{AppId}";

    public bool Show(string title, string body)
    {
        try
        {
            using (var key = Registry.CurrentUser.CreateSubKey(RegistryKey))
            {
                key.SetValue("DisplayName", AppInfo.ProductName);
            }

            var xml = new XmlDocument();
            xml.LoadXml($"""
                <toast>
                  <visual>
                    <binding template="ToastGeneric">
                      <text>{SecurityElement.Escape(title)}</text>
                      <text>{SecurityElement.Escape(body)}</text>
                    </binding>
                  </visual>
                </toast>
                """);
            ToastNotificationManager.CreateToastNotifier(AppId).Show(new ToastNotification(xml));
            return true;
        }
        catch (Exception ex)
        {
            // WinRT reports failures as various COM exceptions; the caller falls back to its own window.
            logger.LogWarning(ex, "Windows notification could not be shown");
            return false;
        }
    }
}
