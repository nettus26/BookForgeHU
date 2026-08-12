using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using BookForge.Core.Models;
using BookForge.Epub.Helpers;
using BookForge.Epub.Interfaces;
using BookForge.Epub.Parsers;

namespace BookForge.Epub;

public class EpubReaderV2 : IEpubReader
{
    public Book Load(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException(
                "Az EPUB fájl elérési útja üres.",
                nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException(
                "Az EPUB fájl nem található.",
                filePath);

        using var package = new EpubPackage(filePath);
        using var archive = package.Open();

        var contentReader = new EpubContentReader();

        var containerXml = contentReader.ReadEntry(
            archive,
            "META-INF/container.xml");

        var containerParser = new ContainerParser();

        var contentPath =
            containerParser.FindContentPath(containerXml);

        if (string.IsNullOrWhiteSpace(contentPath))
            throw new InvalidDataException(
                "Nem található content.opf az EPUB-ban.");

        contentPath = NormalizePath(contentPath);

        var contentXml =
            contentReader.ReadEntry(
                archive,
                contentPath);

        var contentParser = new ContentParser();

        var book = contentParser.Parse(contentXml);

        book.FilePath = filePath;

        // ============================
        // MANIFEST
        // ============================

        var manifestParser = new ManifestParser();

        var manifest =
            manifestParser.Parse(contentXml);

        // ============================
        // SPINE
        // ============================

        var spineParser = new SpineParser();

        var spine =
            spineParser.Parse(contentXml);

        // ============================
        // BORÍTÓ
        // ============================

        book.CoverImage =
            SaveCover(
                archive,
                contentXml,
                manifest,
                contentPath);

        // ============================
        // TARTALOMJEGYZÉK
        // ============================

        var toc =
            LoadTableOfContents(
                archive,
                contentXml,
                manifest,
                contentPath);

        // ============================
        // FEJEZETEK
        // ============================

        LoadChapters(
            archive,
            book,
            manifest,
            spine,
            toc,
            contentPath);

        return book;
    }


    private static void LoadChapters(
        ZipArchive archive,
        Book book,
        Dictionary<string, string> manifest,
        List<string> spine,
        Dictionary<string, string> toc,
        string contentPath)
    {
        var chapterLoader =
            new ChapterLoader();

        var titleResolver =
            new ChapterTitleResolver();

        var contentDirectory =
            GetDirectory(contentPath);

        var order = 1;

        foreach (var id in spine)
        {
            if (!manifest.TryGetValue(
                id,
                out var href)
                ||
                string.IsNullOrWhiteSpace(href))
            {
                continue;
            }

            var chapterPath =
                ResolvePath(
                    contentDirectory,
                    href);

            var chapterEntry =
                FindEntry(
                    archive,
                    chapterPath);

            if (chapterEntry == null)
                continue;

            string html;

            using (var reader =
                   new StreamReader(
                       chapterEntry.Open()))
            {
                html =
                    reader.ReadToEnd();
            }

            // ============================
            // DIAGNOSZTIKA
            // ============================

            Debug.WriteLine(
                $"EPUB TESZT | Fejezet: {chapterPath}");

            Debug.WriteLine(
                $"EPUB TESZT | HTML hossz: {html.Length}");

            if (!string.IsNullOrWhiteSpace(html))
            {
                Debug.WriteLine(
                    $"EPUB TESZT | HTML eleje: " +
                    html[..Math.Min(200, html.Length)]);
            }
            else
            {
                Debug.WriteLine(
                    "EPUB TESZT | A HTML ÜRES!");
            }

            var title =
                FindTocTitle(
                    toc,
                    chapterPath)
                ?? $"Chapter {order}";

            title =
                titleResolver.Resolve(
                    html,
                    title);

            var chapter =
                chapterLoader.Load(
                    title,
                    html,
                    order);

            Debug.WriteLine(
                $"EPUB TESZT | Chapter.HtmlContent hossz: " +
                $"{chapter.HtmlContent?.Length ?? 0}");

            chapter.FilePath =
                chapterPath;

            chapter.Href =
                href;

            book.Chapters.Add(
                chapter);

            order++;
        }
    }


    private static Dictionary<string, string>
        LoadTableOfContents(
            ZipArchive archive,
            string contentXml,
            Dictionary<string, string> manifest,
            string contentPath)
    {
        var result =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

        var document =
            XDocument.Parse(contentXml);

        XNamespace opf =
            "http://www.idpf.org/2007/opf";

        var contentDirectory =
            GetDirectory(contentPath);

        // EPUB 3: nav

        var navItem =
            document
                .Descendants(opf + "item")
                .FirstOrDefault(item =>
                    (item.Attribute("properties")?.Value
                     ?? string.Empty)
                    .Split(
                        ' ',
                        StringSplitOptions.RemoveEmptyEntries)
                    .Any(p =>
                        p.Equals(
                            "nav",
                            StringComparison.OrdinalIgnoreCase)));

        if (navItem != null)
        {
            var navHref =
                navItem.Attribute("href")?.Value;

            if (!string.IsNullOrWhiteSpace(navHref))
            {
                var navPath =
                    ResolvePath(
                        contentDirectory,
                        navHref);

                var navEntry =
                    FindEntry(
                        archive,
                        navPath);

                if (navEntry != null)
                {
                    using var reader =
                        new StreamReader(
                            navEntry.Open());

                    var navXhtml =
                        reader.ReadToEnd();

                    var parser =
                        new TocParser();

                    var parsed =
                        parser.Parse(navXhtml);

                    AddNormalizedTocEntries(
                        result,
                        parsed,
                        GetDirectory(navPath));
                }
            }
        }

        // EPUB 2: NCX

        if (result.Count == 0)
        {
            string? ncxHref = null;

            var spineElement =
                document
                    .Descendants(opf + "spine")
                    .FirstOrDefault();

            var tocId =
                spineElement?
                    .Attribute("toc")?
                    .Value;

            if (!string.IsNullOrWhiteSpace(tocId)
                &&
                manifest.TryGetValue(
                    tocId,
                    out var tocHref))
            {
                ncxHref = tocHref;
            }

            if (string.IsNullOrWhiteSpace(ncxHref))
            {
                var ncxItem =
                    document
                        .Descendants(opf + "item")
                        .FirstOrDefault(item =>
                            string.Equals(
                                item.Attribute(
                                    "media-type")?.Value,
                                "application/x-dtbncx+xml",
                                StringComparison.OrdinalIgnoreCase));

                ncxHref =
                    ncxItem?.Attribute("href")?.Value;
            }

            if (!string.IsNullOrWhiteSpace(ncxHref))
            {
                var ncxPath =
                    ResolvePath(
                        contentDirectory,
                        ncxHref);

                var ncxEntry =
                    FindEntry(
                        archive,
                        ncxPath);

                if (ncxEntry != null)
                {
                    using var reader =
                        new StreamReader(
                            ncxEntry.Open());

                    var ncx =
                        reader.ReadToEnd();

                    var parser =
                        new NcxParser();

                    var parsed =
                        parser.Parse(ncx);

                    AddNormalizedTocEntries(
                        result,
                        parsed,
                        GetDirectory(ncxPath));
                }
            }
        }

        return result;
    }


    private static string SaveCover(
        ZipArchive archive,
        string contentXml,
        Dictionary<string, string> manifest,
        string contentPath)
    {
        var document =
            XDocument.Parse(contentXml);

        XNamespace opf =
            "http://www.idpf.org/2007/opf";

        var contentDirectory =
            GetDirectory(contentPath);

        string? coverHref = null;

        // EPUB 3

        var coverItem =
            document
                .Descendants(opf + "item")
                .FirstOrDefault(item =>
                    (item.Attribute("properties")?.Value
                     ?? string.Empty)
                    .Split(
                        ' ',
                        StringSplitOptions.RemoveEmptyEntries)
                    .Any(p =>
                        p.Equals(
                            "cover-image",
                            StringComparison.OrdinalIgnoreCase)));

        if (coverItem != null)
        {
            coverHref =
                coverItem.Attribute("href")?.Value;
        }

        // EPUB 2

        if (string.IsNullOrWhiteSpace(coverHref))
        {
            var coverMeta =
                document
                    .Descendants(opf + "meta")
                    .FirstOrDefault(meta =>
                        string.Equals(
                            meta.Attribute("name")?.Value,
                            "cover",
                            StringComparison.OrdinalIgnoreCase));

            var coverId =
                coverMeta?
                    .Attribute("content")?
                    .Value;

            if (!string.IsNullOrWhiteSpace(coverId)
                &&
                manifest.TryGetValue(
                    coverId,
                    out var href))
            {
                coverHref = href;
            }
        }

        ZipArchiveEntry? coverEntry = null;

        if (!string.IsNullOrWhiteSpace(coverHref))
        {
            var coverPath =
                ResolvePath(
                    contentDirectory,
                    coverHref);

            coverEntry =
                FindEntry(
                    archive,
                    coverPath);
        }

        if (coverEntry == null)
        {
            coverEntry =
                archive.Entries
                    .FirstOrDefault(entry =>
                        entry.FullName.Contains(
                            "cover",
                            StringComparison.OrdinalIgnoreCase)
                        &&
                        IsImage(entry.FullName));
        }

        if (coverEntry == null)
            return string.Empty;

        try
        {
            var coverFolder =
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.ApplicationData),
                    "BookForge",
                    "Covers");

            Directory.CreateDirectory(
                coverFolder);

            var extension =
                Path.GetExtension(
                    coverEntry.FullName);

            if (string.IsNullOrWhiteSpace(extension))
                extension = ".img";

            var savedCover =
                Path.Combine(
                    coverFolder,
                    $"{Guid.NewGuid()}{extension}");

            using var source =
                coverEntry.Open();

            using var target =
                File.Create(savedCover);

            source.CopyTo(target);

            return savedCover;
        }
        catch
        {
            return string.Empty;
        }
    }


    private static void AddNormalizedTocEntries(
        Dictionary<string, string> target,
        Dictionary<string, string> parsed,
        string tocDirectory)
    {
        foreach (var item in parsed)
        {
            var path =
                ResolvePath(
                    tocDirectory,
                    item.Key);

            target[path] =
                item.Value;
        }
    }


    private static string? FindTocTitle(
        Dictionary<string, string> toc,
        string chapterPath)
    {
        if (toc.TryGetValue(
            chapterPath,
            out var exact))
        {
            return exact;
        }

        var fileName =
            Path.GetFileName(
                chapterPath);

        if (!string.IsNullOrWhiteSpace(fileName))
        {
            var match =
                toc.FirstOrDefault(item =>
                    string.Equals(
                        Path.GetFileName(item.Key),
                        fileName,
                        StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(match.Value))
            {
                return match.Value;
            }
        }

        return null;
    }


    private static ZipArchiveEntry? FindEntry(
        ZipArchive archive,
        string path)
    {
        var normalized =
            NormalizePath(path);

        return archive.Entries
            .FirstOrDefault(entry =>
                NormalizePath(entry.FullName)
                    .Equals(
                        normalized,
                        StringComparison.OrdinalIgnoreCase));
    }


    private static string ResolvePath(
        string baseDirectory,
        string href)
    {
        href =
            href
                .Replace("\\", "/")
                .Trim();

        href =
            Uri.UnescapeDataString(
                href);

        var combined =
            string.IsNullOrWhiteSpace(
                baseDirectory)
                ? href
                : $"{baseDirectory.TrimEnd('/')}/{href.TrimStart('/')}";

        return NormalizePath(
            combined);
    }


    private static string GetDirectory(
        string path)
    {
        path =
            NormalizePath(path);

        var slash =
            path.LastIndexOf('/');

        return slash < 0
            ? string.Empty
            : path[..slash];
    }


    private static string NormalizePath(
        string path)
    {
        path =
            path
                .Replace("\\", "/")
                .Trim();

        var parts =
            new List<string>();

        foreach (var part in path.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".")
                continue;

            if (part == "..")
            {
                if (parts.Count > 0)
                    parts.RemoveAt(
                        parts.Count - 1);

                continue;
            }

            parts.Add(part);
        }

        return string.Join(
            "/",
            parts);
    }


    private static bool IsImage(
        string path)
    {
        return
            path.EndsWith(
                ".jpg",
                StringComparison.OrdinalIgnoreCase)
            ||
            path.EndsWith(
                ".jpeg",
                StringComparison.OrdinalIgnoreCase)
            ||
            path.EndsWith(
                ".png",
                StringComparison.OrdinalIgnoreCase)
            ||
            path.EndsWith(
                ".webp",
                StringComparison.OrdinalIgnoreCase)
            ||
            path.EndsWith(
                ".gif",
                StringComparison.OrdinalIgnoreCase);
    }
}