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
    private readonly App _app;
    private readonly TaskbarIcon _trayIcon;
    private readonly MenuItem _aiItem;
    private Icon? _currentIcon;

    /// <summary>Offline AI is not built yet (phase P5), so this stays false.</summary>
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
            IsChecked = _app.Settings.SnippetsEnabled,
        };
        snippetsItem.Click += (_, _) => _app.UpdateSettings(
            _app.Settings with { SnippetsEnabled = snippetsItem.IsChecked });

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
                ("Prefix", _app.Snippets.TriggerPrefix),
                ("PaletteHotkey", _app.Settings.PaletteHotkey)),
            ContextMenu = menu,
            LeftClickCommand = new RelayCommand(() => _app.ShowMainWindow()),
        };

        RefreshIcon();
        // Efficiency mode (the bool argument) stays off: the keyboard hook must
        // stay responsive, and EcoQoS would slow it down.
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

        // AI is off, so ask first: the dialogue says what would be downloaded and
        // what it costs in time before anything is fetched (docs/PLAN.md P5.1).
        _aiItem.IsChecked = false;
        new ConsentWindow().Show();
    }

    public void Dispose()
    {
        _trayIcon.Dispose();
    }
}