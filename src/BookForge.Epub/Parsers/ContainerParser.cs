using System.Xml.Linq;

namespace BookForge.Epub.Parsers;

public class ContainerParser
{
    public string? FindContentPath(string xmlContent)
    {
        var document = XDocument.Parse(xmlContent);

        XNamespace ns = "urn:oasis:names:tc:opendocument:xmlns:container";

        var rootfile = document
            .Descendants(ns + "rootfile")
            .FirstOrDefault();

        return rootfile?
            .Attribute("full-path")?
            .Value;
    }
}