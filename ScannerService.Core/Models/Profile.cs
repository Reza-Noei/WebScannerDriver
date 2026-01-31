namespace ScannerService.Core.Models;

public class Profile
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? DeviceId { get; set; }
    public string PaperSource { get; set; } = "Glass"; // Glass, Feeder
    public string BitDepth { get; set; } = "Color"; // Color, Grayscale, BlackAndWhite
    public string PageSize { get; set; } = "A4";
    public string HorizontalAlign { get; set; } = "Center"; // Left, Center, Right
    public int Resolution { get; set; } = 200;
    public string Scale { get; set; } = "1:1"; // 1:1, 1:2, 1:4, 1:8
    public int Brightness { get; set; } = 0; // -100 to 100
    public int Contrast { get; set; } = 0; // -100 to 100
    public int ImageQuality { get; set; } = 85; // 0-100
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}