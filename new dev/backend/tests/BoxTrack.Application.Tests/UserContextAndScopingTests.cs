using BoxTrack.Application;
using BoxTrack.Domain;
using BoxTrack.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BoxTrack.Application.Tests;

public class TestCurrentUserService : ICurrentUserService
{
    public string? UserId { get; set; } = "1";
    public string? Username { get; set; } = "admin";
    public string? Role { get; set; } = "Admin";
    public int? DepartmentId { get; set; }
    public bool IsAdmin => Role == "Admin";
    public bool IsProduction => Role == "Production";
    public bool IsQc => Role == "QC";
}

public class UserContextAndScopingTests
{
    private BoxTrackDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<BoxTrackDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new BoxTrackDbContext(options);
    }

    [Fact]
    public void NormalizeDepartmentUsername_FormatsCorrectly()
    {
        Assert.Equal("assembly", SeedData.NormalizeDepartmentUsername("Assembly"));
        Assert.Equal("plascon", SeedData.NormalizeDepartmentUsername("PLASCON"));
        Assert.Equal("afcafr", SeedData.NormalizeDepartmentUsername("AFC / AFR"));
        Assert.Equal("dept1", SeedData.NormalizeDepartmentUsername("Dept #1"));
    }

    [Fact]
    public async Task ProductionUser_ListPending_OnlyReturnsOwnDepartment()
    {
        using var db = CreateInMemoryContext();

        var dept1 = new Department { Id = 1, Name = "Assembly", IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        var dept2 = new Department { Id = 2, Name = "Molding", IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        db.Departments.AddRange(dept1, dept2);

        var item1 = new Item { Id = 1, Code = 101, Name = "Widget A", DepartmentId = 1, Department = dept1, IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        var item2 = new Item { Id = 2, Code = 201, Name = "Widget B", DepartmentId = 2, Department = dept2, IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        db.Items.AddRange(item1, item2);

        var label1 = new BarcodeLabel
        {
            Id = 1,
            ItemId = 1,
            Item = item1,
            ItemCode = 101,
            BarcodeValue = "BOX-001",
            ManufactureDate = new DateOnly(2026, 10, 8),
            SerialNumber = 1,
            ProductionStatus = "PendingProduction",
            FileName = "BOX-001.png",
            RelativePath = "BOX-001.png",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var label2 = new BarcodeLabel
        {
            Id = 2,
            ItemId = 2,
            Item = item2,
            ItemCode = 201,
            BarcodeValue = "BOX-002",
            ManufactureDate = new DateOnly(2026, 10, 8),
            SerialNumber = 2,
            ProductionStatus = "PendingProduction",
            FileName = "BOX-002.png",
            RelativePath = "BOX-002.png",
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.BarcodeLabels.AddRange(label1, label2);
        await db.SaveChangesAsync();

        // Production user for Dept 1
        var productionUser = new TestCurrentUserService
        {
            Username = "assembly",
            Role = "Production",
            DepartmentId = 1
        };

        var service = new ProductionCatalogService(db, productionUser);

        // Even if requesting departmentId 2 or null, production user is forced to department 1
        var result = await service.ListPendingAsync(departmentId: 2, null, null, null, 1, 100, default);

        Assert.Equal(1, result.Total);
        Assert.Single(result.Items);
        Assert.Equal(101, result.Items[0].ItemCode);
        Assert.Equal("Assembly", result.Items[0].DepartmentName);
    }

    [Fact]
    public async Task AdminUser_ListPending_CanViewAllDepartments()
    {
        using var db = CreateInMemoryContext();

        var dept1 = new Department { Id = 1, Name = "Assembly", IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        var dept2 = new Department { Id = 2, Name = "Molding", IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        db.Departments.AddRange(dept1, dept2);

        var item1 = new Item { Id = 1, Code = 101, Name = "Widget A", DepartmentId = 1, Department = dept1, IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        var item2 = new Item { Id = 2, Code = 201, Name = "Widget B", DepartmentId = 2, Department = dept2, IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        db.Items.AddRange(item1, item2);

        db.BarcodeLabels.AddRange(
            new BarcodeLabel { Id = 1, ItemId = 1, Item = item1, ItemCode = 101, BarcodeValue = "BOX-001", ManufactureDate = new DateOnly(2026, 10, 8), SerialNumber = 1, ProductionStatus = "PendingProduction", FileName = "BOX-001.png", RelativePath = "BOX-001.png", CreatedAt = DateTimeOffset.UtcNow },
            new BarcodeLabel { Id = 2, ItemId = 2, Item = item2, ItemCode = 201, BarcodeValue = "BOX-002", ManufactureDate = new DateOnly(2026, 10, 8), SerialNumber = 2, ProductionStatus = "PendingProduction", FileName = "BOX-002.png", RelativePath = "BOX-002.png", CreatedAt = DateTimeOffset.UtcNow }
        );
        await db.SaveChangesAsync();

        var adminUser = new TestCurrentUserService
        {
            Username = "admin",
            Role = "Admin",
            DepartmentId = null
        };

        var service = new ProductionCatalogService(db, adminUser);

        // Admin requesting all departments
        var resultAll = await service.ListPendingAsync(departmentId: null, null, null, null, 1, 100, default);
        Assert.Equal(2, resultAll.Total);

        // Admin requesting specific department
        var resultDept2 = await service.ListPendingAsync(departmentId: 2, null, null, null, 1, 100, default);
        Assert.Equal(1, resultDept2.Total);
        Assert.Equal(201, resultDept2.Items[0].ItemCode);
    }

    [Fact]
    public async Task ProductionUser_AddToStock_CannotAddOtherDepartmentItem()
    {
        using var db = CreateInMemoryContext();

        var dept2 = new Department { Id = 2, Name = "Molding", IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        db.Departments.Add(dept2);

        var item2 = new Item { Id = 2, Code = 201, Name = "Widget B", DepartmentId = 2, Department = dept2, IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        db.Items.Add(item2);

        db.BarcodeLabels.Add(new BarcodeLabel
        {
            Id = 2,
            ItemId = 2,
            Item = item2,
            ItemCode = 201,
            BarcodeValue = "BOX-002",
            ManufactureDate = new DateOnly(2026, 10, 8),
            SerialNumber = 2,
            ProductionStatus = "PendingProduction",
            FileName = "BOX-002.png",
            RelativePath = "BOX-002.png",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        // Production user is assigned to Department 1
        var productionUser = new TestCurrentUserService
        {
            Username = "assembly",
            Role = "Production",
            DepartmentId = 1
        };

        var service = new ProductionCatalogService(db, productionUser);

        var input = new AddToStockInput(
            BatchNumber: "BATCH-100",
            FromBarcode: "BOX-002",
            ToBarcode: "BOX-002");

        // Attempting to add stock for Department 2 item must throw InvalidOperationException
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AddToStockAsync(input, "assembly", default));

        Assert.Contains("assigned department", ex.Message);
    }
}
