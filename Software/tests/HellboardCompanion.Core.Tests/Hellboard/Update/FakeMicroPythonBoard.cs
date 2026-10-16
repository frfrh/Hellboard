// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HellboardCompanion.Core.Hellboard.Update;

namespace HellboardCompanion.Core.Tests.Hellboard.Update;

/// <summary>
/// Simulates the raw REPL of the Hellboard and the file operations the <see cref="FirmwareInstaller"/>
/// generates (open/write/close, _sha, _swap, _restore, _rm). Unknown code runs without output.
/// </summary>
internal sealed partial class FakeMicroPythonBoard : IReplPort
{
    private readonly Queue<byte> _output = new();
    private readonly List<byte> _code = [];
    private bool _raw;
    private string? _openFile;

    public Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);

    /// <summary>Flips a byte of this file while it is written (simulated transfer error).</summary>
    public string? CorruptWritesTo { get; set; }

    /// <summary>Raises an OSError when this file is activated.</summary>
    public string? FailSwapOf { get; set; }

    public int SoftResets { get; private set; }

    public List<string> Executed { get; } = [];

    public void Write(ReadOnlySpan<byte> data)
    {
        foreach (var b in data)
        {
            if (!_raw)
            {
                switch (b)
                {
                    case 1:
                        _raw = true;
                        Send("raw REPL; CTRL-B to exit\r\n>");
                        break;
                    case 4:
                        SoftResets++;
                        break;
                }

                continue;
            }

            switch (b)
            {
                case 2:
                    _raw = false;
                    _code.Clear();
                    break;
                case 4:
                    var code = Encoding.UTF8.GetString([.. _code]);
                    _code.Clear();
                    Executed.Add(code);
                    Send("OK");
                    try
                    {
                        Send(Run(code));
                        Send("\u0004\u0004>");
                    }
                    catch (InvalidOperationException ex)
                    {
                        Send("\u0004Traceback (most recent call last):\r\nOSError: " + ex.Message + "\r\n\u0004>");
                    }

                    break;
                default:
                    _code.Add(b);
                    break;
            }
        }
    }

    public int Read(Span<byte> buffer, TimeSpan timeout)
    {
        var count = 0;
        while (count < buffer.Length && _output.Count > 0)
        {
            buffer[count++] = _output.Dequeue();
        }

        return count;
    }

    public void Dispose()
    {
    }

    private string Run(string code)
    {
        if (OpenPattern().Match(code) is { Success: true } open)
        {
            _openFile = open.Groups[1].Value;
            Files[_openFile] = [];
        }
        else if (WritePattern().Match(code) is { Success: true } write)
        {
            var data = Convert.FromBase64String(write.Groups[1].Value);
            if (_openFile == CorruptWritesTo + ".new" && data.Length > 0)
            {
                data[0] ^= 0xFF;
            }

            Files[_openFile!] = [.. Files[_openFile!], .. data];
        }
        else if (CallPattern().Match(code) is { Success: true } call)
        {
            var name = call.Groups[2].Value;
            switch (call.Groups[1].Value)
            {
                case "_sha":
                    return Convert.ToHexStringLower(SHA256.HashData(Files[name])) + "\r\n";
                case "_rm":
                    Files.Remove(name);
                    break;
                case "_swap":
                    if (name == FailSwapOf)
                    {
                        throw new InvalidOperationException("[Errno 28] ENOSPC");
                    }

                    Files.Remove(name + ".bak");
                    if (Files.Remove(name, out var old))
                    {
                        Files[name + ".bak"] = old;
                    }

                    Files[name] = Files[name + ".new"];
                    Files.Remove(name + ".new");
                    break;
                case "_restore":
                    if (Files.Remove(name + ".bak", out var backup))
                    {
                        Files[name] = backup;
                    }

                    break;
            }
        }

        return string.Empty;
    }

    private void Send(string text)
    {
        foreach (var b in Encoding.UTF8.GetBytes(text))
        {
            _output.Enqueue(b);
        }
    }

    [GeneratedRegex(@"^f = open\('([^']+)', 'wb'\)$")]
    private static partial Regex OpenPattern();

    [GeneratedRegex(@"^f\.write\(binascii\.a2b_base64\('([^']*)'\)\)$")]
    private static partial Regex WritePattern();

    [GeneratedRegex(@"^(_sha|_rm|_swap|_restore)\('([^']+)'\)$")]
    private static partial Regex CallPattern();
}
