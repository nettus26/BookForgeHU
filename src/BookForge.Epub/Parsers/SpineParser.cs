using System.Xml.Linq;

namespace BookForge.Epub.Parsers;

public class SpineParser
{
    public List<string> Parse(string xmlContent)
    {
        var result = new List<string>();

        var document = XDocument.Parse(xmlContent);

        XNamespace opf = "http://www.idpf.org/2007/opf";

        var items = document
            .Descendants(opf + "itemref");

        foreach (var item in items)
        {
            var idRef = item.Attribute("idref")?.Value;

            if (!string.IsNullOrEmpty(idRef))
            {
                result.Add(idRef);
            }
        }

        return result;
    }
}