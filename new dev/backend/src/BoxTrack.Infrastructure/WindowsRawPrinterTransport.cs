using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using BoxTrack.Application;
using Microsoft.Extensions.Logging;

namespace BoxTrack.Infrastructure;

public sealed class WindowsRawPrinterTransport(ILogger<WindowsRawPrinterTransport> logger) : IPrinterTransport
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private class DocInfoA
    {
        [MarshalAs(UnmanagedType.LPStr)]
        public string? pDocName;
        [MarshalAs(UnmanagedType.LPStr)]
        public string? pOutputFile;
        [MarshalAs(UnmanagedType.LPStr)]
        public string? pDataType;
    }

    [DllImport("winspool.drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPStr)] string szPrinter, out IntPtr hPrinter, IntPtr pd);

    [DllImport("winspool.drv", EntryPoint = "ClosePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool StartDocPrinter(IntPtr hPrinter, int level, [In, MarshalAs(UnmanagedType.LPStruct)] DocInfoA di);

    [DllImport("winspool.drv", EntryPoint = "EndDocPrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "StartPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "EndPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "WritePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

    public Task SendRawAsync(string printerName, byte[] data, string documentName, CancellationToken cancellationToken = default)
    {
        if (data == null || data.Length == 0)
        {
            throw new ArgumentException("Cannot send empty data to printer.", nameof(data));
        }

        if (!OperatingSystem.IsWindows())
        {
            logger.LogInformation("Non-Windows OS: simulated raw send of {ByteCount} bytes to {PrinterName} ({DocumentName})", data.Length, printerName, documentName);
            return Task.CompletedTask;
        }

        SendBytesToPrinter(printerName, data, documentName);
        return Task.CompletedTask;
    }

    public Task<string> GetPrinterStatusAsync(string printerName, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows()) return Task.FromResult("Ready");

        try
        {
            if (OpenPrinter(printerName, out var hPrinter, IntPtr.Zero))
            {
                ClosePrinter(hPrinter);
                return Task.FromResult("Ready");
            }
            return Task.FromResult("Offline");
        }
        catch
        {
            return Task.FromResult("Unknown");
        }
    }

    [SupportedOSPlatform("windows")]
    private void SendBytesToPrinter(string szPrinterName, byte[] pBytes, string docName)
    {
        var di = new DocInfoA
        {
            pDocName = docName,
            pDataType = "RAW"
        };

        if (!OpenPrinter(szPrinterName.Normalize(), out var hPrinter, IntPtr.Zero))
        {
            var err = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"Could not open printer '{szPrinterName}'. Win32 Error: {err}");
        }

        try
        {
            if (!StartDocPrinter(hPrinter, 1, di))
            {
                var err = Marshal.GetLastWin32Error();
                throw new InvalidOperationException($"Could not start print document on '{szPrinterName}'. Win32 Error: {err}");
            }

            try
            {
                if (!StartPagePrinter(hPrinter))
                {
                    var err = Marshal.GetLastWin32Error();
                    throw new InvalidOperationException($"Could not start page printer on '{szPrinterName}'. Win32 Error: {err}");
                }

                try
                {
                    var pUnmanagedBytes = Marshal.AllocCoTaskMem(pBytes.Length);
                    try
                    {
                        Marshal.Copy(pBytes, 0, pUnmanagedBytes, pBytes.Length);
                        if (!WritePrinter(hPrinter, pUnmanagedBytes, pBytes.Length, out var dwWritten) || dwWritten != pBytes.Length)
                        {
                            var err = Marshal.GetLastWin32Error();
                            throw new InvalidOperationException($"WritePrinter failed on '{szPrinterName}'. Written {dwWritten} of {pBytes.Length}. Win32 Error: {err}");
                        }
                    }
                    finally
                    {
                        Marshal.FreeCoTaskMem(pUnmanagedBytes);
                    }
                }
                finally
                {
                    EndPagePrinter(hPrinter);
                }
            }
            finally
            {
                EndDocPrinter(hPrinter);
            }
        }
        finally
        {
            ClosePrinter(hPrinter);
        }
    }
}
