using DocConversionService.Api.Middleware;
using DocConversionService.Application.Configuration;
using DocConversionService.Application.Interfaces;
using DocConversionService.Application.Jobs;
using DocConversionService.Domain.Enums;
using DocConversionService.Infrastructure.Parsing;
using DocConversionService.Infrastructure.Persistence;
using DocConversionService.Infrastructure.Rendering;
using DocConversionService.Infrastructure.Splitting;
using DocConversionService.Infrastructure.Storage;
using DocConversionService.Infrastructure.Validation;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure
builder.Services.Configure<ConversionSettings>(builder.Configuration.GetSection("Conversion"));
builder.Services.Configure<StorageSettings>(builder.Configuration.GetSection("Storage"));

// DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

// Core services
builder.Services.AddScoped<IJobRepository, JobRepository>();
builder.Services.AddSingleton<IDocumentParser, PdfDocumentParser>();
builder.Services.AddSingleton<IDocumentSplitter, DocumentSplitter>();
builder.Services.AddSingleton<IJobValidator, JobValidator>();
builder.Services.AddSingleton<IFileStorageProvider, LocalDiskFileStorageProvider>();
builder.Services.AddScoped<JobOrchestrationService>();
builder.Services.AddScoped<JobQueryService>();

// Renderers
builder.Services.AddSingleton<HtmlDocumentRenderer>();
builder.Services.AddSingleton<DocxDocumentRenderer>();
builder.Services.AddSingleton<IReadOnlyDictionary<OutputFormat, IDocumentRenderer>>(sp =>
{
    var renderers = new List<IDocumentRenderer>
    {
        sp.GetRequiredService<HtmlDocumentRenderer>(),
        sp.GetRequiredService<DocxDocumentRenderer>()
    };
    return renderers.ToDictionary(r => r.Format);
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthorization();
app.MapControllers();

// Migrate DB on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.Run();
