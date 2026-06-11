using System.Security;
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
    private static readonly (RegistryHive Hive, RegistryView View)[] UninstallViews =
    [
        (RegistryHive.LocalMachine, RegistryView.Registry64),
        (RegistryHive.LocalMachine, RegistryView.Registry32),   // replaces the literal WOW6432Node path
        (RegistryHive.CurrentUser, RegistryView.Default),
    ];

    private const string UninstallPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    public IReadOnlyList<InstalledProgram> GetInstalledPrograms(bool includeSystemComponents)
    {
        var programs = new List<InstalledProgram>();
        var seen = new HashSet<string>();

        foreach (var (hive, view) in UninstallViews)
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(UninstallPath);
            if (key is null) continue;

            foreach (var subKeyName in key.GetSubKeyNames())
            {
                try
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
                catch (Exception ex) when (ex is SecurityException or IOException or UnauthorizedAccessException)
                {
                    // corrupt or access-denied key — skip it, matching v1's SilentlyContinue
                }
            }
        }

        return programs.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
