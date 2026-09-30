using System.Drawing;
using System.IO;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using Wordwright.App.Resources;

namespace Wordwright.App.Tray;

/// <summary>
/// Owns the tray icon and its menu (docs/DESIGN.md §1). The glyph colour
/// follows the taskbar theme; the menu opens the window and quits the app.
/// </summary>
internal sealed class TrayIconController : IDisposable
{
    // Defaults from docs/ARCHITECTURE.md settings.json; SettingsStore owns them from P1.4.
    private const string DefaultSnippetPrefix = ";";
    private const string DefaultPaletteHotkey = "Ctrl+Alt+Space";

    private readonly App _app;
    private readonly TaskbarIcon _trayIcon;
    private readonly MenuItem _aiItem;
    private Icon? _currentIcon;

    // Display state for the menu checkmarks; both persist via SettingsStore from P1.4.
    internal bool SnippetsOn { get; set; } = true;
    internal bool AiOn { get; private set; }

    public TrayIconController(App app)
    {
        _app = app;

        var openItem = new MenuItem { Header = Strings.Get("Tray.Open") };
        openItem.Click += (_, _) => _app.ShowMainWindow();

        var snippetsItem = new MenuItem
        {
            Header = Strings.Get("Tray.SnippetsOn"),
            IsCheckable = true,
            IsChecked = SnippetsOn,
        };
        snippetsItem.Click += (_, _) => SnippetsOn = snippetsItem.IsChecked;

        _aiItem = new MenuItem
        {
            Header = Strings.Get("Tray.TurnOnAi"),
            IsCheckable = true,
            IsChecked = AiOn,
        };
        _aiItem.Click += OnAiItemClicked;

        var quitItem = new MenuItem { Header = Strings.Get("Tray.Quit") };
        quitItem.Click += (_, _) => _app.Quit();

        var menu = new ContextMenu();
        menu.Items.Add(openItem);
        menu.Items.Add(snippetsItem);
        menu.Items.Add(_aiItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(quitItem);

        _trayIcon = new TaskbarIcon
        {
            ToolTipText = Strings.Get(
                "Tray.Tooltip",
                ("Prefix", DefaultSnippetPrefix),
                ("PaletteHotkey", DefaultPaletteHotkey)),
            ContextMenu = menu,
            LeftClickCommand = new RelayCommand(() => _app.ShowMainWindow()),
        };

        RefreshIcon();
        // Efficiency mode (the bool argument) stays off: the keyboard hook (phase P2)
        // must stay responsive, and EcoQoS would slow it down.
        _trayIcon.ForceCreate(false);
    }

    /// <summary>Re-renders the glyph after the taskbar theme changes.</summary>
    internal void RefreshIcon()
    {
        var glyph = SystemTheme.TaskbarUsesLightTheme()
            ? MarkRenderer.LightTaskbarGlyph
            : MarkRenderer.DarkTaskbarGlyph;

        var icoBytes = MarkRenderer.RenderTrayIconIco(glyph);
        using var stream = new MemoryStream(icoBytes);
        var newIcon = new Icon(stream, 32, 32);
        _trayIcon.Icon = newIcon;
        _currentIcon?.Dispose();
        _currentIcon = newIcon;
    }

    private void OnAiItemClicked(object sender, System.Windows.RoutedEventArgs e)
    {
        if (AiOn)
        {
            AiOn = _aiItem.IsChecked;
            return;
        }

        // AI is not downloadable yet (phase P5); until the consent dialogue exists,
        // opening the window is the nearest honest action.
        _aiItem.IsChecked = false;
        _app.ShowMainWindow();
    }

    public void Dispose()
    {
        _trayIcon.Dispose();
    }
}