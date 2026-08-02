using BookForge.Core.Models;

namespace BookForge.Epub.Interfaces;

public interface IEpubReader
{
    Book Load(string filePath);
}