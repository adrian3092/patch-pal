using PatchPal.Core.Persistence;

namespace PatchPal.Core.Tests.Persistence;

public class ErrorLogTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory();
    public void Dispose() => _dir.Delete(recursive: true);

    private static readonly DateTimeOffset Noon = new(2026, 11, 9, 12, 0, 0, TimeSpan.FromHours(-6));

    private static Exception Thrown(string message)
    {
        try
        {
            throw new InvalidOperationException(message);
        }
        catch (InvalidOperationException ex)
        {
            return ex;
        }
    }

    [Fact]
    public void Write_KeepsEveryErrorWithWhenAndWhereItHappened()
    {
        var path = ErrorLog.Write(_dir.FullName, Thrown("first"), "Unexpected error", Noon);
        ErrorLog.Write(_dir.FullName, Thrown("second"), "Unexpected error", Noon.AddMinutes(5));

        Assert.Equal(Path.Combine(_dir.FullName, "error.log"), path);
        var log = File.ReadAllText(path!);
        Assert.Contains("2026-11-09 12:00:00", log);
        Assert.Contains("2026-11-09 12:05:00", log);
        Assert.Contains("Unexpected error", log);
        Assert.Contains("System.InvalidOperationException: first", log);
        Assert.Contains("System.InvalidOperationException: second", log);
        Assert.Contains(nameof(Thrown), log);                    // the stack trace, to find the cause
    }

    [Fact]
    public void Write_MovesALargeLogAsideAndStartsAFreshOne()
    {
        var path = Path.Combine(_dir.FullName, "error.log");
        var old = new string('x', 200);
        File.WriteAllText(path, old);

        ErrorLog.Write(_dir.FullName, Thrown("new"), "Unexpected error", Noon, maxBytes: 100);

        Assert.Equal(old, File.ReadAllText(path + ".old"));
        var log = File.ReadAllText(path);
        Assert.Contains("System.InvalidOperationException: new", log);
        Assert.DoesNotContain(old, log);
    }

    [Fact]
    public void Write_WhenTheLogCannotBeWritten_ReturnsNullInsteadOfThrowing()
    {
        // It runs in the app's last-resort error handler, where throwing would end the app.
        var notAFolder = Path.Combine(_dir.FullName, "not-a-folder");
        File.WriteAllText(notAFolder, "");

        Assert.Null(ErrorLog.Write(Path.Combine(notAFolder, "PatchPal"), Thrown("boom"), "Unexpected error", Noon));
    }
}
