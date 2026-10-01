using Wordwright.App.Palette;
using Wordwright.Core.Actions;
using Wordwright.Platform.Hotkeys;

namespace Wordwright.App.Ai;

/// <summary>
/// Connects the hotkeys to the AI surface: the palette hotkey opens the palette
/// (docs/PLAN.md P6.5). The rewrite, the pill and running a chosen action are
/// P6.6; the per-action hotkeys and the recorder are P6.7. It re-registers
/// whenever settings or actions change (docs/ARCHITECTURE.md → Hotkeys).
/// </summary>
internal sealed class AiCoordinator : IDisposable
{
    /// <summary>The registration id of the palette hotkey.</summary>
    internal const string PaletteHotkeyId = "palette";

    private const string ActionIdPrefix = "action:";

    private readonly App _app;
    private readonly HotkeyService _hotkeys = new();

    private PaletteWindow? _palette;

    public AiCoordinator(App app)
    {
        _app = app;
        _hotkeys.Pressed += OnPressed;
        Apply();
    }

    public HotkeyService Hotkeys => _hotkeys;

    /// <summary>Re-registers the palette hotkey from the current settings.</summary>
    public void Apply()
    {
        var registrations = new List<HotkeyRegistration>();

        if (HotkeySpec.Parse(_app.Settings.PaletteHotkey) is { } palette)
        {
            registrations.Add(new HotkeyRegistration(PaletteHotkeyId, palette));
        }

        _hotkeys.Apply(registrations);
    }

    /// <summary>The registration id for an action's own hotkey.</summary>
    internal static string ActionId(string actionId) => ActionIdPrefix + actionId;

    private void OnPressed(object? sender, HotkeyPressedEventArgs e)
    {
        if (e.Id == PaletteHotkeyId)
        {
            OpenPalette();
        }
    }

    /// <summary>Opens the palette near the caret, or brings the open one forward.</summary>
    internal void OpenPalette()
    {
        if (_palette is { IsVisible: true })
        {
            _palette.Activate();
            return;
        }

        var palette = new PaletteWindow(
            _app.Actions.Actions,
            showAiOff: !_app.Settings.AiEnabled);

        _palette = palette;
        palette.Closed += (_, _) => _palette = null;
        palette.Show();
    }

    public void Dispose()
    {
        _hotkeys.Pressed -= OnPressed;
        _hotkeys.Dispose();
        _palette?.Close();
        _palette = null;
    }
}
