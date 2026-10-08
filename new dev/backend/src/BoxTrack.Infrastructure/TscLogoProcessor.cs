using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using BoxTrack.Application;
using BoxTrack.Domain;

namespace BoxTrack.Infrastructure;

public sealed class TscLogoProcessor : IImageProcessor
{
    public static int MillimetersToDots(double mm, int dpi = 203) =>
        (int)Math.Round(mm * (dpi / 25.4), MidpointRounding.AwayFromZero);

    public Task<(int Width, int Height)> GetImageDimensionsAsync(
        Stream imageStream,
        CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult((100, 100));
        }

        return Task.FromResult(GetWindowsImageDimensions(imageStream));
    }

    [SupportedOSPlatform("windows")]
    private static (int Width, int Height) GetWindowsImageDimensions(Stream imageStream)
    {
        using var img = Image.FromStream(imageStream, useEmbeddedColorManagement: false, validateImageData: true);
        return (img.Width, img.Height);
    }

    public Task<ProcessedLogoGraphic> ProcessLogoForThermalPrintAsync(
        Stream imageStream,
        double targetWidthMm,
        double targetHeightMm,
        LogoFitMode fitMode,
        int dpi = 203,
        CancellationToken cancellationToken = default)
    {
        var targetWidthDots = Math.Max(8, MillimetersToDots(targetWidthMm, dpi));
        var targetHeightDots = Math.Max(8, MillimetersToDots(targetHeightMm, dpi));

        if (!OperatingSystem.IsWindows())
        {
            var dummyBytes = new byte[((targetWidthDots + 7) / 8) * targetHeightDots];
            return Task.FromResult(new ProcessedLogoGraphic(targetWidthDots, targetHeightDots, (targetWidthDots + 7) / 8, dummyBytes, "image/x-tspl-bitmap"));
        }

        var result = ProcessWindowsLogoForThermalPrint(imageStream, targetWidthDots, targetHeightDots, fitMode);
        return Task.FromResult(result);
    }

    [SupportedOSPlatform("windows")]
    private static ProcessedLogoGraphic ProcessWindowsLogoForThermalPrint(
        Stream imageStream,
        int targetWidthDots,
        int targetHeightDots,
        LogoFitMode fitMode)
    {
        using var srcImg = Image.FromStream(imageStream, useEmbeddedColorManagement: false, validateImageData: true);
        var srcW = (double)srcImg.Width;
        var srcH = (double)srcImg.Height;

        using var canvas = new Bitmap(targetWidthDots, targetHeightDots, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(canvas))
        {
            g.Clear(Color.White);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.SmoothingMode = SmoothingMode.HighQuality;

            CalculateFitRectangles(
                srcW, srcH,
                targetWidthDots, targetHeightDots,
                fitMode,
                out var destRect, out var srcRect);

            g.DrawImage(srcImg, destRect, srcRect, GraphicsUnit.Pixel);
        }

        // Convert canvas to 1-bit monochrome TSPL-compatible raster (width_bytes = (targetWidthDots + 7) / 8)
        var widthBytes = (targetWidthDots + 7) / 8;
        var bitmapBytes = new byte[widthBytes * targetHeightDots];

        // Convert each pixel into black (0) or white (1) bit
        // In TSPL BITMAP command mode 0: 0 = black dot (burn), 1 = white dot (no burn)
        for (var y = 0; y < targetHeightDots; y++)
        {
            for (var x = 0; x < targetWidthDots; x++)
            {
                var pixel = canvas.GetPixel(x, y);
                // If transparent, treat as white background
                bool isBlack;
                if (pixel.A < 128)
                {
                    isBlack = false;
                }
                else
                {
                    // Luminance: standard ITU-R BT.601
                    var gray = (pixel.R * 299 + pixel.G * 587 + pixel.B * 114) / 1000;
                    isBlack = gray < 160; // Thresholding: darker than 160 is black dot
                }

                if (!isBlack)
                {
                    var byteIndex = y * widthBytes + (x / 8);
                    var bitIndex = 7 - (x % 8);
                    bitmapBytes[byteIndex] |= (byte)(1 << bitIndex);
                }
            }
        }

        return new ProcessedLogoGraphic(
            targetWidthDots,
            targetHeightDots,
            widthBytes,
            bitmapBytes,
            "image/x-tspl-bitmap"
        );
    }

    public static void CalculateFitRectangles(
        double srcW, double srcH,
        int targetW, int targetH,
        LogoFitMode fitMode,
        out RectangleF destRect,
        out RectangleF srcRect)
    {
        srcRect = new RectangleF(0, 0, (float)srcW, (float)srcH);

        if (fitMode == LogoFitMode.Stretch)
        {
            destRect = new RectangleF(0, 0, targetW, targetH);
            return;
        }

        var srcAspect = srcW / srcH;
        var targetAspect = (double)targetW / targetH;

        if (fitMode == LogoFitMode.Contain)
        {
            // Scale to fit completely inside target without cropping
            float drawW, drawH;
            if (srcAspect > targetAspect)
            {
                // Limiting factor is width
                drawW = targetW;
                drawH = (float)(targetW / srcAspect);
            }
            else
            {
                // Limiting factor is height
                drawH = targetH;
                drawW = (float)(targetH * srcAspect);
            }

            var destX = (targetW - drawW) / 2f;
            var destY = (targetH - drawH) / 2f;
            destRect = new RectangleF(destX, destY, drawW, drawH);
        }
        else // Cover
        {
            destRect = new RectangleF(0, 0, targetW, targetH);
            if (srcAspect > targetAspect)
            {
                // Source is wider than target -> crop left & right
                var cropW = (float)(srcH * targetAspect);
                var cropX = (float)((srcW - cropW) / 2f);
                srcRect = new RectangleF(cropX, 0, cropW, (float)srcH);
            }
            else
            {
                // Source is taller than target -> crop top & bottom
                var cropH = (float)(srcW / targetAspect);
                var cropY = (float)((srcH - cropH) / 2f);
                srcRect = new RectangleF(0, cropY, (float)srcW, cropH);
            }
        }
    }
}
