using WinUpdateChecker.Core.Settings;

namespace WinUpdateChecker.Core.Tests.Settings;

public class SettingsStoreTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory();
    public void Dispose() => _dir.Delete(recursive: true);

    private SettingsStore Store() => new(_dir.FullName);

    [Fact]
    public void Load_ReturnsDefaults_WhenFileMissing()
    {
        var s = Store().Load();
        Assert.Equal(ThemePreference.System, s.Theme);
        Assert.True(s.ScanOnLaunch);
        Assert.False(s.IncludeSystemComponents);
        Assert.Empty(s.DisabledSources);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var store = Store();
        store.Save(new AppSettings
        {
            Theme = ThemePreference.Dark,
            ScanOnLaunch = false,
            IncludeSystemComponents = true,
            DisabledSources = ["chocolatey"],
        });
        var s = store.Load();
        Assert.Equal(ThemePreference.Dark, s.Theme);
        Assert.False(s.ScanOnLaunch);
        Assert.True(s.IncludeSystemComponents);
        Assert.Equal(new[] { "chocolatey" }, s.DisabledSources);
    }

    [Fact]
    public void Load_CorruptFile_BacksUpAndReturnsDefaults()
    {
        var path = Path.Combine(_dir.FullName, "settings.json");
        File.WriteAllText(path, "{ not valid json !!");
        var s = Store().Load();
        Assert.Equal(ThemePreference.System, s.Theme);          // defaults, no crash
        Assert.True(File.Exists(path + ".bak"));                 // corrupt file preserved
    }
}
