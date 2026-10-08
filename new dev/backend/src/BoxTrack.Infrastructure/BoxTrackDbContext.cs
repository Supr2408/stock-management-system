using BoxTrack.Domain;
using Microsoft.EntityFrameworkCore;

namespace BoxTrack.Infrastructure;

public sealed class BoxTrackDbContext(DbContextOptions<BoxTrackDbContext> options) : DbContext(options)
{
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<LabelLogo> LabelLogos => Set<LabelLogo>();
    public DbSet<BarcodeLabel> BarcodeLabels => Set<BarcodeLabel>();
    public DbSet<StockReceipt> StockReceipts => Set<StockReceipt>();
    public DbSet<DispatchRecord> DispatchRecords => Set<DispatchRecord>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<PrinterConfiguration> PrinterConfigurations => Set<PrinterConfiguration>();
    public DbSet<PrintJob> PrintJobs => Set<PrintJob>();
    public DbSet<LabelTemplate> LabelTemplates => Set<LabelTemplate>();
    public DbSet<LabelTemplateElement> LabelTemplateElements => Set<LabelTemplateElement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
            foreach (var property in entity.GetProperties())
                property.SetColumnName(ToSnakeCase(property.Name));
        modelBuilder.Entity<Department>().ToTable("department").HasIndex(x => x.Name).IsUnique();
        modelBuilder.Entity<Item>().ToTable("item").HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<Item>().Property(x => x.GrossWeightKg).HasPrecision(8, 2);
        modelBuilder.Entity<Item>().HasOne(x => x.Department).WithMany(x => x.Items).HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Customer>().ToTable("customer");
        modelBuilder.Entity<LabelLogo>().ToTable("label_logo").HasIndex(x => x.Name);
        modelBuilder.Entity<BarcodeLabel>().ToTable("barcode_label").HasIndex(x => x.BarcodeValue).IsUnique();
        modelBuilder.Entity<BarcodeLabel>().Property(x => x.ProductionStatus).HasDefaultValue("PendingProduction");
        modelBuilder.Entity<BarcodeLabel>().HasIndex(x => x.ProductionStatus);
        modelBuilder.Entity<BarcodeLabel>().HasOne(x => x.Item).WithMany(x => x.BarcodeLabels).HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<BarcodeLabel>().HasOne(x => x.LabelLogo).WithMany(x => x.BarcodeLabels).HasForeignKey(x => x.LabelLogoId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<BarcodeLabel>().HasOne(x => x.StockReceipt).WithMany(x => x.BarcodeLabels).HasForeignKey("StockReceiptId").OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<BarcodeLabel>().HasOne(x => x.DispatchRecord).WithMany(x => x.BarcodeLabels).HasForeignKey(x => x.DispatchRecordId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<StockReceipt>().ToTable("stock_receipt");
        modelBuilder.Entity<StockReceipt>().HasIndex(x => x.BatchNumber);
        modelBuilder.Entity<StockReceipt>().HasOne(x => x.Item).WithMany(x => x.StockReceipts).HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<DispatchRecord>().ToTable("dispatch_record");
        modelBuilder.Entity<DispatchRecord>().HasIndex(x => x.SalesOrderNumber);
        modelBuilder.Entity<DispatchRecord>().HasIndex(x => x.InvoiceNumber);
        modelBuilder.Entity<DispatchRecord>().HasOne(x => x.Customer).WithMany(x => x.Dispatches).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<AuditLog>().ToTable("audit_log");
        modelBuilder.Entity<PrinterConfiguration>().ToTable("printer_configuration").HasIndex(x => x.Category);
        modelBuilder.Entity<PrinterConfiguration>().HasOne(x => x.ActiveTemplate).WithMany().HasForeignKey(x => x.ActiveTemplateId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<PrintJob>().ToTable("print_job").HasIndex(x => x.CreatedAt);
        modelBuilder.Entity<LabelTemplate>().ToTable("label_template").HasIndex(x => x.Name);
        modelBuilder.Entity<LabelTemplate>().HasMany(x => x.Elements).WithOne(x => x.Template).HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<LabelTemplateElement>().ToTable("label_template_element");
        modelBuilder.Entity<LabelTemplateElement>().HasOne(x => x.Logo).WithMany().HasForeignKey(x => x.LogoId).OnDelete(DeleteBehavior.SetNull);
    }

    private static string ToSnakeCase(string value) => string.Concat(value.Select((character, index) => index > 0 && char.IsUpper(character) ? "_" + char.ToLowerInvariant(character) : character.ToString().ToLowerInvariant()));
}
