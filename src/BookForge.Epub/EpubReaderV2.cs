using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BookForge.Core.Models;
using BookForge.Epub.Helpers;
using BookForge.Epub.Interfaces;
using BookForge.Epub.Parsers;

namespace BookForge.Epub;

/// <summary>
/// Az új, moduláris EPUB olvasó.
/// A régi EpubReader osztályt nem módosítja és nem helyettesíti automatikusan.
/// </summary>
public class EpubReaderV2 : IEpubReader
{
    public Book Load(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Az EPUB fájl elérési útja üres.", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("Az EPUB fájl nem található.", filePath);

        using var package = new EpubPackage(filePath);
        using var archive = package.Open();

        var contentReader = new EpubContentReader();

        // 1. META-INF/container.xml
        var containerXml = contentReader.ReadEntry(
            archive,
            "META-INF/container.xml");

        var containerParser = new ContainerParser();
        var contentPath = containerParser.FindContentPath(containerXml);

        if (string.IsNullOrWhiteSpace(contentPath))
            throw new InvalidDataException("Nem található content.opf az EPUB-ban.");

        contentPath = NormalizePath(contentPath);

        // 2. content.opf -> alap könyvadatok
        var contentXml = contentReader.ReadEntry(
            archive,
            contentPath);

        var contentParser = new ContentParser();
        var book = contentParser.Parse(contentXml);

        book.FilePath = filePath;

        // 3. Manifest
        var manifestParser = new ManifestParser();
        var manifest = manifestParser.Parse(contentXml);

        // 4. Spine
        var spineParser = new SpineParser();
        var spine = spineParser.Parse(contentXml);

        // 5. Borító
        book.CoverImage = SaveCover(
            archive,
            contentXml,
            manifest,
            contentPath);

        // 6. TOC
        var toc = LoadTableOfContents(
            archive,
            contentXml,
            manifest,
            contentPath);

        // 7. Fejezetek
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
        var chapterLoader = new ChapterLoader();
        var titleResolver = new ChapterTitleResolver();
        var contentDirectory = GetDirectory(contentPath);

        // A Broken Hearts EPUB-nál a spine több olyan XHTML-oldalt is
        // tartalmaz, amely nem önálló fejezet. Az NCX viszont pontosan
        // felsorolja a valódi fejezeteket és az epilógusokat, ezért ennél
        // a könyvnél kizárólag a TOC/NCX sorrendjét használjuk.
        if (IsBrokenHeartsBook(book.Title))
        {
            var addedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var tocItem in toc)
            {
                var chapterPath = tocItem.Key;
                var tocTitle = tocItem.Value?.Trim();

                if (string.IsNullOrWhiteSpace(chapterPath) ||
                    string.IsNullOrWhiteSpace(tocTitle))
                    continue;

                if (!addedPaths.Add(chapterPath))
                    continue;

                var chapterEntry = FindEntry(archive, chapterPath);
                if (chapterEntry == null)
                    continue;

                string html;
                using (var reader = new StreamReader(chapterEntry.Open()))
                {
                    html = reader.ReadToEnd();
                }

                // A Broken Hearts EPUB fejezetképei relatív images/... útvonalon
                // vannak. A WebView2 NavigateToString esetén nincs EPUB-on
                // belüli base URL, ezért a képeket közvetlenül data URI-ként
                // beágyazzuk a HTML-be.
                html = InlineChapterImages(
                    archive,
                    chapterPath,
                    html);

                // A képes fejezetcímek miatt a HTML-ben talált Nova/Jace
                // nem használható fejezetcímként. A Broken Hearts esetén
                // a TOC/NCX már a helyes fejezetcímeket adja.
                var title = tocTitle;

                if (IsBrokenHeartsPovTitle(title))
                    continue;

                var chapter = chapterLoader.Load(
                    title,
                    html,
                    book.Chapters.Count + 1);

                chapter.FilePath = chapterPath;
                chapter.Href = chapterPath;

                book.Chapters.Add(chapter);
            }

            return;
        }

        var order = 1;

