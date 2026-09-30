using System.Threading;
using System.Windows;

namespace Wordwright.App;

public partial class App : Application
{
    private const string MutexName = @"Local\Wordwright.SingleInstance";
    private const string ShowWindowEventName = @"Local\Wordwright.ShowMainWindow";

    private Mutex? _mutex;
    private EventWaitHandle? _showWindowEvent;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Created before the mutex so a second launch can always find it once the mutex exists.
        _showWindowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowWindowEventName);

        _mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            // Second launch: tell the running instance to open its window, then exit.
            _showWindowEvent.Set();
            Shutdown();
            return;
        }

        _ = ThreadPool.RegisterWaitForSingleObject(
            _showWindowEvent,
            callBack: (_, _) => Dispatcher.Invoke(ShowMainWindow),
            state: null,
            millisecondsTimeOutInterval: Timeout.Infinite,
            executeOnlyOnce: false);

        // The app runs from the tray; the window is created hidden and shown on demand.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _mainWindow = new MainWindow { Visibility = Visibility.Hidden };
    }

    internal void ShowMainWindow()
    {
        _mainWindow ??= new MainWindow();
        _mainWindow.Show();
        _mainWindow.Activate();
    }

    internal void Quit()
    {
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_mutex is not null)
        {
            // Only the instance that owns the mutex may release it.
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Not owned (second instance path); nothing to release.
            }

            _mutex.Dispose();
        }

        _showWindowEvent?.Dispose();
        base.OnExit(e);
    }
}