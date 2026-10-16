// SPDX-License-Identifier: GPL-3.0-or-later

namespace HellboardCompanion.Core.Hellboard.Update;

/// <summary>Byte stream to the MicroPython REPL of the Hellboard (USB CDC serial).</summary>
public interface IReplPort : IDisposable
{
    void Write(ReadOnlySpan<byte> data);

    /// <summary>Reads available bytes; waits at most <paramref name="timeout"/>. Returns 0 if nothing arrived.</summary>
    int Read(Span<byte> buffer, TimeSpan timeout);
}
