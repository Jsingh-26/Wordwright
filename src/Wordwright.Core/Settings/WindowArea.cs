namespace Wordwright.Core.Settings;

/// <summary>
/// A screen's work area in device-independent pixels — the part of the screen
/// Windows leaves to applications, with the taskbar taken out. It exists so the
/// window-placement rule can live in Core, away from WPF's <c>Rect</c>.
/// </summary>
public readonly record struct WindowArea(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;

    public double Bottom => Top + Height;
}
