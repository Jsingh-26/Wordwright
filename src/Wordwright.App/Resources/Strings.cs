using System.Globalization;
using System.Resources;

namespace Wordwright.App.Resources;

/// <summary>
/// Accesses user-facing strings from Resources/Strings.resx. Every string must
/// exist in docs/UX_COPY.md; nothing user-facing is written inline.
/// </summary>
internal static class Strings
{
    private static readonly ResourceManager Manager =
        new("Wordwright.App.Resources.Strings", typeof(Strings).Assembly);

    internal static string Get(string id) =>
        Manager.GetString(id, CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException($"Missing string '{id}' in Strings.resx.");

    /// <summary>Gets the string and replaces {Name} placeholders with the given values.</summary>
    internal static string Get(string id, params (string Name, object Value)[] args)
    {
        var text = Get(id);
        foreach (var (name, value) in args)
        {
            text = text.Replace("{" + name + "}", value.ToString(), StringComparison.Ordinal);
        }

        return text;
    }
}