using BookForge.Services;

Console.WriteLine("BookForge Import teszt");

var importer = new ImportService();

var book = importer.ImportEpub(
    @"C:\Users\missn\BookForgeHU\tests\HovaMentel.epub");


Console.WriteLine($"Cím: {book.Title}");
Console.WriteLine($"Szerző: {book.Author}");
Console.WriteLine($"Fejezetek száma: {book.Chapters.Count}");


foreach (var chapter in book.Chapters)
{
    Console.WriteLine($"- {chapter.Title}");
    Console.WriteLine($"  Fájl: {chapter.FilePath}");
    Console.WriteLine($"  Href: {chapter.Href}");
    Console.WriteLine($"  Szavak: {chapter.WordCount}");
    Console.WriteLine($"  Tartalom: {chapter.Content}");
}


// Könyvtár ellenőrzés
Console.WriteLine();
Console.WriteLine("Könyvtár ellenőrzés");

var library = new LibraryService();

var books = library.GetBooks();

Console.WriteLine($"Könyvtárban lévő könyvek: {books.Count}");

foreach (var item in books)
{
    Console.WriteLine($"- {item.Title} / {item.Author}");
    Console.WriteLine($"  Fejezetek: {item.Chapters.Count}");
}

Console.WriteLine();
Console.WriteLine("Nyomj meg egy gombot a kilépéshez...");
Console.ReadKey();