using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wordwright.App.Tray;
using Wordwright.Core.Settings;
using Wordwright.Platform.Startup;

namespace Wordwright.App;

public partial class App : Application
{
    private const string MutexName = @"Local\Wordwright.SingleInstance";
    private const string ShowWindowEventName = @"Local\Wordwright.ShowMainWindow";
    private const int WM_SETTINGCHANGE = 0x001A;

    private static readonly Color ForgeInk = Color.FromRgb(0x23, 0x40, 0x8E);
    private static readonly Color InkLight = Color.FromRgb(0xA4, 0xB6, 0xF0);

    private Mutex? _mutex;
    private EventWaitHandle? _showWindowEvent;
    private MainWindow? _mainWindow;
    private TrayIconController? _trayIcon;
    private SettingsStore _settingsStore = null!;

    /// <summary>The user's settings; changes go through <see cref="UpdateSettings"/>.</summary>
    internal AppSettings Settings { get; private set; } = null!;

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

        _settingsStore = new SettingsStore(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Wordwright"));
        Settings = _settingsStore.Load();
        StartWithWindows.Apply(Settings.StartWithWindows, Environment.ProcessPath!);

        _mainWindow = new MainWindow { Visibility = Visibility.Hidden };

        _trayIcon = new TrayIconController(this);

        // Theme follows the system. SystemThemeWatcher applies the theme, and
        // Changed fires after every apply, which is where we re-assert the brand
        // accent (DESIGN.md: Forge ink in light theme, Ink light in dark).
        ApplicationThemeManager.Changed += OnApplicationThemeChanged;
        SystemThemeWatcher.Watch(_mainWindow, WindowBackdropType.Mica, updateAccents: false);

        // Refresh the tray glyph when Windows switches between light and dark.
        var handle = new WindowInteropHelper(_mainWindow).EnsureHandle();
        HwndSource.FromHwnd(handle)!.AddHook(OnWindowMessage);
    }

    private static void OnApplicationThemeChanged(ApplicationTheme currentTheme, Color systemAccent)
    {
        var accent = currentTheme == ApplicationTheme.Dark ? InkLight : ForgeInk;
        ApplicationAccentColorManager.Apply(accent, currentTheme);
    }

    /// <summary>Persists new settings and applies anything with an external
    /// side effect (today: the Windows Run entry).</summary>
    internal void UpdateSettings(AppSettings settings)
    {
        if (settings.StartWithWindows != Settings.StartWithWindows)
        {
            StartWithWindows.Apply(settings.StartWithWindows, Environment.ProcessPath!);
        }

        Settings = settings;
        _settingsStore.Save(settings);
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