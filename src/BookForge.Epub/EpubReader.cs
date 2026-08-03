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


        var containerXml = contentReader.ReadEntry(
            archive,
            "META-INF/container.xml");


        var containerParser = new ContainerParser();

        var contentPath = containerParser.FindContentPath(containerXml);


        if (contentPath == null)
            throw new Exception("Nem található content.opf");



        var contentXml = contentReader.ReadEntry(
            archive,
            contentPath);


        var contentParser = new ContentParser();

        var book = contentParser.Parse(contentXml);



        // ============================
        // BORÍTÓ KERESÉSE
        // ============================

        string? coverPath = null;


        // Új EPUB szabvány:
        // properties="cover-image"

        var coverIdMatch =
            System.Text.RegularExpressions.Regex.Match(
                contentXml,
                @"<item[^>]+properties\s*=\s*[""']cover-image[""'][^>]+href\s*=\s*[""']([^""']+)[""']",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);


        if (coverIdMatch.Success)
        {
            coverPath = coverIdMatch.Groups[1].Value;
        }



        // Régi EPUB szabvány:
        // <meta name="cover" content="cover-id">

        if (coverPath == null)
        {
            var oldCoverMatch =
                System.Text.RegularExpressions.Regex.Match(
                    contentXml,
                    @"<meta[^>]+name\s*=\s*[""']cover[""'][^>]+content\s*=\s*[""']([^""']+)[""']",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);


            if (oldCoverMatch.Success)
            {
                var coverId = oldCoverMatch.Groups[1].Value;


                var manifestMatch =
                    System.Text.RegularExpressions.Regex.Match(
                        contentXml,
                        $@"<item[^>]+id\s*=\s*[""']{coverId}[""'][^>]+href\s*=\s*[""']([^""']+)[""']",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);


                if (manifestMatch.Success)
                {
                    coverPath = manifestMatch.Groups[1].Value;
                }
            }
        }



        // Utolsó próbálkozás:
        // fájlnév alapján

        if (coverPath == null)
        {
            var fileCover =
                archive.Entries.FirstOrDefault(e =>
                    e.FullName.Contains(
                        "cover",
                        StringComparison.OrdinalIgnoreCase)
                    &&
                    (
                        e.FullName.EndsWith(".jpg",
                            StringComparison.OrdinalIgnoreCase)
                        ||
                        e.FullName.EndsWith(".jpeg",
                            StringComparison.OrdinalIgnoreCase)
                        ||
                        e.FullName.EndsWith(".png",
                            StringComparison.OrdinalIgnoreCase)
                    ));


            if (fileCover != null)
            {
                coverPath = fileCover.FullName;
            }
        }



        if (coverPath != null)
        {
            var coverEntry =
                archive.Entries.FirstOrDefault(e =>
                    e.FullName.Replace("\\", "/")
                    .Equals(
                        coverPath.Replace("\\", "/"),
                        StringComparison.OrdinalIgnoreCase));


            if (coverEntry != null)
            {
                try
                {
                    var coverFolder =
                        Path.Combine(
                            Environment.GetFolderPath(
                                Environment.SpecialFolder.ApplicationData),
                            "BookForge",
                            "Covers");


                    Directory.CreateDirectory(coverFolder);


                    var extension =
                        Path.GetExtension(coverEntry.FullName);


                    var savedCover =
                        Path.Combine(
                            coverFolder,
                            $"{Guid.NewGuid()}{extension}");


                    using var source = coverEntry.Open();
                    using var target = File.Create(savedCover);


                    source.CopyTo(target);


                    book.CoverImage = savedCover;
                }
                catch
                {
                    book.CoverImage = string.Empty;
                }
            }
        }



        // ============================
        // MANIFEST
        // ============================

        var manifestParser = new ManifestParser();

        var manifest = manifestParser.Parse(contentXml);



        // ============================
        // SPINE
        // ============================

        var spineParser = new SpineParser();

        var spine = spineParser.Parse(contentXml);



        // ============================
        // TARTALOMJEGYZÉK
        // ============================

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



        // ============================
        // FEJEZETEK
        // ============================

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