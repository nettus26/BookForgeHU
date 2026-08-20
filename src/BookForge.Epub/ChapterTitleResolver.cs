using System;
using System.Net;
using System.Text.RegularExpressions;

namespace BookForge.Epub;

public class ChapterTitleResolver
{
    public string Resolve(
        string html,
        string fallbackTitle)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return CleanCandidate(fallbackTitle);
        }

        // 1. A fejezetben szereplő valódi címsor.
        // EPUB-okban nem csak h1-h3 fordul elő, ezért h1-h6-ot vizsgálunk.
        var headingMatches =
            Regex.Matches(
                html,
                @"<h[1-6]\b[^>]*>(.*?)</h[1-6]>",
                RegexOptions.IgnoreCase |
                RegexOptions.Singleline);

        foreach (Match match in headingMatches)
        {
            var title =
                CleanCandidate(match.Groups[1].Value);

            if (IsUsableChapterTitle(title, fallbackTitle))
            {
                return title;
            }
        }

        // 2. Olyan p/div elem, amelynek class vagy id attribútuma
        // kifejezetten címre/fejezetre utal.
        var semanticTitleMatches =
            Regex.Matches(
                html,
                @"<(p|div|section)\b[^>]*(?:class|id)\s*=\s*[""'][^""']*(?:chapter|title|heading|fejezet)[^""']*[""'][^>]*>(.*?)</\1>",
                RegexOptions.IgnoreCase |
                RegexOptions.Singleline);

        foreach (Match match in semanticTitleMatches)
        {
            var title =
                CleanCandidate(match.Groups[2].Value);

            if (IsUsableChapterTitle(title, fallbackTitle))
            {
                return title;
            }
        }

        // 3. Kifejezetten fejezetcímként megírt bekezdés.
        // Egyes EPUB-ok nem h1-h6 elemet használnak, hanem egyszerű
        // <p>-t, például: "TIZENKILENCEDIK FEJEZET".
        var paragraphTitleMatches =
            Regex.Matches(
                html,
                @"<(p|div|section)\\b[^>]*>(.*?)</\\1>",
                RegexOptions.IgnoreCase |
                RegexOptions.Singleline);

        foreach (Match match in paragraphTitleMatches)
        {
            var title =
                CleanCandidate(match.Groups[2].Value);

            // Magyar és angol EPUB-oknál is kezeljük a tipikus
            // "X fejezet" / "Chapter X" formát.
            if (Regex.IsMatch(
                    title,
                    @"^(?:[\\p{L}\\d]+(?:[\\s-]+[\\p{L}\\d]+)*\\s+fejezet|chapter\\s+[\\p{L}\\d]+)$",
                    RegexOptions.IgnoreCase)
                && IsUsableChapterTitle(
                    title,
                    fallbackTitle))
            {
                return title;
            }
        }

        // 4. A korábbi számozott fejezetforma.
        var paragraphs =
            Regex.Matches(
                html,
                @"<p\b[^>]*>(.*?)</p>",
                RegexOptions.IgnoreCase |
                RegexOptions.Singleline);

        if (paragraphs.Count >= 2)
        {
            var firstText =
                CleanCandidate(
                    paragraphs[0].Groups[1].Value);

            var secondText =
                CleanCandidate(
                    paragraphs[1].Groups[1].Value);

            if (Regex.IsMatch(
                    firstText,
                    @"^\d{1,4}$") &&
                IsUsableChapterTitle(
                    secondText,
                    fallbackTitle) &&
                secondText.Length <= 150)
            {
                return $"{firstText} {secondText}";
            }
        }

        // 5. <title> csak akkor, ha nem a könyv általános címe.
        var pageTitle =
            Regex.Match(
                html,
                @"<title\b[^>]*>(.*?)</title>",
                RegexOptions.IgnoreCase |
                RegexOptions.Singleline);

        if (pageTitle.Success)
        {
            var title =
                CleanCandidate(
                    pageTitle.Groups[1].Value);

            if (IsUsableChapterTitle(
                    title,
                    fallbackTitle))
            {
                return title;
            }
        }

        // 6. Ha nincs felismerhető cím, a fallback marad.
        return CleanCandidate(fallbackTitle);
    }


    private static bool IsUsableChapterTitle(
        string title,
        string fallbackTitle)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        if (LooksLikeBookTitle(
                title,
                fallbackTitle))
        {
            return false;
        }

        // Ne kerüljön egy teljes bekezdés a TOC-ba.
        if (title.Length > 180)
        {
            return false;
        }

        return true;
    }


    private static bool LooksLikeBookTitle(
        string title,
        string fallbackTitle)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return true;
        }

        var normalizedTitle =
            NormalizeForComparison(title);

        if (!string.IsNullOrWhiteSpace(fallbackTitle))
        {
            var normalizedFallback =
                NormalizeForComparison(fallbackTitle);

            if (normalizedTitle == normalizedFallback)
            {
                return true;
            }
        }

        return false;
    }


    private static string CleanCandidate(
        string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var result =
            Regex.Replace(
                text,
                @"<script\b[^>]*>.*?</script>",
                string.Empty,
                RegexOptions.IgnoreCase |
                RegexOptions.Singleline);

        result =
            Regex.Replace(
                result,
                @"<style\b[^>]*>.*?</style>",
                string.Empty,
                RegexOptions.IgnoreCase |
                RegexOptions.Singleline);

        result =
            Regex.Replace(
                result,
                "<.*?>",
                string.Empty,
                RegexOptions.Singleline);

        result =
            WebUtility.HtmlDecode(
                result);

        result =
            Regex.Replace(
                result,
                @"\s+",
                " ");

        return result.Trim();
    }


    private static string NormalizeForComparison(
        string text)
    {
        var result =
            CleanCandidate(text);

        result =
            Regex.Replace(
                result,
                @"[^\p{L}\p{N}]+",
                " ");

        return result
            .Trim()
            .ToLowerInvariant();
    }
}
