using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wordwright.App.Ai;
using Wordwright.App.Snippets;
using Wordwright.App.Tray;
using Wordwright.Core.Actions;
using Wordwright.Core.Settings;
using Wordwright.Core.Snippets;
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
    private ActionStore _actionStore = null!;
    private ClipboardService? _clipboard;
    private KeyboardHook? _keyboardHook;
    private SnippetEngine? _snippetEngine;

    /// <summary>The user's settings; changes go through <see cref="UpdateSettings"/>.</summary>
    internal AppSettings Settings { get; private set; } = null!;

    /// <summary>The user's snippets, as last loaded or saved.</summary>
    internal SnippetDocument Snippets { get; private set; } = null!;

    /// <summary>The user's AI actions, as last loaded or saved.</summary>
    internal ActionDocument Actions { get; private set; } = null!;

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

        _actionStore = new ActionStore(userData);
        Actions = _actionStore.LoadOrSeed();

        // Registers the palette hotkey (Ctrl+Alt+Space by default) as soon as the
        // app is running; the model itself is not loaded until a rewrite is asked
        // for (docs/ARCHITECTURE.md → Hotkeys, Model lifecycle).
        _ai = new AiCoordinator(this);

        _mainWindow = new MainWindow { Visibility = Visibility.Hidden };

        _trayIcon = new TrayIconController(this);

        StartSnippetEngine();

        if (firstRun)
        {
            // The welcome replaces the empty tray on the very first launch.
            new WelcomeWindow().Show();
        }

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

                Current.Resources["BrandAccentBrush"] = new SolidColorBrush(accent);
                Current.Resources["CautionBrush"] = new SolidColorBrush(
                    currentTheme == ApplicationTheme.Dark ? OchreDarkTheme : Ochre);
            },
            DispatcherPriority.ContextIdle);
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

        // The palette hotkey may have changed; re-register.
        ApplyHotkeys();
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
    }

    /// <summary>Replaces the snippets the engine matches against.</summary>
    internal void UpdateSnippets(SnippetDocument document)
    {
        Snippets = document;
        _snippetStore.Save(document);
        _snippetEngine?.Apply(document);
    }

    /// <summary>Replaces the AI actions, e.g. after the AI actions page edits
    /// them.</summary>
    internal void UpdateActions(ActionDocument document)
    {
        Actions = document;
        _actionStore.Save(document);

        // Action hotkeys are registered again whenever the set changes (P6.7);
        // the palette list is read fresh each time it opens, so nothing else to do.
        ApplyHotkeys();
    }

    /// <summary>Resets a built-in action to its default and saves the result.</summary>
    internal ActionDocument ResetAction(string id)
    {
        Actions = _actionStore.Reset(Actions, id);
        return Actions;
    }

    private RewriteService? _rewrite;

    /// <summary>The one on-device model owner, built on first use so the app does
    /// not touch LLamaSharp until a rewrite is asked for.</summary>
    internal RewriteService Rewrite => _rewrite ??= new RewriteService(this);

    private AiCoordinator? _ai;

    /// <summary>Owns the AI hotkeys and palette, built on first use so a
    /// snippets-only install never registers a hotkey it does not need.</summary>
    internal AiCoordinator Ai => _ai ??= new AiCoordinator(this);

    /// <summary>Re-registers the AI hotkeys after settings or actions changed.</summary>
    internal void ApplyHotkeys() => _ai?.Apply();

    /// <summary>Whether an action's own hotkey could be registered. False means
    /// another app owns it (docs/ARCHITECTURE.md → Hotkeys).</summary>
    internal bool IsActionHotkeyRegistered(string actionId) =>
        _ai?.Hotkeys.IsRegistered(AiCoordinator.ActionId(actionId)) ?? false;

    /// <summary>Whether the palette hotkey could be registered.</summary>
    internal bool IsPaletteHotkeyRegistered =>
        _ai?.Hotkeys.IsRegistered(AiCoordinator.PaletteHotkeyId) ?? false;

    /// <summary>The user's data folder, for "Open data folder" and the stores.</summary>
    internal string UserDataFolder => _settingsStore.DirectoryPath;

    /// <summary>Where model files live: <c>%LocalAppData%\Wordwright\models</c>,
    /// which does not roam (docs/ARCHITECTURE.md → Data files).</summary>
    internal string ModelsFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Wordwright",
        "models");

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
        _ai?.Dispose();
        _rewrite?.Dispose();
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