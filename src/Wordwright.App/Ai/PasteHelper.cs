using Wordwright.Platform.Input;

namespace Wordwright.App.Ai;

/// <summary>How a rewrite was delivered to the user.</summary>
internal enum RewriteDelivery
{
    /// <summary>Typed over the selection in the target app.</summary>
    Pasted,

    /// <summary>The target app is elevated, so the rewrite was put on the
    /// clipboard instead (docs/PLAN.md P6.6 → copy fallback, decision D5).</summary>
    Copied,
}

/// <summary>
/// Puts finished text back where it came from (docs/ARCHITECTURE.md → Paste and
/// selection capture). When the foreground app runs elevated, Windows would
/// ignore our paste, so the text goes on the clipboard and stays there — the one
/// case where the clipboard is not restored, because the copy is the result.
/// </summary>
internal static class PasteHelper
{
    /// <summary>How long our text stays on the clipboard after Ctrl+V.</summary>
    private const int SettleMilliseconds = 150;

    public static RewriteDelivery Deliver(ClipboardService clipboard, string text)
    {
        if (ElevationCheck.ForegroundIsElevated())
        {
            // Leave the rewrite on the clipboard, marked out of Win+V history.
            clipboard.SetText(text);
            return RewriteDelivery.Copied;
        }

        using (clipboard.ReplaceWithText(text))
        {
            InputSender.Paste();
            Thread.Sleep(SettleMilliseconds);
        }

        return RewriteDelivery.Pasted;
    }
}
