using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using Wordwright.App.Resources;
using MediaBrush = System.Windows.Media.SolidColorBrush;
using MediaColor = System.Windows.Media.Color;

namespace Wordwright.App.Tray;

/// <summary>
/// Owns the tray icon and its menu (docs/DESIGN.md §1). The glyph and the menu
/// colours both follow the taskbar theme; the menu opens the window and quits.
/// </summary>
internal sealed class TrayIconController : IDisposable
{
    // Palette (docs/DESIGN.md). The menu derives its surfaces from these with
    // alpha overlays, resolved at apply time because the menu has no owner
    // window and cannot read the app-theme brushes.
    private static readonly MediaColor ForgeInk = MediaColor.FromRgb(0x23, 0x40, 0x8E);
    private static readonly MediaColor Steel = MediaColor.FromRgb(0xE9, 0xEC, 0xF3);
    private static readonly MediaColor Anvil = MediaColor.FromRgb(0x1A, 0x20, 0x30);

    private readonly App _app;
    private readonly TaskbarIcon _trayIcon;
    private readonly ResourceDictionary _menuResources;
    private readonly AcrylicContextMenu _menu;
    private Icon? _currentIcon;

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

        var quitItem = new MenuItem { Header = Strings.Get("Tray.Quit") };
        quitItem.Click += (_, _) => _app.Quit();

        // The styled menu lives in its own dictionary, merged only here, so it
        // never touches the app window's resources (docs/P11_CRAFT_PASS.md).
        _menuResources = new ResourceDictionary
        {
            Source = new Uri(
                "pack://application:,,,/Wordwright.App;component/Tray/TrayMenu.xaml",
                UriKind.Absolute),
        };

        _menu = new AcrylicContextMenu();
        _menu.Resources.MergedDictionaries.Add(_menuResources);
        _menu.Items.Add(openItem);
        _menu.Items.Add(snippetsItem);
        _menu.Items.Add(new Separator());
        _menu.Items.Add(quitItem);

        _trayIcon = new TaskbarIcon
        {
            ToolTipText = Strings.Get(
                "Tray.Tooltip",
                ("Prefix", _app.Snippets.TriggerPrefix)),
            ContextMenu = _menu,
            LeftClickCommand = new RelayCommand(() => _app.ShowMainWindow()),
        };

        RefreshTheme();
        // Efficiency mode (the bool argument) stays off: the keyboard hook must
        // stay responsive, and EcoQoS would slow it down.
        _trayIcon.ForceCreate(false);
    }

    /// <summary>Shows the current prefix in the tooltip, or says that snippets
    /// are not working when Windows refused the keyboard hook (P12.6).</summary>
    internal void RefreshTooltip()
    {
        _trayIcon.ToolTipText = _app.HookRefused
            ? Strings.Get("Tray.Tooltip.HookRefused")
            : Strings.Get("Tray.Tooltip", ("Prefix", _app.Snippets.TriggerPrefix));
    }

    /// <summary>Re-skins the glyph and the menu after the taskbar theme changes.</summary>
    internal void RefreshTheme()
    {
        RefreshIcon();
        ApplyMenuPalette();
    }

    private void RefreshIcon()
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

    /// <summary>Sets the menu's concrete colours for the current taskbar theme:
    /// dark taskbar sits on Anvil with Steel text; light taskbar sits on Steel
    /// with Anvil text. Hover and separators are 8 % and 12 % overlays. On the
    /// Acrylic material (P13.14) the surface is a 60 % tint over it, and the
    /// border is Windows' own.</summary>
    private void ApplyMenuPalette()
    {
        var light = SystemTheme.TaskbarUsesLightTheme();
        var text = light ? Anvil : Steel;
        var tint = light ? ForgeInk : Steel;
        var surface = light ? Steel : Anvil;
        var acrylic = AcrylicContextMenu.IsSupported;

        _menu.UseDarkMaterial = !light;
        Set("TrayMenuBackground", acrylic ? WithAlpha(surface, 0x99) : surface);
        Set("TrayMenuForeground", text);
        Set("TrayMenuHover", WithAlpha(tint, 0x14));
        Set("TrayMenuSeparator", WithAlpha(tint, 0x1F));
        Set("TrayMenuBorder", WithAlpha(tint, acrylic ? (byte)0 : (byte)0x1F));
    }

    private void Set(string key, MediaColor color) => _menuResources[key] = new MediaBrush(color);

    private static MediaColor WithAlpha(MediaColor color, byte alpha) =>
        MediaColor.FromArgb(alpha, color.R, color.G, color.B);

    public void Dispose()
    {
        _trayIcon.Dispose();
    }
}