using System.Xml.Linq;
using BookForge.Core.Models;

namespace BookForge.Epub.Parsers;

public class ContentParser
{
    public Book Parse(string xmlContent)
    {
        var document = XDocument.Parse(xmlContent);

        XNamespace dc = "http://purl.org/dc/elements/1.1/";
        XNamespace opf = "http://www.idpf.org/2007/opf";

        var title = document
            .Descendants(dc + "title")
            .FirstOrDefault()?
            .Value ?? "Ismeretlen cím";

        var author = document
            .Descendants(dc + "creator")
            .FirstOrDefault()?
            .Value ?? "Ismeretlen szerző";

        var language = document
            .Descendants(dc + "language")
            .FirstOrDefault()?
            .Value ?? "hu";

        var book = new Book
        {
            Title = title,
            Author = author,
            Description = $"Nyelv: {language}"
        };

        var manifest = document
            .Descendants(opf + "item")
            .ToList();

        var spine = document
            .Descendants(opf + "itemref")
            .ToList();

        int order = 1;

        foreach (var itemRef in spine)
        {
            var id = itemRef.Attribute("idref")?.Value;

            var item = manifest
                .FirstOrDefault(x =>
                    x.Attribute("id")?.Value == id);

            if (item == null)
                continue;

            var href = item.Attribute("href")?.Value;

            if (href == null)
                continue;

            book.Chapters.Add(new Chapter
            {
                Title = href,
                Order = order++,
                Content = ""
            });
        }

        return book;
    }
}