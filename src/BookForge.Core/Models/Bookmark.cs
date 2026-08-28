using System;

namespace BookForge.Core.Models;

public class Bookmark
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string ChapterPath { get; set; } = string.Empty;

    public double ScrollPosition { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
