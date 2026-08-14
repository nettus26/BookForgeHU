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
            return fallbackTitle;

        // =========================================================
        // 1. H1-H6 FEJEZETCÍM
        // =========================================================

        var heading =
            Regex.Match(
                html,
                @"<h[1-6][^>]*>(.*?)</h[1-6]>",
                RegexOptions.IgnoreCase |
                RegexOptions.Singleline);

        if (heading.Success)
        {
            var title =
                CleanText(
                    heading.Groups[1].Value);

            if (IsValidChapterTitle(
                title,
                fallbackTitle))
            {
                return title;
            }
        }


        // =========================================================
        // 2. GYAKORI EPUB FEJEZETCÍM OSZTÁLYOK
        // =========================================================

        var classHeading =
            Regex.Match(
                html,
                @"<(?:div|p|span)[^>]*class\s*=\s*[""'][^""']*" +
                @"(?:chapter|chapter-title|title|heading)" +
                @"[^""']*[""'][^>]*>(.*?)</(?:div|p|span)>",
                RegexOptions.IgnoreCase |
                RegexOptions.Singleline);

        if (classHeading.Success)
        {
            var title =
                CleanText(
                    classHeading.Groups[1].Value);

            if (IsValidChapterTitle(
                title,
                fallbackTitle))
            {
                return title;
            }
        }


        // =========================================================
        // 3. FEJEZETSZÁM + KÖVETKEZŐ RÖVID CÍM
        // =========================================================

        var blocks =
            Regex.Matches(
                html,
                @"<(?:p|div|span)[^>]*>(.*?)</(?:p|div|span)>",
                RegexOptions.IgnoreCase |
                RegexOptions.Singleline);

        for (var i = 0; i < blocks.Count; i++)
        {
            var first =
                CleanText(
                    blocks[i].Groups[1].Value);

            if (!IsChapterNumber(first))
                continue;

            if (i + 1 < blocks.Count)
            {
                var second =
                    CleanText(
                        blocks[i + 1].Groups[1].Value);

                if (IsValidChapterTitle(
                    second,
                    fallbackTitle))
                {
                    return
                        $"{first} {second}";
                }
            }

            return first;
        }


        // =========================================================
        // 4. HTML <TITLE>
        // =========================================================

        var pageTitle =
            Regex.Match(
                html,
                @"<title[^>]*>(.*?)</title>",
                RegexOptions.IgnoreCase |
                RegexOptions.Singleline);

        if (pageTitle.Success)
        {
            var title =
                CleanText(
                    pageTitle.Groups[1].Value);

            if (IsValidChapterTitle(
                title,
                fallbackTitle))
            {
                return title;
            }
        }


        // =========================================================
        // 5. ELSŐ RÖVID BEKEZDÉS
        // =========================================================

        foreach (Match block in blocks)
        {
            var title =
                CleanText(
                    block.Groups[1].Value);

            if (IsValidChapterTitle(
                title,
                fallbackTitle)
                &&
                title.Length <= 100)
            {
                return title;
            }
        }


        // =========================================================
        // 6. FALLBACK
        // =========================================================

        return fallbackTitle;
    }


    // =========================================================
    // SZÖVEG TISZTÍTÁSA
    // =========================================================

    private static string CleanText(
        string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        text =
            Regex.Replace(
                text,
                "<.*?>",
                string.Empty,
                RegexOptions.Singleline);

        text =
            WebUtility.HtmlDecode(
                text);

        text =
            Regex.Replace(
                text,
                @"\s+",
                " ");

        return text.Trim();
    }


    // =========================================================
    // ÉRVÉNYES FEJEZETCÍM?
    // =========================================================

    private static bool IsValidChapterTitle(
        string title,
        string fallbackTitle)
    {
        if (string.IsNullOrWhiteSpace(title))
            return false;

        if (title.Length > 150)
            return false;

        // Ha ugyanaz, mint a hibás TOC-cím,
        // nem fogadjuk el.
        if (!string.IsNullOrWhiteSpace(fallbackTitle)
            &&
            string.Equals(
                title.Trim(),
                fallbackTitle.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Ne legyen teljes mondat / könyvszöveg.
        if (title.Count(
                c => c == '.' ||
                     c == '!' ||
                     c == '?') > 1)
        {
            return false;
        }

        return true;
    }


    // =========================================================
    // FEJEZETSZÁM FELISMERÉSE
    // =========================================================

    private static bool IsChapterNumber(
        string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        text =
            text.Trim();

        return Regex.IsMatch(
            text,
            @"^(?:chapter\s*)?\d{1,4}[.:]?$",
            RegexOptions.IgnoreCase);
    }
}