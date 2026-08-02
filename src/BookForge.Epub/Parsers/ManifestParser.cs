using System.Xml.Linq;

namespace BookForge.Epub.Parsers;

public class ManifestParser
{
    public Dictionary<string, string> Parse(string xmlContent)
    {
        var result = new Dictionary<string, string>();

        var document = XDocument.Parse(xmlContent);

        XNamespace opf = "http://www.idpf.org/2007/opf";

        var items = document
            .Descendants(opf + "item");

        foreach (var item in items)
        {
            var id = item.Attribute("id")?.Value;
            var href = item.Attribute("href")?.Value;

            if (!string.IsNullOrEmpty(id) &&
                !string.IsNullOrEmpty(href))
            {
                result[id] = href;
            }
        }

        return result;
    }
}