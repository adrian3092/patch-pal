using System.Globalization;

namespace PatchPal.App;

/// <summary>
/// Times and dates the way the Windows region format writes them: 12- or 24-hour, and the
/// region's date order, including any change the user made to that format in Settings.
/// </summary>
public static class LocalFormat
{
    public static string Time(DateTimeOffset time) => time.ToString("t", CultureInfo.CurrentCulture);

    public static string Date(DateOnly date) => date.ToString("D", CultureInfo.CurrentCulture);
}
