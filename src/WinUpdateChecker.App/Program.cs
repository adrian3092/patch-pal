using System.Runtime.InteropServices;
using WinUpdateChecker.Core.Cli;

namespace WinUpdateChecker.App;

public static class Program
{
    private const int AttachParentProcess = -1;

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int processId);

    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    [STAThread]
    public static int Main(string[] args)
    {
        var options = CliOptions.Parse(args);
        if (options.IsHeadless)
        {
            // Borrow the parent shell's console; if there is none (Task Scheduler,
            // Explorer), allocate one so output and errors are not silently lost.
            if (!AttachConsole(AttachParentProcess))
                AllocConsole();
            return Cli.CliRunner.RunAsync(options).GetAwaiter().GetResult();
        }

        var app = new App();
        app.InitializeComponent();
        return app.Run();
    }
}
