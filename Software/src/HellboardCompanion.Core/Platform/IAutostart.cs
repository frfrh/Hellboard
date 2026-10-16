// SPDX-License-Identifier: GPL-3.0-or-later

namespace HellboardCompanion.Core.Platform;

/// <summary>Starts the application when the user signs in (implementation plan §6.7).</summary>
public interface IAutostart
{
    bool IsEnabled { get; }

    void SetEnabled(bool enabled);
}
