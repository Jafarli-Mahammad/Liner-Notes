using LinerNotes.Application;
using LinerNotes.DataAccess;
using LinerNotes.Infrastructure;
using LinerNotes.Presentation;
using LinerNotes.Presentation.Filters;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Standard Kestrel configuration
builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
});

// Configure Onion Architecture layers
builder.Services.AddApplication();
builder.Services.AddDataAccess(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddPresentation(builder.Configuration);

// Add Controllers with RFC 7807 global exception filter
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ApiExceptionFilterAttribute>();
});

// CORS for web client access
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Liner Notes API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseCors("AllowAll");

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

// Guest-first discovery SPA fallback
app.MapFallbackToFile("index.html");

app.Run();

// Make the implicit Program class public for WebApplicationFactory in integration tests
public partial class Program { }
