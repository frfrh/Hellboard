// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;

namespace HellboardCompanion.Core.Hellboard.Update;

/// <summary>Python code on the board raised an exception.</summary>
public sealed class MicroPythonException(string traceback) : Exception($"MicroPython error: {traceback.Trim()}")
{
    public string Traceback { get; } = traceback;
}

/// <summary>
/// MicroPython raw REPL (as used by mpremote / MicroPico): stop the running program with Ctrl-C,
/// enter raw mode with Ctrl-A, send code + Ctrl-D, read "OK", stdout, Ctrl-D, stderr, Ctrl-D, "&gt;".
/// Globals persist between executions until the soft reset (implementation plan §6.10).
/// </summary>
public sealed class MicroPythonRawRepl(IReplPort port, TimeSpan timeout, TimeSpan chunkDelay)
{
    private const byte CtrlA = 1;
    private const byte CtrlB = 2;
    private const byte CtrlC = 3;
    private const byte CtrlD = 4;
    private const int ChunkSize = 256;
    private const string RawPrompt = "raw REPL; CTRL-B to exit\r\n>";

    private readonly List<byte> _pending = [];

    /// <summary>
    /// 30 s answer timeout: renaming files can take several seconds when LittleFS compacts its
    /// metadata (observed 2026-10-16); a too short timeout lets a late answer mix with the next one.
    /// </summary>
    public MicroPythonRawRepl(IReplPort port)
        : this(port, TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(10))
    {
    }

    /// <summary>Interrupts the running program (F8 Ctrl-C fallback) and switches to the raw REPL.</summary>
    public void Enter()
    {
        port.Write([(byte)'\r', CtrlC, CtrlC]);
        Drain(TimeSpan.FromMilliseconds(300));
        port.Write([(byte)'\r', CtrlA]);
        ReadUntil(RawPrompt);
    }

    /// <summary>Runs <paramref name="code"/> and returns what it printed.</summary>
    /// <exception cref="MicroPythonException">The code raised an exception.</exception>
    public string Execute(string code)
    {
        var bytes = Encoding.UTF8.GetBytes(code);
        for (var i = 0; i < bytes.Length; i += ChunkSize)
        {
            port.Write(bytes.AsSpan(i, Math.Min(ChunkSize, bytes.Length - i)));
            if (chunkDelay > TimeSpan.Zero)
            {
                Thread.Sleep(chunkDelay);
            }
        }

        port.Write([CtrlD]);
        var accepted = ReadUntil("OK");
        if (accepted.Length > 0)
        {
            throw new IOException($"Raw REPL did not accept the code: {accepted}");
        }

        var output = ReadUntil("\u0004");
        var error = ReadUntil("\u0004");
        ReadUntil(">");
        return error.Length > 0 ? throw new MicroPythonException(error) : output;
    }

    /// <summary>Leaves the raw REPL and soft-resets the board, so <c>main.py</c> starts again.</summary>
    public void ExitAndSoftReset()
    {
        port.Write([CtrlB]);
        Drain(TimeSpan.FromMilliseconds(200));
        port.Write([CtrlD]);
    }

    /// <summary>Returns the text received before <paramref name="marker"/> (the marker itself is consumed).</summary>
    private string ReadUntil(string marker)
    {
        var markerBytes = Encoding.UTF8.GetBytes(marker);
        var deadline = DateTime.UtcNow + timeout;
        var buffer = new byte[512];
        while (true)
        {
            var index = IndexOf(_pending, markerBytes);
            if (index >= 0)
            {
                var text = Encoding.UTF8.GetString(_pending.GetRange(0, index).ToArray());
                _pending.RemoveRange(0, index + markerBytes.Length);
                return text;
            }

            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                throw new TimeoutException($"No answer from the board (waiting for '{Printable(marker)}', received '{Printable(Encoding.UTF8.GetString([.. _pending]))}').");
            }

            var read = port.Read(buffer, remaining < TimeSpan.FromMilliseconds(100) ? remaining : TimeSpan.FromMilliseconds(100));
            _pending.AddRange(buffer.AsSpan(0, read));
        }
    }

    private void Drain(TimeSpan duration)
    {
        var buffer = new byte[512];
        var deadline = DateTime.UtcNow + duration;
        while (DateTime.UtcNow < deadline)
        {
            port.Read(buffer, TimeSpan.FromMilliseconds(50));
        }

        _pending.Clear();
    }

    private static int IndexOf(List<byte> data, byte[] pattern)
    {
        for (var i = 0; i <= data.Count - pattern.Length; i++)
        {
            var match = true;
            for (var j = 0; j < pattern.Length && match; j++)
            {
                match = data[i + j] == pattern[j];
            }

            if (match)
            {
                return i;
            }
        }

        return -1;
    }

    private static string Printable(string text) =>
        string.Concat(text.Select(c => c < ' ' && c is not '\r' and not '\n' ? $"^{(char)(c + 64)}" : c.ToString()));
}
