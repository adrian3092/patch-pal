namespace PatchPal.Core.Sources;

/// <summary>Finds Chocolatey only where Patch Pal can safely run it with admin rights.</summary>
public interface IChocolateyLocator
{
    /// <summary>The machine-wide install folder; choco is always started with ChocolateyInstall set to it.</summary>
    string InstallDirectory { get; }

    /// <summary>
    /// Full path to choco.exe when it exists in an install folder no normal account can change;
    /// otherwise null, and Chocolatey counts as not installed.
    /// </summary>
    string? ChocoExePath { get; }
}

/// <summary>Decides whether a file or folder is under admin-only control.</summary>
public interface IDirectoryTrust
{
    /// <summary>
    /// True when no normal account can change <paramref name="path"/>: it isn't a junction or
    /// symlink, its owner is an admin principal, and only admin principals may write to it.
    /// Missing or unreadable → false.
    /// </summary>
    bool IsAdminOnly(string path);

    /// <summary>
    /// True when no normal account can move <paramref name="path"/> or any folder above it out of
    /// the way (and put something else there): no junctions or symlinks on the way up, every folder
    /// admin-owned, and no non-admin delete, delete-child, change-permissions or take-ownership rights.
    /// </summary>
    bool HasFixedLocation(string path);
}
