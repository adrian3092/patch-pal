using PatchPal.Core.Persistence;

namespace PatchPal.Core.Tests.Persistence;

public class AppDataPathsTests
{
    [Fact]
    public void ResolveRoot_MovesLegacyFolder_WhenNewFolderMissing()
    {
        var baseDir = Directory.CreateTempSubdirectory();
        try
        {
            var legacy = Directory.CreateDirectory(Path.Combine(baseDir.FullName, "WinUpdateChecker"));
            File.WriteAllText(Path.Combine(legacy.FullName, "settings.json"), "{\"theme\":\"Dark\"}");

            var root = AppDataPaths.ResolveRoot(baseDir.FullName);

            Assert.Equal(Path.Combine(baseDir.FullName, "PatchPal"), root);
            Assert.False(Directory.Exists(legacy.FullName));
            Assert.Equal("{\"theme\":\"Dark\"}", File.ReadAllText(Path.Combine(root, "settings.json")));
        }
        finally
        {
            baseDir.Delete(recursive: true);
        }
    }

    [Fact]
    public void ResolveRoot_LeavesLegacyAlone_WhenNewFolderExists()
    {
        var baseDir = Directory.CreateTempSubdirectory();
        try
        {
            var legacy = Directory.CreateDirectory(Path.Combine(baseDir.FullName, "WinUpdateChecker"));
            File.WriteAllText(Path.Combine(legacy.FullName, "settings.json"), "old");
            var current = Directory.CreateDirectory(Path.Combine(baseDir.FullName, "PatchPal"));
            File.WriteAllText(Path.Combine(current.FullName, "settings.json"), "new");

            var root = AppDataPaths.ResolveRoot(baseDir.FullName);

            Assert.Equal(current.FullName, root);
            Assert.Equal("new", File.ReadAllText(Path.Combine(root, "settings.json")));
            Assert.True(Directory.Exists(legacy.FullName));
        }
        finally
        {
            baseDir.Delete(recursive: true);
        }
    }

    [Fact]
    public void ResolveRoot_ReturnsNewPath_WhenNothingExists()
    {
        var baseDir = Directory.CreateTempSubdirectory();
        try
        {
            var root = AppDataPaths.ResolveRoot(baseDir.FullName);

            Assert.Equal(Path.Combine(baseDir.FullName, "PatchPal"), root);
        }
        finally
        {
            baseDir.Delete(recursive: true);
        }
    }
}
