// SPDX-License-Identifier: GPL-3.0-or-later

using System.IO.Ports;

namespace HellboardCompanion.Core.Hellboard.Update;

/// <summary>REPL access over the Hellboard's USB CDC serial port.</summary>
public sealed class SerialReplPort : IReplPort
{
    private readonly SerialPort _port;

    public SerialReplPort(string portName)
    {
        _port = new SerialPort(portName, 115200)
        {
            DtrEnable = true,   // MicroPython's USB CDC only sends while DTR is set
            RtsEnable = true,
            WriteTimeout = 3000,
        };
        _port.Open();
    }

    public void Write(ReadOnlySpan<byte> data) => _port.BaseStream.Write(data);

    public int Read(Span<byte> buffer, TimeSpan timeout)
    {
        _port.ReadTimeout = Math.Max(1, (int)timeout.TotalMilliseconds);
        try
        {
            return _port.BaseStream.Read(buffer);
        }
        catch (TimeoutException)
        {
            return 0;
        }
    }

    public void Dispose()
    {
        try
        {
            _port.Close();
        }
        catch (IOException)
        {
            // already gone (unplugged)
        }

        _port.Dispose();
    }
}
