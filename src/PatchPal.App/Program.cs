using System.Runtime.InteropServices;
using PatchPal.Core.Cli;

namespace PatchPal.App;

public static class Program
{
    private const int AttachParentProcess = -1;
    private const int StdOutputHandle = -11;

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int processId);

    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    private const uint LOAD_LIBRARY_SEARCH_DEFAULT_DIRS = 0x1000;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetDefaultDllDirectories(uint directoryFlags);

    [STAThread]
    public static int Main(string[] args)
    {
        // Restrict DLL loading to the app directory + System32 (no working-dir / PATH search).
        SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);

        var options = CliOptions.Parse(args);
        if (options.IsHeadless)
        {
            // If stdout is already a real handle (redirected to a pipe/file), use it as-is —
            // attaching a console would steal output away from the redirection. Only borrow
            // or allocate a console when we have no stdout at all (launched from a shell
            // without redirection, Task Scheduler, or Explorer).
            if (GetStdHandle(StdOutputHandle) == IntPtr.Zero && !AttachConsole(AttachParentProcess))
                AllocConsole();
            return Cli.CliRunner.RunAsync(options).GetAwaiter().GetResult();
        }

        var app = new App();
        app.InitializeComponent();
        return app.Run();
    }
}
