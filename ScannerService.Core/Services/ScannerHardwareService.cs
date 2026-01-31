using Microsoft.Extensions.Logging;
using NAPS2.Images;
using NAPS2.Pdf;
using NAPS2.Scan;
using ScannerService.Core.DTOs;
using ScannerService.Core.Interfaces;
using ScannerService.Core.Models;
using System.Runtime.InteropServices;

namespace ScannerService.Core.Services;

public class ScannerProvider : IScannerHardware, IAsyncDisposable
{
    private ScanningContext? _context;
    private ScanController? _controller;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _initialized;
    private readonly ILogger<ScannerProvider>? _logger;

    public ScannerProvider(ILogger<ScannerProvider>? logger = null)
    {
        _logger = logger;
    }

    private async Task InitializeAsync()
    {
        if (_initialized) return;

        await _lock.WaitAsync();
        try
        {
            if (_initialized) return;

            ImageContext imageContext = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? new NAPS2.Images.Gdi.GdiImageContext()
                : new NAPS2.Images.ImageSharp.ImageSharpImageContext();

            _context = new ScanningContext(imageContext);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    _context.SetUpWin32Worker();
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "TWAIN worker setup failed");
                }
            }

            _controller = new ScanController(_context);
            _initialized = true;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<ScannerDto>> GetScannersAsync()
    {
        await InitializeAsync();

        var scanners = new List<ScannerDto>();
        var drivers = GetDrivers();

        foreach (var driver in drivers)
        {
            try
            {
                var devices = await _controller!.GetDeviceList(driver);
                scanners.AddRange(devices.Select(d => new ScannerDto(d.ID, d.Name, driver.ToString())));
            }
            catch { }
        }

        return scanners;
    }

    public async Task<List<string>> ScanAsync(ScanJobConfiguration request)
    {
        await InitializeAsync();

        var device = await FindDeviceAsync(request.DeviceId);
        if (device == null)
            throw new InvalidOperationException($"Scanner not found: {request.DeviceId}");

        var options = new NAPS2.Scan.ScanOptions
        {
            Device = device,
            Dpi = request.Resolution,
            BitDepth = request.BitDepth switch
            {
                "BlackAndWhite" => BitDepth.BlackAndWhite,
                "Grayscale" => BitDepth.Grayscale,
                _ => BitDepth.Color
            },
            PaperSource = request.PaperSource.Equals("Feeder", StringComparison.OrdinalIgnoreCase)
                ? NAPS2.Scan.PaperSource.Feeder
                : NAPS2.Scan.PaperSource.Flatbed
        };

        var images = new List<ProcessedImage>();
        await foreach (var image in _controller!.Scan(options))
        {
            images.Add(image);
        }

        if (images.Count == 0)
            throw new InvalidOperationException("No images scanned");

        var files = await SaveAsync(images, request);

        foreach (var img in images) img.Dispose();

        return files;
    }

    private async Task<ScanDevice?> FindDeviceAsync(string deviceId)
    {
        foreach (var driver in GetDrivers())
        {
            try
            {
                var devices = await _controller!.GetDeviceList(driver);
                var device = devices.FirstOrDefault(d => d.ID == deviceId);
                if (device != null) return device;
            }
            catch { }
        }
        return null;
    }

    private async Task<List<string>> SaveAsync(List<ProcessedImage> images, ScanJobConfiguration req)
    {
        var files = new List<string>();
        var name = req.FileName.Replace("{datetime}", DateTime.Now.ToString("yyyyMMdd_HHmmss"));

        if (req.OutputFormat.Equals("PDF", StringComparison.OrdinalIgnoreCase))
        {
            var path = Path.Combine(req.OutputPath, $"{name}.pdf");
            await new PdfExporter(_context!).Export(path, images);
            files.Add(path);
        }
        else if (req.OutputFormat.Equals("MultiPageTIFF", StringComparison.OrdinalIgnoreCase))
        {
            for (int i = 0; i < images.Count; i++)
            {
                var path = Path.Combine(req.OutputPath, $"{name}_{i + 1}.tiff");
                images[i].Save(path, ImageFileFormat.Tiff);
                files.Add(path);
            }
        }
        else
        {
            var format = req.OutputFormat.ToLowerInvariant() switch
            {
                "png" => ImageFileFormat.Png,
                "tiff" => ImageFileFormat.Tiff,
                _ => ImageFileFormat.Jpeg
            };

            for (int i = 0; i < images.Count; i++)
            {
                var ext = req.OutputFormat.ToLowerInvariant();
                var path = Path.Combine(req.OutputPath, $"{name}_{i + 1}.{ext}");
                images[i].Save(path, format);
                files.Add(path);
            }
        }

        return files;
    }

    private static List<Driver> GetDrivers()
    {
        var drivers = new List<Driver>();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            drivers.Add(Driver.Twain);
            drivers.Add(Driver.Wia);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            drivers.Add(Driver.Sane);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            drivers.Add(Driver.Twain);
        }

        drivers.Add(Driver.Escl);
        return drivers;
    }

    public async ValueTask DisposeAsync()
    {
        _context?.Dispose();
        _lock.Dispose();
        await Task.CompletedTask;
    }
}