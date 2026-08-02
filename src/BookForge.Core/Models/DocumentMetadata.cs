namespace BookForge.Core.Models;

public class DocumentMetadata
{
    public string Language { get; set; } = "hu-HU";

    public string Publisher { get; set; } = string.Empty;

    public string ISBN { get; set; } = string.Empty;

    public string CoverImagePath { get; set; } = string.Empty;
}