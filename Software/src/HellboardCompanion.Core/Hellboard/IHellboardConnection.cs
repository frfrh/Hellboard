// SPDX-License-Identifier: GPL-3.0-or-later

namespace HellboardCompanion.Core.Hellboard;

/// <summary>Line-based connection to a Hellboard (serial port or simulator).</summary>
public interface IHellboardConnection : IDisposable
{
    /// <summary>E.g. "COM8" or "Simulator".</summary>
    string Description { get; }

    /// <summary>Raised on a background thread for every received line (without line break).</summary>
    event EventHandler<string>? LineReceived;

    /// <summary>Raised when the connection breaks (e.g. board unplugged).</summary>
    event EventHandler? Closed;

    void WriteLine(string line);
}
