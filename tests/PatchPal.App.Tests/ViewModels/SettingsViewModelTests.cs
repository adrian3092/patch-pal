using System.IO;
using PatchPal.App.ViewModels;
using PatchPal.Core.Settings;

namespace PatchPal.App.Tests.ViewModels;

public class SettingsViewModelTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory();
    public void Dispose() => _dir.Delete(recursive: true);

    private SettingsViewModel Vm(AppSettings? initial = null)
    {
        var store = new SettingsStore(_dir.FullName);
        if (initial is not null) store.Save(initial);
        return new SettingsViewModel(store, store.Load(),
            sourceInstalled: name => name != "chocolatey",       // pretend choco is missing
            applyTheme: _ => { });
    }

    [Fact]
    public void SourceToggles_ReflectInstalledAndEnabledState()
    {
        var vm = Vm(new AppSettings { DisabledSources = ["scoop"] });
        var winget = vm.Sources.First(s => s.Name == "winget");
        var scoop = vm.Sources.First(s => s.Name == "scoop");
        var choco = vm.Sources.First(s => s.Name == "chocolatey");
        Assert.True(winget.IsEnabled);
        Assert.False(scoop.IsEnabled);                           // user-disabled
        Assert.True(scoop.IsInstalled);
        Assert.False(choco.IsInstalled);                         // not installed -> toggle disabled in UI
    }

    [Fact]
    public void TogglingSource_PersistsDisabledList()
    {
        var vm = Vm();
        vm.Sources.First(s => s.Name == "scoop").IsEnabled = false;
        var reloaded = new SettingsStore(_dir.FullName).Load();
        Assert.Contains("scoop", reloaded.DisabledSources);
    }

    [Fact]
    public void ChangingTheme_AppliesAndPersists()
    {
        ThemePreference? applied = null;
        var store = new SettingsStore(_dir.FullName);
        var vm = new SettingsViewModel(store, store.Load(), _ => true, t => applied = t);
        vm.SelectedTheme = ThemePreference.Light;
        Assert.Equal(ThemePreference.Light, applied);
        Assert.Equal(ThemePreference.Light, store.Load().Theme);
    }

    [Fact]
    public void ChangingASetting_KeepsTheRestOfTheFile_EvenWhenStartupFellBackToDefaults()
    {
        // When settings.json couldn't be read at startup the page starts from the defaults;
        // saving those would replace every other setting in the file with its default.
        var store = new SettingsStore(_dir.FullName);
        store.Save(new AppSettings { DisabledSources = ["scoop"] });
        var vm = new SettingsViewModel(store, new AppSettings(), _ => true, _ => { });

        vm.ScanOnLaunch = false;

        var saved = store.Load();
        Assert.False(saved.ScanOnLaunch);
        Assert.Equal(["scoop"], saved.DisabledSources);
    }

    [Fact]
    public void ChangingASetting_WhenTheFileCannotBeRead_LeavesTheFileAlone()
    {
        var store = new SettingsStore(_dir.FullName);
        store.Save(new AppSettings { DisabledSources = ["scoop"] });
        var vm = new SettingsViewModel(store, store.Load(), _ => true, _ => { });

        using (new FileStream(Path.Combine(_dir.FullName, "settings.json"), FileMode.Open, FileAccess.Read, FileShare.Write))
            vm.ScanOnLaunch = false;                              // must neither throw nor save

        var saved = store.Load();
        Assert.True(saved.ScanOnLaunch);
        Assert.Equal(["scoop"], saved.DisabledSources);
    }

    [Fact]
    public void ScanOnLaunchAndSystemComponents_Persist()
    {
        var store = new SettingsStore(_dir.FullName);
        var vm = new SettingsViewModel(store, store.Load(), _ => true, _ => { });
        vm.ScanOnLaunch = false;
        vm.IncludeSystemComponents = true;
        var reloaded = store.Load();
        Assert.False(reloaded.ScanOnLaunch);
        Assert.True(reloaded.IncludeSystemComponents);
    }
}
