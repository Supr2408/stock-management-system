using System.Drawing.Printing;
using System.Runtime.Versioning;
using BoxTrack.Application;

namespace BoxTrack.Infrastructure;

public sealed class WindowsPrinterDiscoveryService : IPrinterDiscoveryService
{
    public Task<IReadOnlyList<AvailablePrinterDto>> GetAvailablePrintersAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<AvailablePrinterDto>();

        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult<IReadOnlyList<AvailablePrinterDto>>(result);
        }

        try
        {
            EnumerateWindowsPrinters(result);
        }
        catch
        {
            // Fallback gracefully if printer subsystem is temporarily unavailable
        }

        return Task.FromResult<IReadOnlyList<AvailablePrinterDto>>(result);
    }

    [SupportedOSPlatform("windows")]
    private static void EnumerateWindowsPrinters(List<AvailablePrinterDto> result)
    {
        foreach (string printerName in PrinterSettings.InstalledPrinters)
        {
            var isDefault = false;
            var status = "Ready";

            try
            {
                var settings = new PrinterSettings { PrinterName = printerName };
                isDefault = settings.IsDefaultPrinter;
                status = settings.IsValid ? "Ready" : "Offline";
            }
            catch
            {
                status = "Unknown";
            }

            result.Add(new AvailablePrinterDto(
                Name: printerName,
                DisplayName: printerName,
                Status: status,
                IsDefault: isDefault
            ));
        }
    }
}
