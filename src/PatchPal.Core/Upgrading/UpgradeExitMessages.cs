namespace PatchPal.Core.Upgrading;

/// <summary>
/// Translates a package-manager exit code into a human-readable result so the UI can
/// tell the user WHY an upgrade did nothing instead of failing silently.
/// </summary>
public static class UpgradeExitMessages
{
    public static string Translate(string source, int? exitCode)
    {
        if (exitCode is null) return "No exit code was returned by the installer.";
        var code = exitCode.Value;
        if (code == 0) return "Succeeded.";
        if (source == "winget")
        {
            return code switch
            {
                -1978335189 => "No applicable upgrade (already current, pinned, or version mismatch).", // 0x8A15002B
                -1978335212 => "No installed package matched for upgrade.",                              // 0x8A150014
                -1978335216 => "No installer applicable to this system.",                                // 0x8A150010
                -1978334969 => "No network connection — connect to the internet and try again.",         // 0x8A150107
                -1978335090 => "Installed by a different installer type — update it from within the app.", // 0x8A15008E
                1602 => "Installer cancelled.",
                1603 => "Fatal installer error (1603) — a newer version may already be present.",
                -2147023673 => "Operation cancelled (UAC prompt declined?).",
                _ => $"winget exited with code {code} (0x{(uint)code:X8}).",
            };
        }
        return $"{source} exited with code {code}.";
    }
}
