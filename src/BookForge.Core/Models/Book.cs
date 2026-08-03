using System;
using System.Collections.Generic;

namespace BookForge.Core.Models;

public class Book
{
    public string Title { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;


    // Könyv adatok
    public string Language { get; set; } = "hu";

    public string FilePath { get; set; } = string.Empty;

    public string CoverImage { get; set; } = string.Empty;

    public string ISBN { get; set; } = string.Empty;


    // Rendszer adatok
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public DateTime? LastOpened { get; set; }


    // Fejezetek
    public List<Chapter> Chapters { get; set; } = new();
}