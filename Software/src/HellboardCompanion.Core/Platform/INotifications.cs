// SPDX-License-Identifier: GPL-3.0-or-later

namespace HellboardCompanion.Core.Platform;

/// <summary>Notifications of the operating system (Windows: toast in the notification center).</summary>
public interface INotifications
{
    /// <summary>Shows a notification.</summary>
    /// <returns><c>false</c> if the system did not accept it (the caller can fall back to its own window).</returns>
    bool Show(string title, string body);
}
