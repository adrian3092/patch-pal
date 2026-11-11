using PatchPal.Core.Sources;

namespace PatchPal.Core.Tests.Sources;

public sealed class FakeChocolateyLocator(string? path) : IChocolateyLocator
{
    public const string DefaultPath = @"C:\ProgramData\chocolatey\bin\choco.exe";

    /// <summary>Chocolatey found in a trusted folder at <see cref="DefaultPath"/>.</summary>
    public static FakeChocolateyLocator Installed { get; } = new(DefaultPath);

    public string InstallDirectory { get; } =
        path is null ? @"C:\ProgramData\chocolatey" : Path.GetDirectoryName(Path.GetDirectoryName(path))!;

    public string? ChocoExePath => path;
}
