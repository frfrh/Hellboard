// SPDX-License-Identifier: GPL-3.0-or-later

using System.IO.Ports;
using HellboardCompanion.Core.Platform;
using Microsoft.Win32;

namespace HellboardCompanion.Platform.Windows;

/// <summary>
/// Finds the COM ports of USB devices by vendor id via the device registry
/// (<c>HKLM\SYSTEM\CurrentControlSet\Enum\USB\VID_xxxx&amp;PID_yyyy…\Device Parameters\PortName</c>),
/// restricted to the ports that are currently present.
/// </summary>
public sealed class RegistrySerialPortDiscovery : ISerialPortDiscovery
{
    public IReadOnlyList<string> FindPorts(string usbVendorId)
    {
        var present = new HashSet<string>(SerialPort.GetPortNames(), StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        using var usb = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\USB");
        if (usb is null)
        {
            return result;
        }

        foreach (var deviceName in usb.GetSubKeyNames().Where(n => n.StartsWith($"VID_{usbVendorId}", StringComparison.OrdinalIgnoreCase)))
        {
            using var device = usb.OpenSubKey(deviceName);
            if (device is null)
            {
                continue;
            }

            foreach (var instanceName in device.GetSubKeyNames())
            {
                using var parameters = device.OpenSubKey($@"{instanceName}\Device Parameters");
                if (parameters?.GetValue("PortName") is string port && present.Contains(port) && !result.Contains(port))
                {
                    result.Add(port);
                }
            }
        }

        return result;
    }
}
