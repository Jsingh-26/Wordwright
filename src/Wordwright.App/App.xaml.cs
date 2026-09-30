using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using Wordwright.App.Tray;

namespace Wordwright.App;

public partial class App : Application
{
    private const string MutexName = @"Local\Wordwright.SingleInstance";
    private const string ShowWindowEventName = @"Local\Wordwright.ShowMainWindow";
    private const int WM_SETTINGCHANGE = 0x001A;

    private Mutex? _mutex;
    private EventWaitHandle? _showWindowEvent;
    private MainWindow? _mainWindow;
    private TrayIconController? _trayIcon;

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

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _mainWindow = new MainWindow { Visibility = Visibility.Hidden };

        _trayIcon = new TrayIconController(this);

        // Refresh the tray glyph when Windows switches between light and dark.
        var handle = new WindowInteropHelper(_mainWindow).EnsureHandle();
        HwndSource.FromHwnd(handle)!.AddHook(OnWindowMessage);
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

    private IntPtr OnWindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_SETTINGCHANGE
            && Marshal.PtrToStringUni(lParam) == "ImmersiveColorSet")
        {
            Dispatcher.Invoke(_trayIcon!.RefreshIcon);
        }

        return IntPtr.Zero;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();

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