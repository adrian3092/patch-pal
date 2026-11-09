using PatchPal.Core.Settings;

namespace PatchPal.Core.Tests.Settings;

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
    public void Load_NullSourceList_MeansNoneSwitchedOff()
    {
        // A hand-edited file can say "DisabledSources": null; JSON overwrites the default with it.
        File.WriteAllText(Path.Combine(_dir.FullName, "settings.json"), """{ "DisabledSources": null }""");
        Assert.Empty(Store().Load().DisabledSources);
    }

    [Fact]
    public void Update_ChangesOneSettingAndKeepsTheRestOfTheFile()
    {
        var store = Store();
        store.Save(new AppSettings { DisabledSources = ["scoop"] });

        var updated = store.Update(s => s with { Theme = ThemePreference.Dark });

        Assert.Equal(["scoop"], updated.DisabledSources);
        var saved = store.Load();
        Assert.Equal(ThemePreference.Dark, saved.Theme);
        Assert.Equal(["scoop"], saved.DisabledSources);
    }

    [Fact]
    public void Update_WhenTheFileCannotBeRead_FailsWithoutTouchingIt()
    {
        var store = Store();
        store.Save(new AppSettings { DisabledSources = ["scoop"] });
        var path = Path.Combine(_dir.FullName, "settings.json");

        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Write))
            Assert.ThrowsAny<IOException>(() => store.Update(s => s with { Theme = ThemePreference.Dark }));

        Assert.Equal(ThemePreference.System, store.Load().Theme);
        Assert.Equal(["scoop"], store.Load().DisabledSources);
    }

    [Fact]
    public void Load_CorruptFile_BacksUpAndReturnsDefaults()
    {
        var path = Path.Combine(_dir.FullName, "settings.json");
        File.WriteAllText(path, "{ not valid json !!");
        var s = Store().Load();
        Assert.Equal(ThemePreference.System, s.Theme);          // defaults, no crash
        var backup = Assert.Single(Directory.GetFiles(_dir.FullName, "settings.json*.bak"));
        Assert.Equal("{ not valid json !!", File.ReadAllText(backup));   // corrupt file preserved
    }
}
