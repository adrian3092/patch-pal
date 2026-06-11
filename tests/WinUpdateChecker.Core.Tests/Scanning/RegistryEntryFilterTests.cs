using WinUpdateChecker.Core.Scanning;

namespace WinUpdateChecker.Core.Tests.Scanning;

public class RegistryEntryFilterTests
{
    private static RegistryEntry Entry(
        string? name = "Some App", string? version = "1.0",
        int? systemComponent = null, string? parentKeyName = null, string? releaseType = null)
        => new(name, version, "Pub", systemComponent, parentKeyName, releaseType);

    [Fact]
    public void NormalProgram_IsIncluded()
        => Assert.True(RegistryEntryFilter.ShouldInclude(Entry(), includeSystemComponents: false));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingDisplayName_IsAlwaysExcluded(string? name)
    {
        Assert.False(RegistryEntryFilter.ShouldInclude(Entry(name: name), false));
        Assert.False(RegistryEntryFilter.ShouldInclude(Entry(name: name), true)); // even with system components
    }

    [Fact]
    public void SystemComponent_IsExcludedByDefault()
        => Assert.False(RegistryEntryFilter.ShouldInclude(Entry(systemComponent: 1), false));

    [Fact]
    public void ChildEntry_IsExcludedByDefault()
        => Assert.False(RegistryEntryFilter.ShouldInclude(Entry(parentKeyName: "ParentApp"), false));

    [Theory]
    [InlineData("Update")]
    [InlineData("Hotfix")]
    [InlineData("Security Update")]
    public void UpdateReleaseTypes_AreExcludedByDefault(string releaseType)
        => Assert.False(RegistryEntryFilter.ShouldInclude(Entry(releaseType: releaseType), false));

    [Theory]
    [InlineData("KB5034441")]
    [InlineData("Update for Microsoft Office")]
    [InlineData("Security Update for Windows")]
    [InlineData("Hotfix for .NET")]
    public void HotfixStyleNames_AreExcludedByDefault(string name)
        => Assert.False(RegistryEntryFilter.ShouldInclude(Entry(name: name), false));

    [Fact]
    public void IncludeSystemComponents_KeepsEverythingWithAName()
    {
        Assert.True(RegistryEntryFilter.ShouldInclude(Entry(systemComponent: 1), true));
        Assert.True(RegistryEntryFilter.ShouldInclude(Entry(parentKeyName: "x"), true));
        Assert.True(RegistryEntryFilter.ShouldInclude(Entry(name: "KB5034441"), true));
    }
}
