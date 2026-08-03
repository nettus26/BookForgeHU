using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BookForge.App.Services;

public class CoverService
{
    public BitmapSource CreateDefaultCover(
        string title,
        string author)
    {
        const int width = 600;
        const int height = 900;


        var visual = new DrawingVisual();


        using (var context = visual.RenderOpen())
        {
            // háttér
            context.DrawRectangle(
                Brushes.White,
                null,
                new Rect(0, 0, width, height));


            // felső dísz
            context.DrawRectangle(
                new SolidColorBrush(
                    Color.FromRgb(70, 60, 150)),
                null,
                new Rect(0, 0, width, 140));



            // könyv ikon
            var icon =
                new FormattedText(
                    "📖",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(
                        new FontFamily("Segoe UI"),
                        FontStyles.Normal,
                        FontWeights.Normal,
                        FontStretches.Normal),
                    90,
                    Brushes.Black,
                    1.0);


            context.DrawText(
                icon,
                new Point(250, 150));



            // cím
            var titleText =
                new FormattedText(
                    string.IsNullOrWhiteSpace(title)
                        ? "BookForge"
                        : title,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(
                        new FontFamily("Segoe UI"),
                        FontStyles.Normal,
                        FontWeights.Bold,
                        FontStretches.Normal),
                    48,
                    Brushes.Black,
                    1.0);


            context.DrawText(
                titleText,
                new Point(70, 330));



            // szerző
            var authorText =
                new FormattedText(
                    string.IsNullOrWhiteSpace(author)
                        ? ""
                        : author,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(
                        new FontFamily("Segoe UI"),
                        FontStyles.Normal,
                        FontWeights.Normal,
                        FontStretches.Normal),
                    30,
                    Brushes.DarkSlateGray,
                    1.0);


            context.DrawText(
                authorText,
                new Point(70, 450));



            // alsó vonal
            context.DrawRectangle(
                new SolidColorBrush(
                    Color.FromRgb(70, 60, 150)),
                null,
                new Rect(70, 720, 460, 3));



            // BookForge
            var logo =
                new FormattedText(
                    "BookForge",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(
                        new FontFamily("Segoe UI"),
                        FontStyles.Normal,
                        FontWeights.Bold,
                        FontStretches.Normal),
                    32,
                    Brushes.Gray,
                    1.0);


            context.DrawText(
                logo,
                new Point(70, 760));
        }



        var bitmap =
            new RenderTargetBitmap(
                width,
                height,
                96,
                96,
                PixelFormats.Pbgra32);


        bitmap.Render(visual);


        return bitmap;
    }
}