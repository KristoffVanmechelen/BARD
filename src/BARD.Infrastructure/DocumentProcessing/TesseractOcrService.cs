using System.Collections.Concurrent;
using System.Diagnostics;
using BARD.Application.Common.Options;
using BARD.Application.DocumentProcessing.Interfaces;
using Microsoft.Extensions.Options;
using PDFtoImage;
using SkiaSharp;

namespace BARD.Infrastructure.DocumentProcessing;

/// <summary>
/// OCR fallback for scanned PDF pages.
/// Pages are rendered with PDFtoImage and passed to the system Tesseract
/// executable through stdin. This avoids native-wrapper/tessdata issues
/// in the Codespace runtime.
/// </summary>
public sealed class TesseractOcrService : IOcrService
{
    private readonly OcrOptions _options;

    public TesseractOcrService(
        IOptions<OcrOptions> options)
    {
        _options = options.Value;
    }

    public async Task<IReadOnlyDictionary<int, string>> OcrPagesAsync(
        Stream pdfStream,
        IReadOnlyList<int> pageNumbers,
        CancellationToken ct = default)
    {
        using var memoryStream =
            new MemoryStream();

        if (pdfStream.CanSeek)
            pdfStream.Position = 0;

        await pdfStream.CopyToAsync(
            memoryStream,
            ct);

        var pdfBytes =
            memoryStream.ToArray();

        var pages =
            pageNumbers
                .Distinct()
                .OrderBy(x => x)
                .ToArray();

        var results =
            new ConcurrentDictionary<int, string>();

        using var gate =
            new SemaphoreSlim(2);

        var tasks =
            pages.Select(
                async pageNumber =>
                {
                    await gate.WaitAsync(ct);

                    try
                    {
                        ct.ThrowIfCancellationRequested();

                        using var bitmap =
                            Conversion.ToImage(
                                pdfBytes,
                                page: pageNumber,
                                options: new(
                                    Dpi: _options.Dpi));

                        using var pngStream =
                            new MemoryStream();

                        bitmap.Encode(
                            pngStream,
                            SKEncodedImageFormat.Png,
                            100);

                        var startInfo =
                            new ProcessStartInfo
                            {
                                FileName = "tesseract",
                                RedirectStandardInput = true,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                UseShellExecute = false,
                                CreateNoWindow = true,
                            };

                        startInfo.ArgumentList.Add("stdin");
                        startInfo.ArgumentList.Add("stdout");
                        startInfo.ArgumentList.Add("-l");
                        startInfo.ArgumentList.Add(
                            _options.Language);

                        if (!string.IsNullOrWhiteSpace(
                                _options.TessDataPath)
                            && Directory.Exists(
                                _options.TessDataPath))
                        {
                            startInfo.ArgumentList.Add(
                                "--tessdata-dir");

                            startInfo.ArgumentList.Add(
                                _options.TessDataPath);
                        }

                        using var process =
                            Process.Start(startInfo)
                            ?? throw new InvalidOperationException(
                                "Could not start the Tesseract OCR process.");

                        var outputTask =
                            process.StandardOutput
                                .ReadToEndAsync(ct);

                        var errorTask =
                            process.StandardError
                                .ReadToEndAsync(ct);

                        await process.StandardInput.BaseStream
                            .WriteAsync(
                                pngStream.ToArray(),
                                ct);

                        await process.StandardInput.BaseStream
                            .FlushAsync(ct);

                        process.StandardInput.Close();

                        await process.WaitForExitAsync(ct);

                        var output =
                            await outputTask;

                        var error =
                            await errorTask;

                        if (process.ExitCode != 0)
                        {
                            throw new InvalidOperationException(
                                $"Tesseract exited with code {process.ExitCode}: {error}");
                        }

                        results[pageNumber] = output;
                    }
                    finally
                    {
                        gate.Release();
                    }
                });

        await Task.WhenAll(tasks);

        return results
            .OrderBy(pair => pair.Key)
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value);
    }
}
