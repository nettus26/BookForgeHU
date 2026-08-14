using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;

namespace BookForge.Epub.Parsers;

public class TocParser
{
    public Dictionary<string, string> Parse(string xhtml)
    {
        var result =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(xhtml))
        {
            return result;
        }

        // =========================================================
        // LINKEK KERESÉSE
        // =========================================================

        var matches =
            Regex.Matches(
                xhtml,
                @"<a\b[^>]*?\bhref\s*=\s*[""']([^""']+)[""'][^>]*>(.*?)</a\s*>",
                RegexOptions.IgnoreCase |
                RegexOptions.Singleline);

        foreach (Match match in matches)
        {
            if (!match.Success)
            {
                continue;
            }

            var href =
                match.Groups[1].Value;

            var rawTitle =
                match.Groups[2].Value;

            if (string.IsNullOrWhiteSpace(href))
            {
                continue;
            }

            // =====================================================
            // HORGONY ELTÁVOLÍTÁSA
            // =====================================================

            var fragmentIndex =
                href.IndexOf('#');

            if (fragmentIndex >= 0)
            {
                href =
                    href[..fragmentIndex];
            }

            // =====================================================
            // ÚTVONAL NORMALIZÁLÁSA
            // =====================================================

            href =
                href
                    .Replace("\\", "/")
                    .Trim()
                    .TrimStart('/');

            // URL-dekódolás
            try
            {
                href =
                    Uri.UnescapeDataString(
                        href);
            }
            catch
            {
            }

            // =====================================================
            // CÍM TISZTÍTÁSA
            // =====================================================

            var title =
                CleanTitle(
                    rawTitle);

            if (string.IsNullOrWhiteSpace(href) ||
                string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            // =====================================================
            // DUPLIKÁLT ÚTVONAL
            // =====================================================

            result[href] =
                title;
        }


        // =========================================================
        // DIAGNOSZTIKA
        // =========================================================

        foreach (var item in result)
        {
            System.Diagnostics.Debug.WriteLine(
                $"TOC | {item.Key} -> {item.Value}");
        }

        return result;
    }


    // =========================================================
    // CÍM TISZTÍTÁSA
    // =========================================================

    private static string CleanTitle(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        // HTML tagek eltávolítása
        var title =
            Regex.Replace(
                value,
                "<.*?>",
                string.Empty,
                RegexOptions.Singleline);

        // HTML entitások visszaalakítása
        title =
            WebUtility.HtmlDecode(
                title);

        // Whitespace egységesítése
        title =
            Regex.Replace(
                title,
                @"\s+",
                " ");

        return title.Trim();
    }
}