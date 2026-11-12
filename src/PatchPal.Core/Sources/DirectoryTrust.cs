using System.Security.AccessControl;
using System.Security.Principal;

namespace PatchPal.Core.Sources;

/// <summary>
/// Owner + ACL checks for files and folders. Conservative: anything it can't read or doesn't
/// recognize counts as not trusted. Deny entries are ignored, which can only make it stricter.
/// Junctions and symlinks are never trusted: their owner can retarget them after the check.
/// </summary>
public sealed class DirectoryTrust : IDirectoryTrust
{
    private static readonly SecurityIdentifier[] AdminPrincipals =
    [
        new(WellKnownSidType.LocalSystemSid, null),
        new(WellKnownSidType.BuiltinAdministratorsSid, null),
        new("S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464"), // NT SERVICE\TrustedInstaller
    ];

    private const FileSystemRights ChangeRights =
        FileSystemRights.WriteData | FileSystemRights.AppendData | FileSystemRights.WriteExtendedAttributes |
        FileSystemRights.DeleteSubdirectoriesAndFiles | FileSystemRights.WriteAttributes | FileSystemRights.Delete |
        FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership;

    // Rights on a folder itself that let its holder move it away (or grant themselves that).
    private const FileSystemRights MoveRights =
        FileSystemRights.Delete | FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership;

    private const int GenericAll = 0x10000000;
    private const int GenericWrite = 0x40000000;

    public bool IsAdminOnly(string path)
    {
        try
        {
            var security = ReadSecurity(path);
            return security is not null && OwnerIsAdmin(security)
                && !GrantsNonAdmin(security, (int)ChangeRights | GenericAll | GenericWrite);
        }
        catch (Exception ex) when (IsAclReadFailure(ex))
        {
            return false;
        }
    }

    public bool HasFixedLocation(string path)
    {
        try
        {
            for (var dir = new DirectoryInfo(Path.GetFullPath(path)); dir is not null; dir = dir.Parent)
            {
                var security = ReadSecurity(dir.FullName);
                if (security is null || !OwnerIsAdmin(security)) return false;
                if (GrantsNonAdmin(security, (int)MoveRights | GenericAll)) return false;
                // Renaming a folder also works with delete-child on its parent.
                var parent = dir.Parent is null ? null : ReadSecurity(dir.Parent.FullName);
                if (parent is not null && GrantsNonAdmin(parent, (int)FileSystemRights.DeleteSubdirectoriesAndFiles | GenericAll))
                    return false;
            }
            return true;
        }
        catch (Exception ex) when (IsAclReadFailure(ex))
        {
            return false;
        }
    }

    // Null when missing or a junction/symlink.
    private static FileSystemSecurity? ReadSecurity(string path)
    {
        if (Directory.Exists(path))
        {
            var dir = new DirectoryInfo(path);
            return dir.Attributes.HasFlag(FileAttributes.ReparsePoint) ? null : dir.GetAccessControl();
        }
        if (File.Exists(path))
        {
            var file = new FileInfo(path);
            return file.Attributes.HasFlag(FileAttributes.ReparsePoint) ? null : file.GetAccessControl();
        }
        return null;
    }

    // The owner can always rewrite the ACL, so a non-admin owner makes everything else moot.
    private static bool OwnerIsAdmin(FileSystemSecurity security)
        => security.GetOwner(typeof(SecurityIdentifier)) is SecurityIdentifier owner && AdminPrincipals.Contains(owner);

    private static bool GrantsNonAdmin(FileSystemSecurity security, int rights)
    {
        foreach (FileSystemAccessRule rule in security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
        {
            if (rule.AccessControlType != AccessControlType.Allow) continue;
            if (rule.PropagationFlags.HasFlag(PropagationFlags.InheritOnly)) continue; // applies to children only
            if (((int)rule.FileSystemRights & rights) == 0) continue;
            if (rule.IdentityReference is not SecurityIdentifier sid || !AdminPrincipals.Contains(sid)) return true;
        }
        return false;
    }

    private static bool IsAclReadFailure(Exception ex)
        => ex is UnauthorizedAccessException or IOException or InvalidOperationException
            or PrivilegeNotHeldException or IdentityNotMappedException or ArgumentException or NotSupportedException;
}
