using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;

namespace Wordwright.App;

/// <summary>
/// Walking the visual tree, which is the only way to reach controls WPF-UI
/// builds inside its own control templates.
/// </summary>
internal static class VisualTree
{
    internal static IEnumerable<DependencyObject> Descendants(this DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);

            yield return child;

            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    /// <summary>
    /// The first descendant a template calls <paramref name="part"/> — how a
    /// template's own controls are reached from outside, since they have no
    /// fields of their own. WPF-UI names some parts and gives others only an
    /// automation id, so either counts.
    /// </summary>
    internal static UIElement? Part(this DependencyObject root, string part) =>
        root.Descendants()
            .OfType<FrameworkElement>()
            .FirstOrDefault(element =>
                element.Name == part || AutomationProperties.GetAutomationId(element) == part);
}
