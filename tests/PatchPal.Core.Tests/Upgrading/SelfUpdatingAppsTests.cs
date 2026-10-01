using PatchPal.Core.Upgrading;

namespace PatchPal.Core.Tests.Upgrading;

public class SelfUpdatingAppsTests
{
    [Theory]
    [InlineData("winget", "Microsoft.Edge")]
    [InlineData("winget", "microsoft.edge")]     // winget ids are case-insensitive
    public void WingetEdge_UpdatesItself(string source, string packageId)
        => Assert.True(SelfUpdatingApps.IsSelfUpdating(source, packageId));

    [Theory]
    [InlineData("winget", "Git.Git")]            // ordinary winget packages stay updatable
    [InlineData("chocolatey", "Microsoft.Edge")] // only winget's MSI-only Edge package is affected
    public void OtherPackages_DoNotUpdateThemselves(string source, string packageId)
        => Assert.False(SelfUpdatingApps.IsSelfUpdating(source, packageId));
}
