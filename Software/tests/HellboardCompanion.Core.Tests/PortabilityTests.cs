// SPDX-License-Identifier: GPL-3.0-or-later

using System.Reflection;
using System.Runtime.Versioning;
using AwesomeAssertions;
using HellboardCompanion.App;

namespace HellboardCompanion.Core.Tests;

/// <summary>
/// Guards the portability rule of implementation plan §3.1: Core and App stay platform-neutral,
/// so a later Linux port only needs a new platform project.
/// </summary>
public sealed class PortabilityTests
{
    private static readonly string[] ForbiddenAssemblyPrefixes =
    [
        "HellboardCompanion.Platform.",
        "HellboardCompanion.Desktop.",
        "Microsoft.Win32",
        "Microsoft.Windows",
        "System.Windows",
        "Windows",
        "WinRT",
    ];

    public static TheoryData<string> PortableAssemblies => new()
    {
        typeof(AppInfo).Assembly.GetName().Name!,
        typeof(CompanionApp).Assembly.GetName().Name!,
    };

    [Theory]
    [MemberData(nameof(PortableAssemblies))]
    public void Portable_assemblies_do_not_target_a_specific_platform(string assemblyName)
    {
        var assembly = Assembly.Load(assemblyName);

        assembly.GetCustomAttribute<TargetPlatformAttribute>().Should().BeNull(
            "{0} must target plain net10.0, not a platform-specific framework", assemblyName);
    }

    [Theory]
    [MemberData(nameof(PortableAssemblies))]
    public void Portable_assemblies_do_not_reference_platform_specific_assemblies(string assemblyName)
    {
        var references = Assembly.Load(assemblyName).GetReferencedAssemblies().Select(reference => reference.Name!);

        references.Should().NotContain(
            name => ForbiddenAssemblyPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)),
            "{0} must stay platform-neutral", assemblyName);
    }
}
