using System.Reflection;
using CodeCompassProject.CodeCompass.Api.Middleware;
using CodeCompassProject.CodeCompass.Repository;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "CodeCompass API",
        Version = "v1",
        Description = "AI Engineering Copilot - Grounded answers from your documentation and code."
    });

    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

// Register infrastructure services (DI, config binding, handlers)
// Use fake services in Development mode, real Azure services in Production
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDevelopmentServices(builder.Configuration);
}
else
{
    builder.Services.AddInfrastructureServices(builder.Configuration);
}

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

// Configure the HTTP request pipeline

// Exception handling middleware (first in pipeline to catch all errors)
app.UseMiddleware<GlobalExceptionMiddleware>();

// Request logging
app.UseMiddleware<RequestLoggingMiddleware>();

// Swagger (available in all environments for this API)
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "CodeCompass API v1");
});

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Make Program class accessible for WebApplicationFactory in integration tests
public partial class Program { }
