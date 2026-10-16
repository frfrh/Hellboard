// SPDX-License-Identifier: GPL-3.0-or-later

namespace HellboardCompanion.Core.Hellboard.Update;

/// <summary>
/// Installs a <see cref="FirmwareBundle"/> through the raw REPL (implementation plan §6.10):
/// every file is written as <c>name.new</c> and verified by its SHA-256 on the board; only when all
/// files are verified, each old file becomes <c>name.bak</c> and the new one takes its place. If
/// anything fails, the <c>.new</c> files are removed and already replaced files are restored.
/// </summary>
public sealed class FirmwareInstaller(MicroPythonRawRepl repl)
{
    /// <summary>Raw bytes per write command (1 KB base64 per line).</summary>
    private const int WriteChunkSize = 768;

    private const string Helpers = """
        import os, hashlib, binascii
        def _sha(n):
            h = hashlib.sha256()
            with open(n, 'rb') as f:
                while True:
                    b = f.read(512)
                    if not b:
                        break
                    h.update(b)
            print(binascii.hexlify(h.digest()).decode())
        def _rm(n):
            try:
                os.remove(n)
            except OSError:
                pass
        def _swap(n):
            _rm(n + '.bak')
            try:
                os.rename(n, n + '.bak')
            except OSError:
                pass
            os.rename(n + '.new', n)
        def _restore(n):
            try:
                os.stat(n + '.bak')
            except OSError:
                return
            _rm(n)
            os.rename(n + '.bak', n)
        """;

    /// <summary>
    /// Writes, verifies and activates all files. The caller enters the raw REPL before and resets the
    /// board afterwards.
    /// </summary>
    /// <exception cref="FirmwareInstallException">The installation failed; the board keeps (or got back) its old files.</exception>
    public void Install(FirmwareBundle bundle, IProgress<string>? progress = null)
    {
        foreach (var file in bundle.Files)
        {
            if (!FirmwareBundle.TargetPattern().IsMatch(file.Target))
            {
                throw new ArgumentException($"Target name '{file.Target}' is not allowed.", nameof(bundle));
            }
        }

        repl.Execute(Helpers);
        try
        {
            foreach (var file in bundle.Files)
            {
                progress?.Report($"Writing {file.Target} ({file.Content.Length} bytes)");
                Write(file);
                var actual = repl.Execute($"_sha('{file.Target}.new')").Trim();
                if (!string.Equals(actual, file.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new FirmwareInstallException($"{file.Target}: checksum on the board does not match (transfer error).");
                }
            }
        }
        catch (Exception ex) when (ex is not FirmwareInstallException)
        {
            RemoveNewFiles(bundle);
            throw new FirmwareInstallException($"Transfer failed: {ex.Message}", ex);
        }
        catch (FirmwareInstallException)
        {
            RemoveNewFiles(bundle);
            throw;
        }

        var swapped = new List<FirmwareFile>();
        try
        {
            foreach (var file in bundle.Files)
            {
                repl.Execute($"_swap('{file.Target}')");
                swapped.Add(file);
            }

            progress?.Report($"Installed {bundle.Files.Count} files");
        }
        catch (Exception ex)
        {
            foreach (var file in swapped)
            {
                TryExecute($"_restore('{file.Target}')");
            }

            RemoveNewFiles(bundle);
            throw new FirmwareInstallException($"Activating the new files failed, old files restored: {ex.Message}", ex);
        }

        // Leftovers of older firmware versions; best effort, the new firmware does not use them.
        foreach (var name in bundle.Obsolete)
        {
            TryExecute($"_rm('{name}')");
            TryExecute($"_rm('{name}.bak')");
        }
    }

    private void Write(FirmwareFile file)
    {
        repl.Execute($"f = open('{file.Target}.new', 'wb')");
        for (var i = 0; i < file.Content.Length; i += WriteChunkSize)
        {
            var chunk = Convert.ToBase64String(file.Content, i, Math.Min(WriteChunkSize, file.Content.Length - i));
            repl.Execute($"f.write(binascii.a2b_base64('{chunk}'))");
        }

        repl.Execute("f.close()");
    }

    private void RemoveNewFiles(FirmwareBundle bundle)
    {
        TryExecute("try:\n    f.close()\nexcept Exception:\n    pass");
        foreach (var file in bundle.Files)
        {
            TryExecute($"_rm('{file.Target}.new')");
        }
    }

    private void TryExecute(string code)
    {
        try
        {
            repl.Execute(code);
        }
        catch (Exception ex) when (ex is MicroPythonException or IOException or TimeoutException)
        {
            // best effort while cleaning up
        }
    }
}

public sealed class FirmwareInstallException(string message, Exception? inner = null) : Exception(message, inner);
