using System.Text.RegularExpressions;

namespace SoftwareEngineering.BLL.Services;

/// <summary>
/// Частина тексту: звичайний текст (Link = null) або посилання.
/// </summary>
public sealed record TextSegment(string Text, Uri? Link);

/// <summary>
/// Знаходить посилання в тексті (опис завдання), щоб UI зробив їх клікабельними.
/// Посиланнями вважаються лише http(s):// і www. — інші схеми (file://, javascript: тощо) лишаються текстом,
/// щоб клік не міг запустити локальний файл чи програму.
/// </summary>
public static partial class TextLinks
{
    // Розділові знаки в кінці зазвичай належать реченню, а не адресі: «див. https://site.com.»
    private static readonly char[] TrailingPunctuation = { '.', ',', ';', ':', '!', '?', '"', '\'', '»', ']', '}' };

    public static IReadOnlyList<TextSegment> Split(string? text)
    {
        var segments = new List<TextSegment>();
        if (string.IsNullOrEmpty(text))
        {
            return segments;
        }

        var position = 0;
        foreach (Match match in UrlCandidate().Matches(text))
        {
            var candidate = TrimTrailing(match.Value);
            if (!TryCreateLink(candidate, out var link))
            {
                continue;
            }

            if (match.Index > position)
            {
                segments.Add(new TextSegment(text[position..match.Index], null));
            }

            segments.Add(new TextSegment(candidate, link));
            position = match.Index + candidate.Length;
        }

        if (position < text.Length)
        {
            segments.Add(new TextSegment(text[position..], null));
        }

        return segments;
    }

    private static string TrimTrailing(string candidate)
    {
        var trimmed = candidate.TrimEnd(TrailingPunctuation);

        // Дужка в кінці — частина адреси лише тоді, коли в адресі є відкривна: (https://uk.wikipedia.org/wiki/C_(мова))
        while (trimmed.EndsWith(')') && trimmed.Count(c => c == ')') > trimmed.Count(c => c == '('))
        {
            trimmed = trimmed[..^1].TrimEnd(TrailingPunctuation);
        }

        return trimmed;
    }

    private static bool TryCreateLink(string candidate, out Uri? link)
    {
        var address = candidate.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? "https://" + candidate : candidate;
        if (Uri.TryCreate(address, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && uri.Host.Contains('.'))
        {
            link = uri;
            return true;
        }

        link = null;
        return false;
    }

    [GeneratedRegex(@"(?:https?://|www\.)[^\s<>""]+", RegexOptions.IgnoreCase)]
    private static partial Regex UrlCandidate();
}
