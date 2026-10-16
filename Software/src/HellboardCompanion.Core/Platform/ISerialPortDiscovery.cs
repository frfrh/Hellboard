// SPDX-License-Identifier: GPL-3.0-or-later

namespace HellboardCompanion.Core.Platform;

/// <summary>Finds serial ports of connected USB devices (implementation plan §3.1).</summary>
public interface ISerialPortDiscovery
{
    /// <summary>Port names (e.g. "COM8", "/dev/ttyACM0") of present devices with the given USB vendor id, e.g. "2E8A".</summary>
    IReadOnlyList<string> FindPorts(string usbVendorId);
}
