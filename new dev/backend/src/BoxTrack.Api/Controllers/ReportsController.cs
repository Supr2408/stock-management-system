using BoxTrack.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoxTrack.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/reports")]
public sealed class ReportsController(IReportCatalog catalog) : ControllerBase
{
    [HttpGet("daily-print")] public Task<IReadOnlyList<DailyPrintReportRow>> DailyPrint(DateOnly date, CancellationToken cancellationToken) => catalog.DailyPrintAsync(date, cancellationToken);
    [HttpGet("stock-summary")] public Task<IReadOnlyList<StockSummaryReportRow>> StockSummary(int? departmentId, int? itemId, CancellationToken cancellationToken) => catalog.StockSummaryAsync(departmentId, itemId, cancellationToken);
    [HttpGet("stock")] public Task<IReadOnlyList<StockReportRow>> Stock(int? departmentId, CancellationToken cancellationToken) => catalog.StockAsync(departmentId, cancellationToken);
    [HttpGet("item-detail")] public Task<ItemDetailReportRow?> ItemDetail(string barcode, CancellationToken cancellationToken) => catalog.ItemDetailAsync(barcode, cancellationToken);
    [HttpGet("batch")] public Task<IReadOnlyList<BatchReportRow>> Batch(string batchNumber, CancellationToken cancellationToken) => catalog.BatchAsync(batchNumber, cancellationToken);
    [HttpGet("customer/{customerId:int}")] public async Task<ActionResult<CustomerReportResult>> Customer(int customerId, CancellationToken cancellationToken) => (await catalog.CustomerAsync(customerId, cancellationToken)) is { } result ? Ok(result) : NotFound();
    [HttpGet("batch-wise")] public Task<IReadOnlyList<InventorySummaryReportRow>> BatchWise(string? batchNumber, CancellationToken cancellationToken) => catalog.BatchWiseAsync(batchNumber, cancellationToken);
    [HttpGet("product-wise")] public Task<IReadOnlyList<InventorySummaryReportRow>> ProductWise(int? itemId, CancellationToken cancellationToken) => catalog.ProductWiseAsync(itemId, cancellationToken);
    [HttpGet("sales-order-summary")] public Task<IReadOnlyList<SalesOrderSummaryReportRow>> SalesOrderSummary(string salesOrderNumber, CancellationToken cancellationToken) => catalog.SalesOrderSummaryAsync(salesOrderNumber, cancellationToken);
    [HttpGet("box-details")] public Task<IReadOnlyList<BoxDetailReportRow>> BoxDetails(int? departmentId, int? itemId, string? fromBarcode, string? toBarcode, CancellationToken cancellationToken) => catalog.BoxDetailsAsync(departmentId, itemId, fromBarcode, toBarcode, cancellationToken);
    [HttpGet("batches")] public Task<IReadOnlyList<string>> Batches(CancellationToken cancellationToken) => catalog.ListBatchesAsync(cancellationToken);
}
