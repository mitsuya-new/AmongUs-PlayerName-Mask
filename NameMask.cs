using System.Globalization;
using System.Text.RegularExpressions;

namespace PlayerNameMaskMod;

internal static class NameMask
{
    private static readonly Regex Tags = new(
        @"</?(?:b|i|u|s|color|size|alpha|align|font|font-weight|material|link|mark|sub|sup|voffset|cspace|mspace|indent|line-indent|line-height|margin|margin-left|margin-right|width|pos|rotate|nobr|noparse|lowercase|uppercase|smallcaps|style)(?:=[^<>]*)?>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static string VisibleName(string name, bool richText) => richText ? Tags.Replace(name, "") : name;
    internal static string Mask(string name, bool richText) =>
        new('*', StringInfo.ParseCombiningCharacters(VisibleName(name, richText)).Length);

    internal static string MaskDisplay(string text, bool richText, string? namePart)
    {
        if (!string.IsNullOrEmpty(namePart))
        {
            var tags = richText ? Tags.Matches(text).Cast<Match>().ToArray() : Array.Empty<Match>();
            int searchStart = 0;
            while (searchStart < text.Length)
            {
                int start = text.IndexOf(namePart, searchStart, StringComparison.Ordinal);
                if (start < 0) break;
                var containingTag = tags.FirstOrDefault(tag => tag.Index <= start && start < tag.Index + tag.Length);
                if (containingTag == null || namePart.StartsWith('<'))
                    return text[..start] + Mask(namePart, richText) + text[(start + namePart.Length)..];
                searchStart = containingTag.Index + containingTag.Length;
            }
        }
        return Mask(text, richText);
    }

    internal static bool IsOther(int? owner, int localOwner, string name, IReadOnlySet<string> ownNames) =>
        owner.HasValue ? owner.Value != localOwner : !ownNames.Contains(name);
}

internal sealed class MaskState
{
    internal string Original { get; private set; } = "";
    internal string? Applied { get; private set; }
    private string masked = "";
    private bool lastRichText;
    private string? lastNamePart;

    internal string Update(string current, bool richText, bool mask, string? namePart = null)
    {
        if (Applied == null || current != Applied)
        {
            if (Original != current || lastRichText != richText || lastNamePart != namePart || Applied == null)
                masked = NameMask.MaskDisplay(current, richText, namePart);
            Original = current;
        }
        else if (lastRichText != richText || lastNamePart != namePart)
            masked = NameMask.MaskDisplay(Original, richText, namePart);
        lastRichText = richText;
        lastNamePart = namePart;
        Applied = mask ? masked : Original;
        return Applied;
    }

    internal string Restore(string current)
    {
        string restored = Applied != null && current == Applied ? Original : current;
        Applied = null;
        return restored;
    }
}
