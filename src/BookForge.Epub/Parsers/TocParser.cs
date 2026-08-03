using System.Xml.Linq;

namespace BookForge.Epub.Parsers;

public class TocParser
{
    public Dictionary<string, string> Parse(string xhtml)
    {
        var result = new Dictionary<string, string>();

        var document = XDocument.Parse(xhtml);

        XNamespace xhtmlNs = "http://www.w3.org/1999/xhtml";

        var links = document
            .Descendants(xhtmlNs + "a");

        foreach (var link in links)
        {
            var href = link.Attribute("href")?.Value;
            var title = link.Value.Trim();

            if (!string.IsNullOrEmpty(href) &&
                !string.IsNullOrEmpty(title))
            {
                result[href] = title;
            }
        }

        return result;
    }
}