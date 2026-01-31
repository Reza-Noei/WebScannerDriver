namespace ScannerService.Core.DTOs;

public record ScanRequest(
    int ProfileId,
    string? OutputPath = null,
    string? OutputFormat = null
);

public record ScanResult(
    bool Success,
    List<string> Files,
    string? ErrorMessage,
    TimeSpan Duration
);

public record ExportSettingsDto(
    string OutputFormat,
    string OutputDirectory,
    string FileName
);