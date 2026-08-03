
using BookForge.Epub;
using BookForge.Services;

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


// Könyvtár teszt
Console.WriteLine();
Console.WriteLine("Könyvtár teszt");

var library = new LibraryService();

library.AddBook(book);

var books = library.GetBooks();

Console.WriteLine($"Könyvtárban lévő könyvek: {books.Count}");

foreach (var item in books)
{
    Console.WriteLine($"- {item.Title} / {item.Author}");
}