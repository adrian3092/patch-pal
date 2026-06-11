using Microsoft.Win32;
using WinUpdateChecker.Core.Models;

namespace WinUpdateChecker.Core.Scanning;

/// <summary>
/// Reads installed programs from the HKLM/HKCU uninstall keys (including WOW6432Node),
/// ported from v1 Get-InstalledPrograms. Thin registry walk — the filter rules live in
/// <see cref="RegistryEntryFilter"/> where they are unit-tested.
/// </summary>
public sealed class RegistryScanner : IInstalledProgramProvider
{
    private static readonly (RegistryKey Hive, string Path)[] UninstallKeys =
    [
        (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
        (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
        (Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
    ];

    public IReadOnlyList<InstalledProgram> GetInstalledPrograms(bool includeSystemComponents)
    {
        var programs = new List<InstalledProgram>();
        var seen = new HashSet<string>();

        foreach (var (hive, path) in UninstallKeys)
        {
            using var key = hive.OpenSubKey(path);
            if (key is null) continue;

            foreach (var subKeyName in key.GetSubKeyNames())
            {
                using var sub = key.OpenSubKey(subKeyName);
                if (sub is null) continue;

                var entry = new RegistryEntry(
                    DisplayName: sub.GetValue("DisplayName") as string,
                    DisplayVersion: sub.GetValue("DisplayVersion") as string,
                    Publisher: sub.GetValue("Publisher") as string,
                    SystemComponent: sub.GetValue("SystemComponent") as int?,
                    ParentKeyName: sub.GetValue("ParentKeyName") as string,
                    ReleaseType: sub.GetValue("ReleaseType") as string);

                if (!RegistryEntryFilter.ShouldInclude(entry, includeSystemComponents)) continue;

                var name = entry.DisplayName!.Trim();
                var version = (entry.DisplayVersion ?? "").Trim();
                if (!seen.Add($"{name}|{version}".ToLowerInvariant())) continue;

                programs.Add(new InstalledProgram(name, version, entry.Publisher));
            }
        }

        return programs.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
