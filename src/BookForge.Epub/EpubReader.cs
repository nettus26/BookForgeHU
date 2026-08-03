using BookForge.Core.Models;
using BookForge.Epub.Helpers;
using BookForge.Epub.Interfaces;
using BookForge.Epub.Parsers;

namespace BookForge.Epub;

public class EpubReader : IEpubReader
{
    public Book Load(string filePath)
    {
        using var package = new EpubPackage(filePath);
        using var archive = package.Open();

        var contentReader = new EpubContentReader();


        // container.xml
        var containerXml = contentReader.ReadEntry(
            archive,
            "META-INF/container.xml");

        var containerParser = new ContainerParser();

        var contentPath = containerParser.FindContentPath(containerXml);

        if (contentPath == null)
            throw new Exception("Nem található content.opf");


        // content.opf
        var contentXml = contentReader.ReadEntry(
            archive,
            contentPath);

        var contentParser = new ContentParser();

        var book = contentParser.Parse(contentXml);


        // manifest
        var manifestParser = new ManifestParser();

        var manifest = manifestParser.Parse(contentXml);


        // spine
        var spineParser = new SpineParser();

        var spine = spineParser.Parse(contentXml);


        // Tartalomjegyzék
        Dictionary<string, string> toc = new();

        var navEntry = archive.Entries
            .FirstOrDefault(e =>
                e.FullName.Replace("\\", "/")
                .Equals(
                    "Text/nav.xhtml",
                    StringComparison.OrdinalIgnoreCase));

        if (navEntry != null)
        {
            using var navReader = new StreamReader(navEntry.Open());

            var navContent = navReader.ReadToEnd();

            var tocParser = new TocParser();

            toc = tocParser.Parse(navContent);
        }


        // Fejezetek
        var chapterLoader = new ChapterLoader();

        int order = 1;

        foreach (var id in spine)
        {
            if (!manifest.ContainsKey(id))
                continue;


            var chapterPath = manifest[id];


            var chapterEntry = archive.Entries
                .FirstOrDefault(e =>
                    e.FullName.Replace("\\", "/")
                    .Equals(
                        chapterPath,
                        StringComparison.OrdinalIgnoreCase));


            if (chapterEntry == null)
                continue;


            using var reader =
                new StreamReader(chapterEntry.Open());

            var html = reader.ReadToEnd();


            var title = $"Chapter {order}";


            // TOC cím keresése többféle útvonallal
            if (toc.ContainsKey(chapterPath))
            {
                title = toc[chapterPath];
            }
            else
            {
                var shortPath = chapterPath.Replace("Text/", "");

                if (toc.ContainsKey(shortPath))
                {
                    title = toc[shortPath];
                }
            }


            var chapter = chapterLoader.Load(
                title,
                html,
                order);


            book.Chapters.Add(chapter);

            order++;
        }


        return book;
    }
}