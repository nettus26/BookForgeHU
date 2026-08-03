using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using BookForge.Core.Models;
using BookForge.Services;
using BookForge.Epub;
using BookForge.App.Services;

namespace BookForge.App;

public partial class MainWindow : Window
{
    private readonly ImportService importer;
    private readonly LibraryService library;
    private readonly CoverService coverService;

    private readonly List<Book> books = new();


    public MainWindow()
    {
        InitializeComponent();

        importer = new ImportService();

        library = new LibraryService();

        coverService = new CoverService();

        LoadLibrary();
    }



    private void LoadLibrary()
    {
        var savedBooks = library.GetBooks();


        foreach (var savedBook in savedBooks)
        {
            try
            {
                if (File.Exists(savedBook.FilePath))
                {
                    var reader = new EpubReader();

                    var fullBook =
                        reader.Load(savedBook.FilePath);


                    books.Add(fullBook);

                    BookList.Items.Add(fullBook);
                }
                else
                {
                    books.Add(savedBook);

                    BookList.Items.Add(savedBook);
                }
            }
            catch
            {
                books.Add(savedBook);

                BookList.Items.Add(savedBook);
            }
        }
    }



    private void AddEpub_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "EPUB könyv (*.epub)|*.epub"
        };


        if (dialog.ShowDialog() == true)
        {
            try
            {
                var book =
                    importer.ImportEpub(dialog.FileName);


                books.Add(book);

                BookList.Items.Add(book);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.ToString(),
                    "EPUB betöltési hiba");
            }
        }
    }



    private void DeleteBook_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (BookList.SelectedItem is Book book)
        {
            var result =
                MessageBox.Show(
                    $"Biztosan törlöd ezt a könyvet?\n\n{book.Title}",
                    "Könyv törlése",
                    MessageBoxButton.YesNo);


            if (result == MessageBoxResult.Yes)
            {
                library.RemoveBook(book);

                books.Remove(book);

                BookList.Items.Remove(book);

                ChapterList.Items.Clear();

                ContentText.Text =
                    "Válassz ki egy fejezetet";


                ChapterTitleText.Text = "";

                BookTitleText.Text = "";

                BookAuthorText.Text = "";

                BookLanguageText.Text = "";

                BookDateText.Text = "";

                CoverImageBox.Source = null;
            }
        }
    }
private void BookList_SelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (BookList.SelectedItem is Book book)
        {
            BookTitleText.Text = book.Title;

            BookAuthorText.Text = book.Author;

            BookLanguageText.Text = book.Language;

            BookDateText.Text =
                book.CreatedDate.ToString("yyyy.MM.dd.");


            LoadCover(book);


            ChapterList.Items.Clear();


            foreach (var chapter in book.Chapters)
            {
                ChapterList.Items.Add(chapter);
            }


            ContentText.Text =
                "Válassz ki egy fejezetet";


            ChapterTitleText.Text = "";
        }
    }



    private void LoadCover(Book book)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(book.CoverImage)
                &&
                File.Exists(book.CoverImage))
            {
                var image = new BitmapImage();


                image.BeginInit();

                image.UriSource =
                    new Uri(book.CoverImage);

                image.CacheOption =
                    BitmapCacheOption.OnLoad;

                image.EndInit();


                CoverImageBox.Source = image;

                return;
            }



            // Ha nincs EPUB borító,
            // készítünk egy saját BookForge borítót

            CoverImageBox.Source =
                coverService.CreateDefaultCover(
                    book.Title,
                    book.Author);
        }
        catch
        {
            CoverImageBox.Source =
                coverService.CreateDefaultCover(
                    book.Title,
                    book.Author);
        }
    }



    private void ChapterList_SelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ChapterList.SelectedItem is Chapter chapter)
        {
            ChapterTitleText.Text =
                chapter.Title;


            ContentText.Text =
                chapter.Content;
        }
    }
}