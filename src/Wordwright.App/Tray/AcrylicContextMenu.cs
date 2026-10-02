using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;

namespace Wordwright.App.Tray;

/// <summary>
/// A context menu on the Windows 11 transient material, Acrylic, the way the
/// system's own menus are (docs/PLAN.md P13.14): base window, then card, then
/// flyout. Windows rounds its corners and draws its shadow and border.
/// <para>
/// DWM puts a backdrop only behind an ordinary window, and WPF builds a context
/// menu's popup as a layered (transparent) window, afresh on every open. So the
/// popup is switched to an ordinary window before it is built, its client area
/// is made see-through, and the backdrop is asked for as each window appears.
/// The popup is private to <see cref="ContextMenu"/>; if a later WPF moves it,
/// or Windows is older than 11 22H2, the menu stays the solid one it was.
/// </para>
/// </summary>
internal sealed class AcrylicContextMenu : ContextMenu
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    private const int DWMWCP_ROUND = 2;
    private const int DWMSBT_TRANSIENTWINDOW = 3;

    /// <summary>Windows 11 22H2, the first build with system backdrops.</summary>
    private const int FirstBackdropBuild = 22621;

    private static readonly FieldInfo? PopupField =
        typeof(ContextMenu).GetField("_parentPopup", BindingFlags.Instance | BindingFlags.NonPublic);

    static AcrylicContextMenu()
    {
        // ContextMenu creates its popup, makes it transparent and then coerces
        // HasDropShadow, all before the popup's window exists: the one moment
        // the popup can still be told to build an ordinary window.
        HasDropShadowProperty.OverrideMetadata(
            typeof(AcrylicContextMenu),
            new FrameworkPropertyMetadata(false, null, CoerceHasDropShadow));
    }

    public AcrylicContextMenu()
    {
        // An implicit style matches the exact type, so ask for the
        // ContextMenu one in TrayMenu.xaml by name.
        SetResourceReference(StyleProperty, typeof(ContextMenu));
        PresentationSource.AddSourceChangedHandler(this, OnSourceChanged);
    }

    /// <summary>True where Windows can draw the material; elsewhere the menu
    /// keeps its solid background.</summary>
    public static bool IsSupported { get; } =
        PopupField is not null && Environment.OSVersion.Version.Build >= FirstBackdropBuild;

    /// <summary>Dark material for a dark taskbar, light for a light one.</summary>
    public bool UseDarkMaterial { get; set; } = true;

    private static object CoerceHasDropShadow(DependencyObject menu, object value)
    {
        if (IsSupported && PopupField!.GetValue(menu) is Popup { IsOpen: false } popup)
        {
            popup.AllowsTransparency = false;
        }

        // The template draws no shadow of its own; Windows adds one to the window.
        return false;
    }

    private void OnSourceChanged(object sender, SourceChangedEventArgs e)
    {
        if (!IsSupported || e.NewSource is not HwndSource source)
        {
            return;
        }

        var handle = source.Handle;
        source.CompositionTarget.BackgroundColor = Colors.Transparent;

        var margins = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        _ = DwmExtendFrameIntoClientArea(handle, ref margins);

        var dark = UseDarkMaterial ? 1 : 0;
        _ = DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));

        var corners = DWMWCP_ROUND;
        _ = DwmSetWindowAttribute(handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref corners, sizeof(int));

        var backdrop = DWMSBT_TRANSIENTWINDOW;
        _ = DwmSetWindowAttribute(handle, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr window, ref Margins margins);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}
