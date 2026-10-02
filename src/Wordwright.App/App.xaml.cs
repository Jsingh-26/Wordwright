using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wordwright.App.Snippets;
using Wordwright.App.Tray;
using Wordwright.Core.Settings;
using Wordwright.Core.Snippets;
using Microsoft.Win32;
using Velopack;
using Wordwright.Platform.Input;
using Wordwright.Platform.Keyboard;
using Wordwright.Platform.Startup;

namespace Wordwright.App;

public partial class App : Application
{
    private const string MutexName = @"Local\Wordwright.SingleInstance";
    private const string ShowWindowEventName = @"Local\Wordwright.ShowMainWindow";
    private const int WM_SETTINGCHANGE = 0x001A;

    private static readonly Color ForgeInk = Color.FromRgb(0x23, 0x40, 0x8E);
    private static readonly Color InkLight = Color.FromRgb(0xA4, 0xB6, 0xF0);
    private static readonly Color Ochre = Color.FromRgb(0x9A, 0x5B, 0x00);
    private static readonly Color Steel = Color.FromRgb(0xE9, 0xEC, 0xF3);
    private static readonly Color Anvil = Color.FromRgb(0x1A, 0x20, 0x30);
    private static readonly Color OchreDarkTheme = Color.FromRgb(0xE8, 0xB4, 0x5A);

    /// <summary>
    /// The first thing the generated entry point does, before WPF loads its
    /// resources or a window exists. The installer runs the app with its own
    /// arguments to finish an install, an update or an uninstall, and Velopack
    /// has to see those first (docs/PLAN.md P3.5). Velopack's packer would
    /// rather this sat in a hand-written <c>Main</c>, but the WPF SDK's XAML pass
    /// rejects a second entry point, and a constructor runs in the same place.
    /// Nothing here ever checks for or downloads an update on its own.
    /// </summary>
    public App()
    {
        VelopackApp.Build().Run();
    }

    private Mutex? _mutex;
    private EventWaitHandle? _showWindowEvent;
    private MainWindow? _mainWindow;
    private TrayIconController? _trayIcon;
    private SettingsStore _settingsStore = null!;
    private SnippetStore _snippetStore = null!;
    private ClipboardService? _clipboard;
    private KeyboardHook? _keyboardHook;
    private SnippetEngine? _snippetEngine;

    /// <summary>Whether SystemThemeWatcher is currently attached, so we only ever
    /// unwatch a window it is watching (UnWatch throws on an unwatched window).</summary>
    private bool _themeWatched;

    /// <summary>The user's settings; changes go through <see cref="UpdateSettings"/>.</summary>
    internal AppSettings Settings { get; private set; } = null!;

