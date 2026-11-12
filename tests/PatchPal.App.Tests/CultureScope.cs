using System.Globalization;

namespace PatchPal.App.Tests;

/// <summary>Runs a test under a given region format and puts the previous one back after.</summary>
public sealed class CultureScope : IDisposable
{
    private readonly CultureInfo _previous = CultureInfo.CurrentCulture;

    public CultureScope(string name) => CultureInfo.CurrentCulture = new CultureInfo(name, useUserOverride: false);

    public void Dispose() => CultureInfo.CurrentCulture = _previous;
}
