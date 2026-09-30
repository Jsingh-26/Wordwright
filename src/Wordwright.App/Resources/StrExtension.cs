using System.Windows.Markup;

namespace Wordwright.App.Resources;

/// <summary>
/// XAML markup extension for user-facing strings:
/// <c>{res:Str Snippets.Title}</c>. The strings live in Strings.resx
/// (docs/UX_COPY.md); nothing user-facing is written inline.
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class StrExtension : MarkupExtension
{
    private readonly string _id;

    public StrExtension(string id)
    {
        _id = id;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return Strings.Get(_id);
    }
}