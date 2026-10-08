using System.Text;
using BoxTrack.Api.Middleware;
using BoxTrack.Application;
using BoxTrack.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
var projectRoot = Directory.GetParent(builder.Environment.ContentRootPath)?.Parent?.Parent?.FullName ?? builder.Environment.ContentRootPath;

// Load local .env file if present
var envPath = Path.Combine(projectRoot, ".env");
if (File.Exists(envPath))
{
    foreach (var line in File.ReadAllLines(envPath))
    {
        var trimmed = line.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#')) continue;
        var parts = trimmed.Split('=', 2);
        if (parts.Length == 2)
        {
            Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
            builder.Configuration[parts[0].Trim()] = parts[1].Trim();
        }
    }
}

builder.Host.UseSerilog((context, logger) => logger.ReadFrom.Configuration(context.Configuration).WriteTo.Console());
builder.Services.AddSingleton<MasterService>();
builder.Services.AddDbContext<BoxTrackDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("Default") ?? "Host=localhost;Port=5432;Database=boxtrack;Username=boxtrack;Password=boxtrack_local_only"));
builder.Services.AddScoped<IItemCatalog, ItemCatalogService>();
builder.Services.AddScoped<IDepartmentCatalog, DepartmentCatalogService>();
builder.Services.AddScoped<ICustomerCatalog, CustomerCatalogService>();
builder.Services.AddScoped<IPrinterDiscoveryService, WindowsPrinterDiscoveryService>();
builder.Services.AddScoped<IPrintService, WindowsPrintService>();
builder.Services.AddScoped<IPrinterCatalog, PrinterCatalogService>();
builder.Services.AddScoped<IImageProcessor, TscLogoProcessor>();
builder.Services.AddScoped<IPrinterTransport, WindowsRawPrinterTransport>();
builder.Services.AddScoped<ITscCommandGenerator>(provider =>
{
    var imageProc = provider.GetRequiredService<IImageProcessor>();
    var logger = provider.GetRequiredService<ILogger<TscTsplCommandGenerator>>();
    return new TscTsplCommandGenerator(imageProc, Path.Combine(projectRoot, "barcode"), logger);
});
builder.Services.AddScoped<ILabelTemplateCatalog, LabelTemplateCatalogService>();
builder.Services.AddScoped<ILabelCatalog>(provider =>
{
    var database = provider.GetRequiredService<BoxTrackDbContext>();
    var printService = provider.GetRequiredService<IPrintService>();
    return new LabelCatalogService(database, Path.Combine(projectRoot, "barcode"), printService);
});
builder.Services.AddScoped<IProductionCatalog, ProductionCatalogService>();
builder.Services.AddScoped<IDispatchCatalog, DispatchCatalogService>();
builder.Services.AddScoped<IReportCatalog, ReportCatalogService>();

var jwtSecret = builder.Configuration["JWT_SECRET"] ?? "development-only-secret-change-me-please";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options => options.TokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuerSigningKey = true,
    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
    ValidateIssuer = false,
    ValidateAudience = false,
});
builder.Services.AddAuthorization();
builder.Services.AddCors(options => options.AddPolicy("LocalFrontend", policy => policy
    .WithOrigins("http://127.0.0.1:5173", "http://localhost:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()));

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddHealthChecks();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var database = scope.ServiceProvider.GetRequiredService<BoxTrackDbContext>();
    await database.Database.MigrateAsync();
    await SeedData.InitializeAsync(database);
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
var barcodeRoot = Path.Combine(projectRoot, "barcode");
Directory.CreateDirectory(barcodeRoot);
app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(barcodeRoot), RequestPath = "/barcode" });

app.UseCors("LocalFrontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
