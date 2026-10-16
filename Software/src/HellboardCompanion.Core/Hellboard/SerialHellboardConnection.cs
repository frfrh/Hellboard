// SPDX-License-Identifier: GPL-3.0-or-later

using System.IO.Ports;
using System.Text;

namespace HellboardCompanion.Core.Hellboard;

/// <summary>
/// USB CDC serial connection to the Hellboard (hellboard-protocol.md §1). Platform-neutral
/// (<c>System.IO.Ports</c> works on Windows and Linux); finding the port is platform-specific.
/// </summary>
public sealed class SerialHellboardConnection : IHellboardConnection
{
    private readonly SerialPort _port;
    private readonly StringBuilder _buffer = new();
    private readonly Lock _lock = new();
    private int _closed;

    public SerialHellboardConnection(string portName)
    {
        Description = portName;
        _port = new SerialPort(portName, 115200)
        {
            DtrEnable = true,   // MicroPython's USB CDC only sends while DTR is set
            RtsEnable = true,
            Encoding = new UTF8Encoding(false),
            NewLine = "\n",
            WriteTimeout = 3000,   // protocol §1: the firmware services stdin at least every 100 ms
        };
        _port.DataReceived += OnDataReceived;
        _port.ErrorReceived += (_, _) => RaiseClosed();
        _port.Open();
    }

    public string Description { get; }

    public event EventHandler<string>? LineReceived;

    public event EventHandler? Closed;

    public void WriteLine(string line)
    {
        try
        {
            _port.Write(line + "\n");
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or TimeoutException)
        {
            RaiseClosed();
            throw new IOException($"Writing to {Description} failed.", ex);
        }
    }

    public void Dispose()
    {
        _port.DataReceived -= OnDataReceived;
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

    private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        string text;
        try
        {
            text = _port.ReadExisting();
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            RaiseClosed();
            return;
        }

        List<string> lines = [];
        lock (_lock)
        {
            _buffer.Append(text);
            var content = _buffer.ToString();
            int newline;
            while ((newline = content.IndexOf('\n')) >= 0)
            {
                lines.Add(content[..newline].TrimEnd('\r'));
                content = content[(newline + 1)..];
            }

            _buffer.Clear().Append(content.Length > 8192 ? content[^8192..] : content);
        }

        foreach (var line in lines)
        {
            LineReceived?.Invoke(this, line);
        }
    }

    private void RaiseClosed()
    {
        if (Interlocked.Exchange(ref _closed, 1) == 0)
        {
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }
}