    /// <summary>The user's snippets, as last loaded or saved.</summary>
    internal SnippetDocument Snippets { get; private set; } = null!;

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
            callBack: (_, _) => Dispatcher.Invoke(() => ShowMainWindow()),
            state: null,
            millisecondsTimeOutInterval: Timeout.Infinite,
            executeOnlyOnce: false);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var userData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Wordwright");

        _settingsStore = new SettingsStore(userData);
        Settings = _settingsStore.Load();
        StartWithWindows.Apply(Settings.StartWithWindows, Environment.ProcessPath!);

        _snippetStore = new SnippetStore(userData);
        var firstRun = !File.Exists(Path.Combine(userData, "snippets.json"));
        Snippets = _snippetStore.LoadOrSeed();

        _mainWindow = new MainWindow { Visibility = Visibility.Hidden };

        _trayIcon = new TrayIconController(this);

        StartSnippetEngine();

        if (firstRun)
        {
            // The welcome replaces the empty tray on the very first launch.
            new WelcomeWindow().Show();
        }

        // The theme chosen in Settings decides the source: System follows Windows
        // through SystemThemeWatcher; Light/Dark pin one theme (docs/PLAN.md P11.2).
        // Changed fires after every apply, which is where we re-assert the brand
        // accent (DESIGN.md: Forge ink in light theme, Ink light in dark).
        ApplicationThemeManager.Changed += OnApplicationThemeChanged;
        ApplyTheme();

        // Refresh the tray glyph when Windows switches between light and dark.
        var handle = new WindowInteropHelper(_mainWindow).EnsureHandle();
        HwndSource.FromHwnd(handle)!.AddHook(OnWindowMessage);

        // Windows can drop a low-level hook around sleep and a session lock
        // without telling us, so put it back each time (docs/PLAN.md P12.6).
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionSwitch += OnSessionSwitch;
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            Dispatcher.BeginInvoke(ReinstallHook);
        }
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason is SessionSwitchReason.SessionUnlock or SessionSwitchReason.ConsoleConnect
            or SessionSwitchReason.RemoteConnect)
        {
            Dispatcher.BeginInvoke(ReinstallHook);
        }
    }

    private void ReinstallHook()
    {
        if (Settings.SnippetsEnabled && _keyboardHook is not null)
        {
            _keyboardHook.Restart();
            _trayIcon?.RefreshTooltip();
        }
    }

    /// <summary>True when snippets are on but Windows would not install the
    /// keyboard hook, so nothing can expand; the tray says so.</summary>
    internal bool HookRefused => Settings.SnippetsEnabled && _keyboardHook is { IsInstalled: false };

    private static void OnApplicationThemeChanged(ApplicationTheme currentTheme, Color systemAccent)
    {
        var accent = currentTheme == ApplicationTheme.Dark ? InkLight : ForgeInk;

        // WPF-UI raises this event while it is still swapping theme dictionaries,
        // and its own accent pass (the user's Windows accent) lands after that, so
        // wait for the dispatcher to go quiet before claiming the accent.
        Current.Dispatcher.BeginInvoke(
            () =>
            {
                ApplicationAccentColorManager.Apply(accent, currentTheme);

                // WPF-UI rewrites its accent *colours* from the user's Windows accent
                // whenever it applies a theme, and that pass lands after this event.
                // Its accent-filled buttons read these brushes, which WPF-UI never
                // rewrites, so the brand accent wins whatever the Windows accent is.
                Current.Resources["AccentFillColorDefault"] = accent;
                Current.Resources["AccentButtonBackground"] = new SolidColorBrush(accent);
                Current.Resources["AccentButtonBackgroundPointerOver"] =
                    new SolidColorBrush(Color.FromArgb(0xE6, accent.R, accent.G, accent.B));
                Current.Resources["AccentButtonBackgroundPressed"] =
                    new SolidColorBrush(Color.FromArgb(0xCC, accent.R, accent.G, accent.B));

                // List selection is a painted surface, not the accent: docs/DESIGN.md
                // gives Steel for it in light theme, and the same tint reads well in
                // dark theme at low opacity.
                Current.Resources["SelectionBrush"] = new SolidColorBrush(
                    currentTheme == ApplicationTheme.Dark
                        ? Color.FromArgb(0x1F, Steel.R, Steel.G, Steel.B)
                        : Steel);

                // The shortcut chip (docs/PLAN.md → P11.5) sits a touch lighter
                // than the selection it lives on, so it still reads when a row is
                // chosen: Steel at a lower alpha in dark, a light tint in light.
                Current.Resources["ChipBrush"] = new SolidColorBrush(
                    currentTheme == ApplicationTheme.Dark
                        ? Color.FromArgb(0x14, Steel.R, Steel.G, Steel.B)
                        : Color.FromArgb(0x33, ForgeInk.R, ForgeInk.G, ForgeInk.B));

                Current.Resources["BrandAccentBrush"] = new SolidColorBrush(accent);
                Current.Resources["HeroPaperBrush"] = new SolidColorBrush(
                    currentTheme == ApplicationTheme.Dark ? Anvil : Colors.White);
                Current.Resources["CautionBrush"] = new SolidColorBrush(
                    currentTheme == ApplicationTheme.Dark ? OchreDarkTheme : Ochre);
            },
            DispatcherPriority.ContextIdle);
    }

    /// <summary>Applies the theme chosen in Settings (docs/PLAN.md P11.2). System
    /// re-attaches the OS watcher; Light and Dark detach it and pin one theme, so a
    /// later Windows theme change leaves the app alone. The tray keeps following
    /// the taskbar theme regardless (P11.3).</summary>
    internal void ApplyTheme()
    {
        if (_mainWindow is null)
        {
            return;
        }

        var followsSystem = Settings.Theme is not ("light" or "dark");

        // Only detach the watcher if it is attached; UnWatch throws otherwise, and
        // at startup the window is still hidden and has never been watched.
        if (_themeWatched)
        {
            SystemThemeWatcher.UnWatch(_mainWindow);
            _themeWatched = false;
        }

        if (followsSystem)
        {
            SystemThemeWatcher.Watch(_mainWindow, WindowBackdropType.Mica, updateAccents: false);
            _themeWatched = true;
        }

        // Apply explicitly rather than relying on Watch: the watcher keeps us
        // following later OS changes, but it does not re-apply on its own when it
        // is (re-)attached mid-session, so switching back to System would stick.
        var theme = Settings.Theme switch
        {
            "light" => ApplicationTheme.Light,
            "dark" => ApplicationTheme.Dark,
            _ => ResolveSystemTheme(),
        };
        ApplicationThemeManager.Apply(theme, WindowBackdropType.Mica, updateAccent: false);
    }

    /// <summary>Maps the OS theme to one of ours, so System can be applied
    /// without waiting for the next OS theme change.</summary>
    private static ApplicationTheme ResolveSystemTheme()
    {
        if (ApplicationThemeManager.IsSystemHighContrast())
        {
            return ApplicationTheme.HighContrast;
        }

        // Qualified: the tray has its own SystemTheme enum (Tray/SystemTheme.cs).
        return ApplicationThemeManager.GetSystemTheme() == Wpf.Ui.Appearance.SystemTheme.Light
            ? ApplicationTheme.Light
            : ApplicationTheme.Dark;
    }

    /// <summary>Persists new settings and applies anything with an external
    /// side effect (the Windows Run entry, and turning snippets on or off).</summary>
    internal void UpdateSettings(AppSettings settings)
    {
        if (settings.StartWithWindows != Settings.StartWithWindows)
        {
            StartWithWindows.Apply(settings.StartWithWindows, Environment.ProcessPath!);
        }

        var snippetsToggled = settings.SnippetsEnabled != Settings.SnippetsEnabled;
        Settings = settings;

        if (snippetsToggled)
        {
            ApplySnippetsEnabled();
        }

        _settingsStore.Save(settings);
    }

    /// <summary>Starts the keystroke watching that expands snippets, unless the
    /// user has switched snippets off.</summary>
    private void StartSnippetEngine()
    {
        _clipboard = new ClipboardService();
        _keyboardHook = new KeyboardHook(Settings.ExcludedApps);
        _snippetEngine = new SnippetEngine(_keyboardHook, _clipboard, Dispatcher);
        _snippetEngine.Apply(Snippets);

        ApplySnippetsEnabled();
    }

    private void ApplySnippetsEnabled()
    {
        if (_keyboardHook is null)
        {
            return;
        }

        if (Settings.SnippetsEnabled)
        {
            _keyboardHook.Start();
        }
        else
        {
            _keyboardHook.Stop();
        }

        _trayIcon?.RefreshTooltip();
    }

    /// <summary>Replaces the snippets the engine matches against.</summary>
    internal void UpdateSnippets(SnippetDocument document)
    {
        var prefixChanged = document.TriggerPrefix != Snippets.TriggerPrefix;
        Snippets = document;
        _snippetStore.Save(document);
        _snippetEngine?.Apply(document);

        if (prefixChanged)
        {
            _trayIcon?.RefreshTooltip();
        }
    }

    /// <summary>The user's data folder, for "Open data folder" and the stores.</summary>
    internal string UserDataFolder => _settingsStore.DirectoryPath;

    /// <summary>The snippet engine, so a window can tell when a snippet expanded.
    /// It is rebuilt when the excluded apps change, so read it each time.</summary>
    internal SnippetEngine? SnippetEngine => _snippetEngine;

    /// <summary>Rebuilds the keystroke watching after the excluded apps change:
    /// the hook takes its list once, when it is built.</summary>
    internal void ApplyExcludedApps()
    {
        if (_keyboardHook is null)
        {
            return;
        }

        _snippetEngine?.Dispose();
        _keyboardHook.Dispose();
        _keyboardHook = null;
        _snippetEngine = null;

        StartSnippetEngine();
    }

    internal void ShowMainWindow(Type? pageType = null)
    {
        _mainWindow ??= new MainWindow();

        if (pageType is not null)
        {
            _mainWindow.ShowPage(pageType);
        }

        // Show() alone leaves a minimized window minimized, so restore it first.
        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }

        _mainWindow.Show();
        _mainWindow.Activate();
    }

    internal void Quit()
    {
        Shutdown();
    }

    /// <summary>Raised as the app exits, so an editor can write edits that are
    /// still waiting for the typing pause (docs/PLAN.md P12.4).</summary>
    internal event EventHandler? FlushingEdits;

    private IntPtr OnWindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_SETTINGCHANGE
            && Marshal.PtrToStringUni(lParam) == "ImmersiveColorSet")
        {
            Dispatcher.Invoke(_trayIcon!.RefreshTheme);
        }

        return IntPtr.Zero;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        FlushingEdits?.Invoke(this, EventArgs.Empty);

        // SystemEvents holds static references; let go of this instance.
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.SessionSwitch -= OnSessionSwitch;

        _snippetEngine?.Dispose();
        _keyboardHook?.Dispose();
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