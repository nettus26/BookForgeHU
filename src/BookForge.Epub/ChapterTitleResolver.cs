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
            return fallbackTitle;
        }

        // =========================================================
        // 1. SZÁMOZOTT FEJEZET
        // =========================================================
        //
        // Az EPUB többféleképpen tárolhatja a fejezetszámot:
        //
        // <p>5</p>
        // <p>EGÉSZ</p>
        //
        // vagy:
        //
        // <p><span>5</span></p>
        // <p>EGÉSZ</p>
        //
        // Ezért nem a HTML szerkezetét, hanem az első két
        // bekezdés megtisztított szövegét vizsgáljuk.
        //

        var paragraphs =
            Regex.Matches(
                html,
                @"<p\b[^>]*>(.*?)</p>",
                RegexOptions.IgnoreCase |
                RegexOptions.Singleline);

        if (paragraphs.Count >= 2)
        {
            var firstText =
                CleanText(
                    paragraphs[0].Groups[1].Value);

            var secondText =
                CleanText(
                    paragraphs[1].Groups[1].Value);

            if (Regex.IsMatch(
                    firstText,
                    @"^\d{1,4}$") &&
                !string.IsNullOrWhiteSpace(
                    secondText) &&
                secondText.Length <= 150)
            {
                return $"{firstText} {secondText}";
            }
        }

        // =========================================================
        // 2. H1-H3 CÍM
        // =========================================================

        var heading =
            Regex.Match(
                html,
                @"<h[1-3]\b[^>]*>(.*?)</h[1-3]>",
                RegexOptions.IgnoreCase |
                RegexOptions.Singleline);

        if (heading.Success)
        {
            var title =
                CleanText(
                    heading.Groups[1].Value);

            if (!string.IsNullOrWhiteSpace(title))
            {
                return title;
            }
        }

        // =========================================================
        // 3. HTML <title>
        // =========================================================

        var pageTitle =
            Regex.Match(
                html,
                @"<title\b[^>]*>(.*?)</title>",
                RegexOptions.IgnoreCase |
                RegexOptions.Singleline);

        if (pageTitle.Success)
        {
            var title =
                CleanText(
                    pageTitle.Groups[1].Value);

            // Az EPUB minden oldalán ugyanaz a könyvcím szerepel.
            // Ezt nem tekintjük fejezetcímnek.
            if (!string.IsNullOrWhiteSpace(title) &&
                !LooksLikeBookTitle(
                    title,
                    fallbackTitle))
            {
                return title;
            }
        }

        // =========================================================
        // 4. FALLBACK
        // =========================================================

        return fallbackTitle;
    }


    private static bool LooksLikeBookTitle(
        string title,
        string fallbackTitle)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(fallbackTitle) &&
            title.Equals(
                fallbackTitle.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }


    private static string CleanText(
        string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var result =
            Regex.Replace(
                text,
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
}
