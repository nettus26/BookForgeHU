
using BookForge.Epub;

Console.WriteLine("BookForge EPUB teszt");

var reader = new EpubReader();

var book = reader.Load(@"C:\Users\missn\BookForgeHU\tests\TestBook.epub");

Console.WriteLine($"Cím: {book.Title}");
Console.WriteLine($"Szerző: {book.Author}");
Console.WriteLine($"Fejezetek száma: {book.Chapters.Count}");

foreach (var chapter in book.Chapters)
{
    Console.WriteLine($"- {chapter.Title}");
    Console.WriteLine($"  Tartalom: {chapter.Content}");
}