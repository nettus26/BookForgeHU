using System.Xml.Linq;
using BookForge.Core.Models;

namespace BookForge.Epub.Parsers;

public class ContentParser
{
    public Book Parse(string xmlContent)
    {
        var document = XDocument.Parse(xmlContent);

        XNamespace dc = "http://purl.org/dc/elements/1.1/";

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

        return new Book
        {
            Title = title,
            Author = author,
            Description = $"Nyelv: {language}"
        };
    }
}