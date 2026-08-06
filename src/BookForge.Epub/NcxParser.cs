using System.Xml.Linq;

using System.Xml.Linq;

namespace BookForge.Epub;

public class NcxParser
{
    public Dictionary<string, string> Parse(string ncx)
    {
        var result = new Dictionary<string, string>();

        var doc = XDocument.Parse(ncx);

        XNamespace ns = doc.Root?.Name.Namespace ?? "";

        foreach (var navPoint in doc.Descendants(ns + "navPoint"))
        {
            var title =
                navPoint.Element(ns + "navLabel")
                        ?.Element(ns + "text")
                        ?.Value
                        ?.Trim();

            var src =
                navPoint.Element(ns + "content")
                        ?.Attribute("src")
                        ?.Value
                        ?.Trim();

            if (string.IsNullOrWhiteSpace(title) ||
                string.IsNullOrWhiteSpace(src))
            {
                continue;
            }

            // chapter.xhtml#page12 -> chapter.xhtml
            var hashIndex = src.IndexOf('#');

            if (hashIndex >= 0)
            {
                src = src[..hashIndex];
            }

            // Ugyanaz a fájl csak egyszer szerepeljen
            if (!result.ContainsKey(src))
            {
                result.Add(src, title);

                System.Diagnostics.Debug.WriteLine(
                    $"NCX: {src} -> {title}");
            }
        }

        return result;
    }
}