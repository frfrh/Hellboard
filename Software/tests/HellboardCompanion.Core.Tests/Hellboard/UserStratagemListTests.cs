// SPDX-License-Identifier: GPL-3.0-or-later

using AwesomeAssertions;
using HellboardCompanion.Core.Hellboard;
using Microsoft.Extensions.Logging.Abstractions;

namespace HellboardCompanion.Core.Tests.Hellboard;

public sealed class UserStratagemListTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("hellboard-user-list").FullName;

    private string File1 => Path.Combine(_dir, "stratagems.json");

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Saved_entries_are_read_back_and_merged_into_the_catalog()
    {
        var list = UserStratagemList.Load(File1);
        list.Set(new UserStratagem("my_flag", "My Flag", "Mission", "UUDD"));
        list.Set(new UserStratagem("machine_gun", "Machine Gun", "Support weapon", "DLDUL"));   // corrected code
        list.Save();

        UserStratagemList.Load(File1).Entries.Should().HaveCount(2);
        var catalog = StratagemCatalog.Load(File1, NullLogger.Instance);
        catalog.Find("my_flag")!.Sequence.Should().Be("UUDD");
        catalog.Find("machine_gun")!.Sequence.Should().Be("DLDUL");
        catalog.UserIds.Should().BeEquivalentTo("my_flag", "machine_gun");
        catalog.UserFilePath.Should().Be(File1);
    }

    [Fact]
    public void Every_entry_is_written_on_its_own_line()
    {
        var list = UserStratagemList.Load(File1);
        list.Set(new UserStratagem("a", "A", "Mission", "UUD"));
        list.Set(new UserStratagem("b", "B", "Mission", null));
        list.Save();

        File.ReadAllLines(File1).Count(l => l.TrimStart().StartsWith("{\"id\":", StringComparison.Ordinal)).Should().Be(2);
    }

    [Fact]
    public void Removing_an_override_restores_the_built_in_code()
    {
        var list = UserStratagemList.Load(File1);
        list.Set(new UserStratagem("machine_gun", "Machine Gun", "Support weapon", "UUUU"));
        list.Save();
        list.Remove("machine_gun").Should().BeTrue();
        list.Save();

        StratagemCatalog.Load(File1, NullLogger.Instance).Find("machine_gun")!.Sequence.Should().Be("DLDUR");
    }

    [Fact]
    public void Import_merges_another_list()
    {
        var other = UserStratagemList.Load(Path.Combine(_dir, "shared.json"));
        other.Set(new UserStratagem("shared", "Shared", "Eagle", "URL"));
        other.Save();
        var list = UserStratagemList.Load(File1);
        list.Set(new UserStratagem("mine", "Mine", "Mission", "DDD"));

        list.Import(other.FilePath).Should().Be(1);

        list.Entries.Select(e => e.Id).Should().Equal("mine", "shared");
    }

    [Theory]
    [InlineData("ok_id", "Name", "Mission", "UDLR", null)]
    [InlineData("ok_id", "Name", "Mission", null, null)]
    [InlineData("Bad Id", "Name", "Mission", "UDL", "id")]
    [InlineData("ok_id", "", "Mission", "UDL", "name")]
    [InlineData("ok_id", "Name", "Boosters", "UDL", "category")]
    [InlineData("ok_id", "Name", "Mission", "UD", "code")]
    [InlineData("ok_id", "Name", "Mission", "UDLRUDLRUDL", "code")]
    public void Entries_are_validated(string id, string name, string category, string? code, string? problem)
    {
        var error = UserStratagemList.Validate(new UserStratagem(id, name, category, code), ["Mission", "Eagle"]);

        if (problem is null)
        {
            error.Should().BeNull();
        }
        else
        {
            error.Should().Contain(problem);
        }
    }

    [Fact]
    public void Replacing_the_catalog_content_notifies_its_users()
    {
        var catalog = StratagemCatalog.Load(File1, NullLogger.Instance);
        var list = UserStratagemList.Load(File1);
        list.Set(new UserStratagem("my_flag", "My Flag", "Mission", "UUDD"));
        list.Save();
        var changed = false;
        catalog.Changed += (_, _) => changed = true;

        catalog.ReplaceWith(StratagemCatalog.Load(File1, NullLogger.Instance));

        changed.Should().BeTrue();
        catalog.Find("my_flag").Should().NotBeNull();
    }
}
