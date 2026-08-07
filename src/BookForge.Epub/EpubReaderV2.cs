using BookForge.Core.Models;
using BookForge.Epub.Helpers;
using BookForge.Epub.Parsers;
using System.IO.Compression;

namespace BookForge.Epub;

public class EpubReaderV2
{
    public Book Load(string filePath)
    {
        using var package = new EpubPackage(filePath);
        using var archive = package.Open();

        return ReadBook(archive);
    }

    private Book ReadBook(ZipArchive archive)
    {
        var contentPath = FindContentPath(archive);

        throw new NotImplementedException();
    }

    private string FindContentPath(ZipArchive archive)
    {
        var contentReader = new EpubContentReader();

        var containerXml =
            contentReader.ReadEntry(
                archive,
                "META-INF/container.xml");

        var parser = new ContainerParser();

        var contentPath =
            parser.FindContentPath(containerXml);

        if (string.IsNullOrWhiteSpace(contentPath))
        {
            throw new Exception(
                "Nem található a content.opf.");
        }

        return contentPath;
    }
}