using System.Runtime.InteropServices;
using WinUpdateChecker.Core.Cli;

namespace WinUpdateChecker.App;

public static class Program
{
    private const int AttachParentProcess = -1;

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int processId);

    [STAThread]
    public static int Main(string[] args)
    {
        var options = CliOptions.Parse(args);
        if (options.IsHeadless)
        {
            // A WinExe has no console; borrow the parent shell's so output is visible
            // when invoked from PowerShell/cmd or a scheduled task.
            AttachConsole(AttachParentProcess);
            return Cli.CliRunner.RunAsync(options).GetAwaiter().GetResult();
        }

        var app = new App();
        app.InitializeComponent();
        return app.Run();
    }
}
