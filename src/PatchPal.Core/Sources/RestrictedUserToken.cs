using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;
using static PatchPal.Core.Sources.UserProcessNative;

namespace PatchPal.Core.Sources;

/// <summary>
/// Builds a copy of this process's token with the rights of a normal (non-admin) prompt:
/// Administrators and "local account and member of Administrators" deny-only, every privilege
/// a normal prompt lacks removed, Medium integrity, the user as default owner, and a default
/// DACL of the user + SYSTEM (an elevated token's default would hand new objects to Administrators).
/// </summary>
internal static class RestrictedUserToken
{
    private static readonly string[] NormalPrivileges =
    [
        "SeShutdownPrivilege", "SeChangeNotifyPrivilege", "SeUndockPrivilege",
        "SeIncreaseWorkingSetPrivilege", "SeTimeZonePrivilege",
    ];

    private static readonly SecurityIdentifier[] SidsToDisable =
    [
        new("S-1-5-32-544"), // BUILTIN\Administrators
        new("S-1-5-114"),    // NT AUTHORITY\Local account and member of Administrators group
    ];

    private static readonly SecurityIdentifier MediumIntegrity = new("S-1-16-8192");
    private static readonly SecurityIdentifier LocalSystem = new(WellKnownSidType.LocalSystemSid, null);
    private const int GenericAll = 0x10000000;

    public static SafeAccessTokenHandle Create()
    {
        using var current = Process.GetCurrentProcess();
        Check(OpenProcessToken(current.Handle, TOKEN_ASSIGN_PRIMARY | TOKEN_DUPLICATE | TOKEN_QUERY | TOKEN_ADJUST_DEFAULT, out var self));
        using (self)
        using (var identity = WindowsIdentity.GetCurrent())
        {
            var user = identity.User ?? throw new InvalidOperationException("The current token has no user SID.");
            var restricted = Restrict(self);
            try
            {
                SetSid(restricted, TokenIntegrityLevel, MediumIntegrity, SE_GROUP_INTEGRITY, withAttributes: true);
                SetSid(restricted, TokenOwner, user, 0, withAttributes: false);
                SetDefaultDacl(restricted, user);
                return restricted;
            }
            catch
            {
                restricted.Dispose();
                throw;
            }
        }
    }

    private static SafeAccessTokenHandle Restrict(SafeAccessTokenHandle self)
    {
        var pins = SidsToDisable.Select(sid => GCHandle.Alloc(SidBytes(sid), GCHandleType.Pinned)).ToList();
        try
        {
            var disable = pins.Select(p => new SID_AND_ATTRIBUTES { Sid = p.AddrOfPinnedObject() }).ToArray();
            var delete = PrivilegesToDelete(self);
            Check(CreateRestrictedToken(self, 0, disable.Length, disable, delete.Length, delete, 0, null, out var restricted));
            return restricted;
        }
        finally
        {
            pins.ForEach(p => p.Free());
        }
    }

    private static LUID_AND_ATTRIBUTES[] PrivilegesToDelete(SafeAccessTokenHandle token)
    {
        GetTokenInformation(token, TokenPrivileges, IntPtr.Zero, 0, out var length);
        var buffer = Marshal.AllocHGlobal(length);
        try
        {
            Check(GetTokenInformation(token, TokenPrivileges, buffer, length, out _));
            var count = Marshal.ReadInt32(buffer);
            var entrySize = Marshal.SizeOf<LUID_AND_ATTRIBUTES>();
            var result = new List<LUID_AND_ATTRIBUTES>();
            for (var i = 0; i < count; i++)
            {
                var entry = Marshal.PtrToStructure<LUID_AND_ATTRIBUTES>(buffer + sizeof(int) + i * entrySize);
                if (!NormalPrivileges.Contains(PrivilegeName(entry.Luid)))
                    result.Add(entry);
            }
            return [.. result];
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string PrivilegeName(LUID luid)
    {
        var length = 64;
        var name = new StringBuilder(length);
        Check(LookupPrivilegeName(null, ref luid, name, ref length));
        return name.ToString();
    }

    // TOKEN_MANDATORY_LABEL / TOKEN_OWNER followed by the SID itself, in one block, so the
    // length passed (struct + SID, as documented) never reaches past the allocation.
    private static void SetSid(SafeAccessTokenHandle token, int infoClass, SecurityIdentifier sid, uint attributes, bool withAttributes)
    {
        var bytes = SidBytes(sid);
        var header = withAttributes ? Marshal.SizeOf<SID_AND_ATTRIBUTES>() : IntPtr.Size;
        var buffer = Marshal.AllocHGlobal(header + bytes.Length);
        try
        {
            var sidPtr = buffer + header;
            Marshal.Copy(bytes, 0, sidPtr, bytes.Length);
            if (withAttributes)
                Marshal.StructureToPtr(new SID_AND_ATTRIBUTES { Sid = sidPtr, Attributes = attributes }, buffer, false);
            else
                Marshal.WriteIntPtr(buffer, sidPtr);
            Check(SetTokenInformation(token, infoClass, buffer, header + bytes.Length));
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static void SetDefaultDacl(SafeAccessTokenHandle token, SecurityIdentifier user)
    {
        var acl = new RawAcl(GenericAcl.AclRevision, 2);
        acl.InsertAce(0, new CommonAce(AceFlags.None, AceQualifier.AccessAllowed, GenericAll, user, false, null));
        acl.InsertAce(1, new CommonAce(AceFlags.None, AceQualifier.AccessAllowed, GenericAll, LocalSystem, false, null));
        var bytes = new byte[acl.BinaryLength];
        acl.GetBinaryForm(bytes, 0);
        var pin = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        var buffer = Marshal.AllocHGlobal(IntPtr.Size);
        try
        {
            Marshal.WriteIntPtr(buffer, pin.AddrOfPinnedObject());
            Check(SetTokenInformation(token, TokenDefaultDacl, buffer, IntPtr.Size));
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
            pin.Free();
        }
    }

    private static byte[] SidBytes(SecurityIdentifier sid)
    {
        var bytes = new byte[sid.BinaryLength];
        sid.GetBinaryForm(bytes, 0);
        return bytes;
    }
}
