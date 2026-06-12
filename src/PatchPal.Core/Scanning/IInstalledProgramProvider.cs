using PatchPal.Core.Models;

namespace PatchPal.Core.Scanning;

/// <summary>Seam so ScanService can be tested without touching the real registry.</summary>
public interface IInstalledProgramProvider
{
    IReadOnlyList<InstalledProgram> GetInstalledPrograms(bool includeSystemComponents);
}
