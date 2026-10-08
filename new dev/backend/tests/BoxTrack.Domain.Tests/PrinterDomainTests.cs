using BoxTrack.Domain;

namespace BoxTrack.Domain.Tests;

public sealed class PrinterDomainTests
{
    [Fact]
    public void PrinterConfiguration_InitializesWithCorrectDefaults()
    {
        var config = new PrinterConfiguration
        {
            Category = PrinterCategory.RegularDocument,
            PrinterName = "HP LaserJet M1005",
            CreatedBy = "admin"
        };

        Assert.Equal(PrinterCategory.RegularDocument, config.Category);
        Assert.Equal("HP LaserJet M1005", config.PrinterName);
        Assert.True(config.IsActive);
        Assert.Equal("admin", config.CreatedBy);
        Assert.True(config.CreatedAt <= DateTimeOffset.UtcNow);
        Assert.True(config.UpdatedAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void PrinterConfiguration_BarcodeCategory_InitializesProperly()
    {
        var config = new PrinterConfiguration
        {
            Category = PrinterCategory.Barcode,
            PrinterName = "Zebra ZD220"
        };

        Assert.Equal(PrinterCategory.Barcode, config.Category);
        Assert.Equal("Zebra ZD220", config.PrinterName);
        Assert.True(config.IsActive);
    }

    [Fact]
    public void PrintJob_InitializesWithQueuedStatusAndDefaults()
    {
        var job = new PrintJob
        {
            Category = PrinterCategory.Barcode,
            PrinterName = "Zebra ZD220",
            DocumentName = "Label_Batch_001",
            RequestedBy = "admin"
        };

        Assert.Equal(PrintJobStatus.Queued, job.Status);
        Assert.Equal(1, job.Copies);
        Assert.Equal(0, job.RetryCount);
        Assert.Null(job.StartedAt);
        Assert.Null(job.CompletedAt);
        Assert.Null(job.ErrorMessage);
    }

    [Fact]
    public void PrintJob_StatusTransitions_UpdateCorrectly()
    {
        var job = new PrintJob
        {
            Category = PrinterCategory.RegularDocument,
            PrinterName = "HP LaserJet M1005",
            DocumentName = "Dispatch_Slip_123"
        };

        job.Status = PrintJobStatus.Printing;
        job.StartedAt = DateTimeOffset.UtcNow;
        Assert.Equal(PrintJobStatus.Printing, job.Status);
        Assert.NotNull(job.StartedAt);

        job.Status = PrintJobStatus.Completed;
        job.CompletedAt = DateTimeOffset.UtcNow;
        Assert.Equal(PrintJobStatus.Completed, job.Status);
        Assert.NotNull(job.CompletedAt);
        Assert.Null(job.ErrorMessage);
    }

    [Fact]
    public void PrintJob_FailureState_TracksErrorMessage()
    {
        var job = new PrintJob
        {
            Category = PrinterCategory.RegularDocument,
            PrinterName = "Offline_Printer",
            DocumentName = "Report_A"
        };

        job.Status = PrintJobStatus.Failed;
        job.ErrorMessage = "Printer is offline or out of paper.";
        job.CompletedAt = DateTimeOffset.UtcNow;

        Assert.Equal(PrintJobStatus.Failed, job.Status);
        Assert.Equal("Printer is offline or out of paper.", job.ErrorMessage);
    }
}