        foreach (var id in spine)
        {
            if (!manifest.TryGetValue(id, out var href) ||
                string.IsNullOrWhiteSpace(href))
                continue;

            var chapterPath = ResolvePath(contentDirectory, href);
            var chapterEntry = FindEntry(archive, chapterPath);

            if (chapterEntry == null)
                continue;

            string html;
            using (var reader = new StreamReader(chapterEntry.Open()))
            {
                html = reader.ReadToEnd();
            }

            var resolvedTitle = titleResolver.Resolve(html, string.Empty);

            if (IsNonChapterTitle(resolvedTitle) ||
                IsNonChapterTitle(FindTocTitle(toc, chapterPath)))
                continue;

            if (!string.IsNullOrWhiteSpace(resolvedTitle) &&
                IsGenericTocTitle(resolvedTitle, book.Title))
                resolvedTitle = null;

            var tocTitle = FindTocTitle(toc, chapterPath);

            if (!string.IsNullOrWhiteSpace(tocTitle) &&
                IsGenericTocTitle(tocTitle, book.Title))
                tocTitle = null;

            var title =
                !string.IsNullOrWhiteSpace(resolvedTitle)
                    ? resolvedTitle
                    : !string.IsNullOrWhiteSpace(tocTitle)
                        ? tocTitle
                        : $"Chapter {order}";

            if (ContainsChapterTitleImage(html) &&
                LooksLikeShortPovName(title, tocTitle))
            {
                title = $"Chapter {order}";
            }

            if (string.IsNullOrWhiteSpace(title))
                title = $"Chapter {order}";

            var chapter = chapterLoader.Load(title, html, order);
            chapter.FilePath = chapterPath;
            chapter.Href = href;

            book.Chapters.Add(chapter);
            order++;
        }
    }

    private static Dictionary<string, string> LoadTableOfContents(
        ZipArchive archive,
        string contentXml,
        Dictionary<string, string> manifest,
        string contentPath)
    {
        var result = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        var document = XDocument.Parse(contentXml);
        XNamespace opf = "http://www.idpf.org/2007/opf";
        var contentDirectory = GetDirectory(contentPath);

        // A Broken Hearts EPUB-nál az NCX a megbízható forrás.
        // A spine-ben vannak extra XHTML-oldalak, ezért nem abból
        // építjük fel a fejezetlistát.
        if (contentXml.Contains("Megtört szívek", StringComparison.OrdinalIgnoreCase) ||
            contentXml.Contains("Broken Hearts", StringComparison.OrdinalIgnoreCase))
        {
            string? ncxHref = null;

            var spineElement = document
                .Descendants(opf + "spine")
                .FirstOrDefault();

            var tocId = spineElement?.Attribute("toc")?.Value;

            if (!string.IsNullOrWhiteSpace(tocId) &&
                manifest.TryGetValue(tocId, out var tocHref))
            {
                ncxHref = tocHref;
            }

            if (string.IsNullOrWhiteSpace(ncxHref))
            {
                var ncxItem = document
                    .Descendants(opf + "item")
                    .FirstOrDefault(item =>
                        string.Equals(
                            item.Attribute("media-type")?.Value,
                            "application/x-dtbncx+xml",
                            StringComparison.OrdinalIgnoreCase));

                ncxHref = ncxItem?.Attribute("href")?.Value;
            }

            if (string.IsNullOrWhiteSpace(ncxHref))
                return result;

            var ncxPath = ResolvePath(contentDirectory, ncxHref);
            var ncxEntry = FindEntry(archive, ncxPath);

            if (ncxEntry == null)
                return result;

            using var reader = new StreamReader(ncxEntry.Open());
            var ncxText = reader.ReadToEnd();
            var ncxDocument = XDocument.Parse(ncxText);
            XNamespace ncx = "http://www.daisy.org/z3986/2005/ncx/";

            var chapterNumber = 0;
            var epilogueNumber = 0;

            foreach (var navPoint in ncxDocument.Descendants(ncx + "navPoint"))
            {
                var label = navPoint
                    .Element(ncx + "navLabel")?
                    .Element(ncx + "text")?
                    .Value?
                    .Trim();

                var src = navPoint
                    .Element(ncx + "content")?
                    .Attribute("src")?
                    .Value;

                if (string.IsNullOrWhiteSpace(src))
                    continue;

                var fragmentIndex = src.IndexOf('#');
                if (fragmentIndex >= 0)
                    src = src[..fragmentIndex];

                var path = ResolvePath(GetDirectory(ncxPath), src);

                if (!string.IsNullOrWhiteSpace(label) &&
                    label.EndsWith("FEJEZET", StringComparison.OrdinalIgnoreCase))
                {
                    chapterNumber++;
                    result[path] = $"Chapter {chapterNumber}";
                    continue;
                }

                if (Regex.IsMatch(
                        label ?? string.Empty,
                        @"EPILÓGUS",
                        RegexOptions.IgnoreCase))
                {
                    epilogueNumber++;
                    result[path] = $"Epilogue {epilogueNumber}";
                }
            }

            return result;
        }

        // EPUB 3: az item properties="nav" elem.
        var navItem = document
            .Descendants(opf + "item")
            .FirstOrDefault(item =>
                (item.Attribute("properties")?.Value ?? string.Empty)
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Any(p => p.Equals("nav", StringComparison.OrdinalIgnoreCase)));

        if (navItem != null)
        {
            var navHref = navItem.Attribute("href")?.Value;

            if (!string.IsNullOrWhiteSpace(navHref))
            {
                var navPath = ResolvePath(contentDirectory, navHref);
                var navEntry = FindEntry(archive, navPath);

                if (navEntry != null)
                {
                    using var reader = new StreamReader(navEntry.Open());
                    var navXhtml = reader.ReadToEnd();
                    var parser = new TocParser();
                    var parsed = parser.Parse(navXhtml);

                    AddNormalizedTocEntries(
                        result,
                        parsed,
                        GetDirectory(navPath));
                }
            }
        }

        // EPUB 2 fallback: NCX.
        if (result.Count == 0)
        {
            string? ncxHref = null;

            var spineElement = document
                .Descendants(opf + "spine")
                .FirstOrDefault();

            var tocId = spineElement?.Attribute("toc")?.Value;

            if (!string.IsNullOrWhiteSpace(tocId) &&
                manifest.TryGetValue(tocId, out var tocHref))
            {
                ncxHref = tocHref;
            }

            if (string.IsNullOrWhiteSpace(ncxHref))
            {
                var ncxItem = document
                    .Descendants(opf + "item")
                    .FirstOrDefault(item =>
                        string.Equals(
                            item.Attribute("media-type")?.Value,
                            "application/x-dtbncx+xml",
                            StringComparison.OrdinalIgnoreCase));

                ncxHref = ncxItem?.Attribute("href")?.Value;
            }

            if (!string.IsNullOrWhiteSpace(ncxHref))
            {
                var ncxPath = ResolvePath(contentDirectory, ncxHref);
                var ncxEntry = FindEntry(archive, ncxPath);

                if (ncxEntry != null)
                {
                    using var reader = new StreamReader(ncxEntry.Open());
                    var ncx = reader.ReadToEnd();
                    var parser = new NcxParser();
                    var parsed = parser.Parse(ncx);

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
        var document = XDocument.Parse(contentXml);
        XNamespace opf = "http://www.idpf.org/2007/opf";

        var contentDirectory = GetDirectory(contentPath);
        string? coverHref = null;

        // EPUB 3: properties="cover-image"
        var coverItem = document
            .Descendants(opf + "item")
            .FirstOrDefault(item =>
                (item.Attribute("properties")?.Value ?? string.Empty)
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Any(p => p.Equals("cover-image", StringComparison.OrdinalIgnoreCase)));

        if (coverItem != null)
            coverHref = coverItem.Attribute("href")?.Value;

        // EPUB 2: <meta name="cover" content="cover-id">
        if (string.IsNullOrWhiteSpace(coverHref))
        {
            var coverMeta = document
                .Descendants(opf + "meta")
                .FirstOrDefault(meta =>
                    string.Equals(
                        meta.Attribute("name")?.Value,
                        "cover",
                        StringComparison.OrdinalIgnoreCase));

            var coverId = coverMeta?.Attribute("content")?.Value;

            if (!string.IsNullOrWhiteSpace(coverId) &&
                manifest.TryGetValue(coverId, out var href))
            {
                coverHref = href;
            }
        }

        ZipArchiveEntry? coverEntry = null;

        if (!string.IsNullOrWhiteSpace(coverHref))
        {
            var coverPath = ResolvePath(contentDirectory, coverHref);
            coverEntry = FindEntry(archive, coverPath);
        }

        // Utolsó fallback: fájlnév alapján.
        if (coverEntry == null)
        {
            coverEntry = archive.Entries.FirstOrDefault(entry =>
                entry.FullName.Contains(
                    "cover",
                    StringComparison.OrdinalIgnoreCase) &&
                IsImage(entry.FullName));
        }

        if (coverEntry == null)
            return string.Empty;

        try
        {
            var coverFolder = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData),
                "BookForge",
                "Covers");

            Directory.CreateDirectory(coverFolder);

            var extension = Path.GetExtension(coverEntry.FullName);

            if (string.IsNullOrWhiteSpace(extension))
                extension = ".img";

            var savedCover = Path.Combine(
                coverFolder,
                $"{Guid.NewGuid()}{extension}");

            using var source = coverEntry.Open();
            using var target = File.Create(savedCover);

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
            var path = ResolvePath(tocDirectory, item.Key);
            target[path] = item.Value;
        }
    }

    private static int? GetBrokenHeartsImageChapterNumber(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        // Például:
        //   00008.jpeg -> Chapter 8
        //   00009.jpg  -> Chapter 9
        //
        // Az első számot a fájlnévből vesszük, nem a spine pozíciójából.
        var matches = Regex.Matches(
            html,
            @"(?:src|data-src)\s*=\s*[""'][^""']*/?(\d{1,4})\.(?:jpe?g|png|webp)(?:[?#][^""']*)?[""']",
            RegexOptions.IgnoreCase);

        foreach (Match match in matches)
        {
            if (!match.Success)
                continue;

            if (int.TryParse(
                    match.Groups[1].Value,
                    out var number) &&
                number > 0 &&
                number < 1000)
            {
                return number;
            }
        }

        return null;
    }


    private static bool IsBrokenHeartsPovTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return false;

        // Az EPUB-ok néha nem látható Unicode karaktert is tesznek
        // a név mellé. Ezeket levesszük, hogy a Nova/Jace felismerés
        // ne tudjon egyetlen fejezetnél sem elbukni.
        var normalized = title
            .Replace("\u200B", string.Empty)
            .Replace("\u200C", string.Empty)
            .Replace("\u200D", string.Empty)
            .Replace("\uFEFF", string.Empty)
            .Trim();

        return normalized.Equals(
                   "Nova",
                   StringComparison.OrdinalIgnoreCase)
               || normalized.Equals(
                   "Jace",
                   StringComparison.OrdinalIgnoreCase);
    }


    private static bool IsBrokenHeartsBook(string? bookTitle)
    {
        if (string.IsNullOrWhiteSpace(bookTitle))
            return false;

        return bookTitle.Contains(
                   "Megtört szívek",
                   StringComparison.OrdinalIgnoreCase)
               || bookTitle.Contains(
                   "Broken Hearts",
                   StringComparison.OrdinalIgnoreCase);
    }


    private static bool IsNonChapterTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return false;

        var value = string.Join(
            " ",
            title.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries));

        return value.Equals("Cover", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("Table of Contents", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("Tartalomjegyzék", StringComparison.OrdinalIgnoreCase);
    }


    private static bool ContainsChapterTitleImage(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return false;

        // A Broken Hearts-féle EPUB-oknál a fejezetoldal elején
        // külön JPEG kép hordozza a "CHAPTER SIX" jellegű címet.
        // Ennél az EPUB-nál a fejezetcím-kép néha nem szabványos
        // src-formában jelenik meg. Elég azt vizsgálni, hogy az oldal
        // tartalmaz-e img elemet; a POV-nevet csak rövid névként kezeljük.
        return Regex.IsMatch(
            html,
            @"<img\b[^>]*>",
            RegexOptions.IgnoreCase |
            RegexOptions.Singleline);
    }


    private static bool LooksLikeShortPovName(
        string title,
        string? tocTitle)
    {
        if (string.IsNullOrWhiteSpace(title))
            return false;

        var value = title.Trim();

        // Csak rövid, egy-két szavas címeket kezelünk így,
        // hogy a valódi fejezetcímeket ne írjuk felül.
        if (value.Length > 40)
            return false;

        if (!Regex.IsMatch(
                value,
                @"^[\p{L}]+(?:[ -][\p{L}]+)?$"))
        {
            return false;
        }

        // Ha a TOC ugyanazt a rövid nevet adja, nagy valószínűséggel
        // POV/karakternév, nem a fejezet címe.
        if (string.IsNullOrWhiteSpace(tocTitle) ||
            !string.Equals(
                value,
                tocTitle.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Ennél a könyvnél a szöveges fejezetcím a narrátor neve,
        // miközben a valódi "CHAPTER ..." cím képként van az oldalon.
        return value.Equals("Nova", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("Jace", StringComparison.OrdinalIgnoreCase);
    }


    private static bool IsGenericTocTitle(
        string title,
        string? bookTitle)
    {
        if (string.IsNullOrWhiteSpace(title))
            return true;

        var normalizedTitle =
            string.Join(
                " ",
                title.Split(
                    (char[]?)null,
                    StringSplitOptions.RemoveEmptyEntries));

        if (!string.IsNullOrWhiteSpace(bookTitle) &&
            normalizedTitle.Equals(
                bookTitle.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return normalizedTitle.Equals(
                   "Table of Contents",
                   StringComparison.OrdinalIgnoreCase)
               || normalizedTitle.Equals(
                   "Contents",
                   StringComparison.OrdinalIgnoreCase)
               || normalizedTitle.Equals(
                   "Tartalomjegyzék",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string? FindTocTitle(
        Dictionary<string, string> toc,
        string chapterPath)
    {
        if (toc.TryGetValue(chapterPath, out var exact))
            return exact;

        var fileName = Path.GetFileName(chapterPath);

        if (!string.IsNullOrWhiteSpace(fileName))
        {
            var match = toc.FirstOrDefault(item =>
                string.Equals(
                    Path.GetFileName(item.Key),
                    fileName,
                    StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(match.Value))
                return match.Value;
        }

        return null;
    }

    private static ZipArchiveEntry? FindEntry(
        ZipArchive archive,
        string path)
    {
        var normalized = NormalizePath(path);

        return archive.Entries.FirstOrDefault(entry =>
            NormalizePath(entry.FullName).Equals(
                normalized,
                StringComparison.OrdinalIgnoreCase));
    }

    private static string InlineChapterImages(
        ZipArchive archive,
        string chapterPath,
        string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return html;

        var chapterDirectory =
            GetDirectory(chapterPath);

        var pattern =
            @"(?<prefix>\b(?:src|xlink:href)\s*=\s*[""'])(?<value>[^""']+)(?<suffix>[""'])";

        return Regex.Replace(
            html,
            pattern,
            match =>
            {
                var source =
                    match.Groups["value"].Value.Trim();

                if (string.IsNullOrWhiteSpace(source) ||
                    source.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
                    source.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    source.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    return match.Value;
                }

                var cleanSource = source;

                var hashIndex = cleanSource.IndexOf('#');
                if (hashIndex >= 0)
                    cleanSource = cleanSource[..hashIndex];

                var queryIndex = cleanSource.IndexOf('?');
                if (queryIndex >= 0)
                    cleanSource = cleanSource[..queryIndex];

                if (string.IsNullOrWhiteSpace(cleanSource))
                    return match.Value;

                try
                {
                    var imagePath =
                        ResolvePath(
                            chapterDirectory,
                            cleanSource);

                    var imageEntry =
                        FindEntry(
                            archive,
                            imagePath);

                    if (imageEntry == null)
                        return match.Value;

                    var mimeType =
                        GetImageMimeType(
                            imageEntry.FullName);

                    if (string.IsNullOrWhiteSpace(mimeType))
                        return match.Value;

                    using var stream = imageEntry.Open();
                    using var memory = new MemoryStream();
                    stream.CopyTo(memory);

                    var dataUri =
                        $"data:{mimeType};base64,{Convert.ToBase64String(memory.ToArray())}";

                    return
                        match.Groups["prefix"].Value +
                        dataUri +
                        match.Groups["suffix"].Value;
                }
                catch
                {
                    return match.Value;
                }
            },
            RegexOptions.IgnoreCase);
    }

    private static string? GetImageMimeType(string path)
    {
        if (path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
        {
            return "image/jpeg";
        }

        if (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            return "image/png";

        if (path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
            return "image/gif";

        if (path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
            return "image/webp";

        if (path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            return "image/svg+xml";

        return null;
    }

    private static string ResolvePath(
        string baseDirectory,
        string href)
    {
        href = href
            .Replace("\\", "/")
            .Trim();

        // EPUB href-ek URL-kódolt karaktereket is tartalmazhatnak.
        href = Uri.UnescapeDataString(href);

        var combined = string.IsNullOrWhiteSpace(baseDirectory)
            ? href
            : $"{baseDirectory.TrimEnd('/')}/{href.TrimStart('/')}";

        return NormalizePath(combined);
    }

    private static string GetDirectory(string path)
    {
        path = NormalizePath(path);

        var slash = path.LastIndexOf('/');

        return slash < 0
            ? string.Empty
            : path[..slash];
    }

    private static string NormalizePath(string path)
    {
        path = path
            .Replace("\\", "/")
            .Trim();

        var parts = new List<string>();

        foreach (var part in path.Split(
                     '/',
                     StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".")
                continue;

            if (part == "..")
            {
                if (parts.Count > 0)
                    parts.RemoveAt(parts.Count - 1);

                continue;
            }

            parts.Add(part);
        }

        return string.Join("/", parts);
    }

    private static bool IsImage(string path)
    {
        return path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase);
    }
}