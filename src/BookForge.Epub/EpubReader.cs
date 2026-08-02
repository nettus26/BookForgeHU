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

        var containerEntry = archive.GetEntry("META-INF/container.xml");

        if (containerEntry == null)
            throw new Exception("Nem található container.xml");

        using var containerReader = new StreamReader(containerEntry.Open());

        var containerXml = containerReader.ReadToEnd();

        var containerParser = new ContainerParser();

        var contentPath = containerParser.FindContentPath(containerXml);

        if (contentPath == null)
            throw new Exception("Nem található content.opf");

        var contentEntry = archive.GetEntry(contentPath);

        if (contentEntry == null)
            throw new Exception("Nem található OPF fájl");

        using var contentReader = new StreamReader(contentEntry.Open());

        var contentXml = contentReader.ReadToEnd();

        var contentParser = new ContentParser();

        var book = contentParser.Parse(contentXml);

        return book;
    }
}