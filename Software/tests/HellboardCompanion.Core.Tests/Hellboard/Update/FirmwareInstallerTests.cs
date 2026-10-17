// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;
using AwesomeAssertions;
using HellboardCompanion.Core.Hellboard.Update;

namespace HellboardCompanion.Core.Tests.Hellboard.Update;

public sealed class FirmwareInstallerTests
{
    private readonly FakeMicroPythonBoard _board = new();
    private readonly MicroPythonRawRepl _repl;

    public FirmwareInstallerTests()
    {
        _repl = new MicroPythonRawRepl(_board, TimeSpan.FromSeconds(1), TimeSpan.Zero);
        _board.Files["main.py"] = Bytes("old main");
        _board.Files["config.json"] = Bytes("user settings");
    }

    [Fact]
    public void Files_are_written_verified_and_activated_with_a_backup()
    {
        var large = new byte[5000];
        Random.Shared.NextBytes(large);
        var bundle = new FirmwareBundle("1.0.0", [new FirmwareFile("main.py", Bytes("new main")), new FirmwareFile("stratagems.json", large)]);

        _repl.Enter();
        new FirmwareInstaller(_repl).Install(bundle);

        _board.Files["main.py"].Should().Equal(Bytes("new main"));
        _board.Files["main.py.bak"].Should().Equal(Bytes("old main"));
        _board.Files["stratagems.json"].Should().Equal(large);
        _board.Files["config.json"].Should().Equal(Bytes("user settings"));   // not part of the bundle
        _board.Files.Keys.Should().NotContain(k => k.EndsWith(".new", StringComparison.Ordinal));
    }

    [Fact]
    public void Obsolete_files_of_older_firmware_are_removed()
    {
        _board.Files["macro.py"] = Bytes("old");
        _board.Files["macro.py.bak"] = Bytes("older");
        var bundle = new FirmwareBundle("1.0.0", [new FirmwareFile("main.py", Bytes("new main"))], ["macro.py"]);

        _repl.Enter();
        new FirmwareInstaller(_repl).Install(bundle);

        _board.Files.Keys.Should().NotContain(["macro.py", "macro.py.bak"]);
        _board.Files["main.py"].Should().Equal(Bytes("new main"));
    }

    [Fact]
    public void A_checksum_mismatch_leaves_the_old_files_untouched()
    {
        _board.CorruptWritesTo = "stratagems.json";
        var bundle = new FirmwareBundle("1.0.0", [new FirmwareFile("main.py", Bytes("new main")), new FirmwareFile("stratagems.json", Bytes("{}"))]);

        _repl.Enter();
        var install = () => new FirmwareInstaller(_repl).Install(bundle);

        install.Should().Throw<FirmwareInstallException>().WithMessage("*stratagems.json*checksum*");
        _board.Files.Keys.Should().BeEquivalentTo("main.py", "config.json");
        _board.Files["main.py"].Should().Equal(Bytes("old main"));
    }

    [Fact]
    public void A_failure_while_activating_restores_the_replaced_files()
    {
        _board.FailSwapOf = "stratagems.json";
        var bundle = new FirmwareBundle("1.0.0", [new FirmwareFile("main.py", Bytes("new main")), new FirmwareFile("stratagems.json", Bytes("{}"))]);

        _repl.Enter();
        var install = () => new FirmwareInstaller(_repl).Install(bundle);

        install.Should().Throw<FirmwareInstallException>().WithMessage("*restored*");
        _board.Files["main.py"].Should().Equal(Bytes("old main"));
        _board.Files.Keys.Should().NotContain(k => k.EndsWith(".new", StringComparison.Ordinal));
    }

    [Fact]
    public void Exit_soft_resets_the_board()
    {
        _repl.Enter();
        _repl.ExitAndSoftReset();

        _board.SoftResets.Should().Be(1);
    }

    [Fact]
    public void Python_errors_are_reported()
    {
        _board.FailSwapOf = "x";
        _repl.Enter();

        var run = () => _repl.Execute("_swap('x')");

        run.Should().Throw<MicroPythonException>().WithMessage("*ENOSPC*");
    }

    [Theory]
    [InlineData("main.py", true)]
    [InlineData("stratagems.json", true)]
    [InlineData("lib/x.py", false)]
    [InlineData("a'b.py", false)]
    [InlineData(".hidden", false)]
    public void Only_plain_file_names_are_accepted_as_targets(string target, bool valid)
    {
        var dir = Directory.CreateTempSubdirectory("hellboard-manifest").FullName;
        try
        {
            File.WriteAllText(Path.Combine(dir, "a.py"), "print(1)");
            File.WriteAllText(Path.Combine(dir, "manifest.json"), $$"""{"version": "1.0.0", "files": [{"source": "a.py", "target": "{{target.Replace("\"", "\\\"")}}"}]}""");

            var load = () => FirmwareBundle.Load(Path.Combine(dir, "manifest.json"));

            if (valid)
            {
                load().Files.Select(f => f.Target).Should().Equal(FirmwareBundle.VersionFile, target);
            }
            else
            {
                load.Should().Throw<FormatException>();
            }
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);
}
