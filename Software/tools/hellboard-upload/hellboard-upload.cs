// SPDX-License-Identifier: GPL-3.0-or-later
//
// Installs the Hellboard firmware files listed in Firmware/manifest.json onto the board through the
// raw REPL: write as *.new, verify SHA-256 on the board, then swap (old files kept as *.bak), soft reset.
// Development tool of phase P7; the same Core classes become the firmware updater of the Companion (§6.10).
//
// Usage (from Software/):  dotnet run tools/hellboard-upload/hellboard-upload.cs -- [--port COM8] [--list]
// Close VS Code / MicroPico and the Companion first, so the COM port is free.

#:project ../../src/HellboardCompanion.Platform.Windows/HellboardCompanion.Platform.Windows.csproj
#:property TargetFramework=net10.0-windows10.0.19041.0
#:property PublishAot=false

using HellboardCompanion.Core.Hellboard.Update;
using HellboardCompanion.Platform.Windows;

var port = Option("--port");
var manifest = Option("--manifest") ?? Path.GetFullPath(Path.Combine(AppContext.GetData("EntryPointFileDirectoryPath") as string ?? ".", "../../../Firmware/manifest.json"));

var bundle = FirmwareBundle.Load(manifest);
Console.WriteLine($"Firmware bundle: {manifest}");
foreach (var file in bundle.Files)
{
    Console.WriteLine($"  {file.Target,-20} {file.Content.Length,7} bytes  sha256 {file.Sha256[..12]}…");
}

if (args.Contains("--list"))
{
    return 0;
}

if (port is null)
{
    var ports = new RegistrySerialPortDiscovery().FindPorts("2E8A");
    if (ports.Count != 1)
    {
        Console.Error.WriteLine(ports.Count == 0
            ? "No RP2040 serial port (USB VID 2E8A) found. Is the Hellboard connected?"
            : $"Several RP2040 ports found ({string.Join(", ", ports)}); choose one with --port.");
        return 2;
    }

    port = ports[0];
}

Console.WriteLine($"Hellboard on {port}");
try
{
    using var serial = new SerialReplPort(port);
    var repl = new MicroPythonRawRepl(serial);
    repl.Enter();
    try
    {
        new FirmwareInstaller(repl).Install(bundle, new Progress<string>(m => Console.WriteLine($"  {m}")));
    }
    finally
    {
        repl.ExitAndSoftReset();
    }

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("Done – the board restarts with the new files.");
    Console.ResetColor();
    return 0;
}
catch (Exception ex) when (ex is FirmwareInstallException or IOException or TimeoutException or UnauthorizedAccessException or MicroPythonException)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine($"Upload failed: {ex.Message}");
    if (ex is UnauthorizedAccessException)
    {
        Console.Error.WriteLine("The port is in use – close MicroPico and the Companion.");
    }

    Console.ResetColor();
    return 1;
}

string? Option(string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}
