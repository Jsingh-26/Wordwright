using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls.Primitives;
using Wordwright.App.Resources;

namespace Wordwright.App;

/// <summary>
/// Names the controls WPF-UI builds inside its own control templates, which a
/// screen reader would otherwise read as a bare "button"
/// (docs/UX_COPY.md → Accessibility).
/// </summary>
internal static class Accessibility
{
    /// <summary>
    /// Names every scrollbar's page up and down buttons, which WPF leaves
    /// unnamed — a scrollbar's other buttons are named after their icons. The
    /// buttons live in the scrollbar's own template, which Windows only applies
    /// once the bar is needed, so it is asked for here. Horizontal bars are left
    /// alone: the pages never show one.
    /// </summary>
    internal static void NameScrollButtons(DependencyObject contentArea)
    {
        foreach (var scrollBar in contentArea.Descendants().OfType<ScrollBar>())
        {
            scrollBar.ApplyTemplate();

            // The two page buttons carry no name and no automation id, so their
            // command is the only thing that tells them apart.
            foreach (var button in scrollBar.Descendants().OfType<RepeatButton>())
            {
                if (button.Command == ScrollBar.PageUpCommand)
                {
                    Name(button, "A11y.PageUp");
                }
                else if (button.Command == ScrollBar.PageDownCommand)
                {
                    Name(button, "A11y.PageDown");
                }
            }
        }
    }

    internal static void Name(UIElement? element, string stringId)
    {
        if (element is not null)
        {
            AutomationProperties.SetName(element, Strings.Get(stringId));
        }
    }
}
