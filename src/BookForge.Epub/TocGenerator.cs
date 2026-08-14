using System;
using System.Collections.Generic;
using System.Linq;
using BookForge.Core.Models;

namespace BookForge.Epub;

public class TocGenerator
{
    public Dictionary<string, string> Generate(
        Book book)
    {
        var toc =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

        if (book == null)
        {
            return toc;
        }

        var order =
            1;

        foreach (var chapter in book.Chapters)
        {
            if (chapter == null)
            {
                continue;
            }

            var href =
                !string.IsNullOrWhiteSpace(
                    chapter.Href)
                    ? chapter.Href
                    : chapter.FilePath;

            if (string.IsNullOrWhiteSpace(href))
            {
                continue;
            }

            href =
                NormalizeHref(href);

            if (string.IsNullOrWhiteSpace(href))
            {
                continue;
            }

            var title =
                chapter.Title?.Trim();

            if (string.IsNullOrWhiteSpace(title))
            {
                title =
                    $"Chapter {order}";
            }

            toc[href] =
                title;

            order++;
        }

        return toc;
    }


    // =========================================================
    // HREF NORMALIZÁLÁSA
    // =========================================================

    private static string NormalizeHref(
        string href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return string.Empty;
        }

        href =
            href
                .Replace("\\", "/")
                .Trim();

        // Fragment eltávolítása
        var fragmentIndex =
            href.IndexOf('#');

        if (fragmentIndex >= 0)
        {
            href =
                href[..fragmentIndex];
        }

        // Query eltávolítása
        var queryIndex =
            href.IndexOf('?');

        if (queryIndex >= 0)
        {
            href =
                href[..queryIndex];
        }

        try
        {
            href =
                Uri.UnescapeDataString(
                    href);
        }
        catch
        {
            // Ha nem sikerül dekódolni,
            // használjuk az eredeti értéket.
        }

        var parts =
            href.Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries);

        var normalizedParts =
            new List<string>();

        foreach (var part in parts)
        {
            if (part == ".")
            {
                continue;
            }

            if (part == "..")
            {
                if (normalizedParts.Count > 0)
                {
                    normalizedParts.RemoveAt(
                        normalizedParts.Count - 1);
                }

                continue;
            }

            normalizedParts.Add(
                part);
        }

        return string.Join(
            "/",
            normalizedParts);
    }
}