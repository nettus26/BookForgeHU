using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using BookForge.Core.Models;
using BookForge.Services;
using BookForge.Epub;
using BookForge.App.Services;
using Microsoft.Web.WebView2.Wpf;

namespace BookForge.App;

public partial class MainWindow : Window
{
    private readonly ImportService importer;
    private readonly LibraryService library;
    private readonly CoverService coverService;

    private readonly List<Book> books = new();

    private readonly WebView2 contentViewer;


    public MainWindow()
    {
        InitializeComponent();

        contentViewer = new WebView2();

        ReaderHost.Children.Add(contentViewer);

        importer = new ImportService();

        library = new LibraryService();

        coverService = new CoverService();

        LoadLibrary();

        Loaded += MainWindow_Loaded;
    }


    private async void MainWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            await contentViewer.EnsureCoreWebView2Async();

            contentViewer.NavigateToString("""
                <!DOCTYPE html>
                <html>
                <body style="font-family: Georgia, serif; margin: 30px;">
                    <p>Válassz ki egy fejezetet.</p>
                </body>
                </html>
                """);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "WebView2 hiba");
        }
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


    private void AddEpub_Click(
        object sender,
        RoutedEventArgs e)
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

                contentViewer.NavigateToString("""
                    <!DOCTYPE html>
                    <html>
                    <body>
                        <p>Válassz ki egy fejezetet.</p>
                    </body>
                    </html>
                    """);
            }
        }
    }


    private void BookList_SelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (BookList.SelectedItem is Book book)
        {
            BookTitleText.Text =
                book.Title;

            BookAuthorText.Text =
                book.Author;

            BookLanguageText.Text =
                book.Language;

            BookDateText.Text =
                book.CreatedDate.ToString(
                    "yyyy.MM.dd.");

            LoadCover(book);

            ChapterList.Items.Clear();

            foreach (var chapter in book.Chapters)
            {
                ChapterList.Items.Add(chapter);
            }
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

                CoverImageBox.Source =
                    image;

                return;
            }

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


            MessageBox.Show(
                $"Fejezet: {chapter.Title}\n\n" +
                $"HtmlContent hossza: {chapter.HtmlContent?.Length ?? 0} karakter",
                "EPUB ellenőrzés");


            if (!string.IsNullOrWhiteSpace(
                chapter.HtmlContent))
            {
                try
                {
                    contentViewer.NavigateToString(
                        chapter.HtmlContent);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        ex.ToString(),
                        "Fejezet megjelenítési hiba");
                }
            }
        }
    }
}