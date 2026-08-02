using System;

namespace BookForge.Core.Models;

public class Chapter
{
    public string Title { get; set; } = string.Empty;

    public int Order { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; } = DateTime.Now;
}