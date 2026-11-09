using System.Windows;
using System.Windows.Threading;
using PatchPal.Core;
using PatchPal.Core.Persistence;

namespace PatchPal.App;

/// <summary>
/// The application. Errors that nothing else handled go to %APPDATA%\PatchPal\error.log. Once the
/// window is up, one on the UI thread is reported and the app keeps running, so a batch of updates
/// in progress still gets its results and History entries.
/// </summary>
public partial class App : Application
{
    private bool _isShowingError;

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            ErrorLog.Write(e.ExceptionObject as Exception ?? new Exception($"{e.ExceptionObject}"), "Unhandled error");
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            ErrorLog.Write(e.Exception, "Unobserved task error");
            e.SetObserved();
        };
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var logPath = ErrorLog.Write(e.Exception, "Unexpected error");

        // Before the window is up there is nothing to keep running, so the error still ends the app.
        e.Handled = MainWindow is { IsLoaded: true };
        if (_isShowingError) return;                             // one dialog at a time; the log has the rest

        _isShowingError = true;
        try
        {
            var details = logPath is null ? "" : $"\n\nThe details are in {logPath}.";
            var message = $"Something went wrong: {e.Exception.Message}{details}";
            if (e.Handled)
                System.Windows.MessageBox.Show(MainWindow, message, AppInfo.ProductName, MessageBoxButton.OK, MessageBoxImage.Error);
            else
                System.Windows.MessageBox.Show(message, AppInfo.ProductName, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _isShowingError = false;
        }
    }
}
