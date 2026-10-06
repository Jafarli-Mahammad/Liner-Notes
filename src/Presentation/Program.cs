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
    options.Limits.MaxRequestBodySize = 64 * 1024;
});

// Configure Onion Architecture layers
builder.Services.AddApplication();
builder.Services.AddDataAccess(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Configuration["Environment"] = builder.Environment.EnvironmentName;
builder.Services.AddPresentation(builder.Configuration);

// Add Controllers with RFC 7807 global exception filter and JSON string enum serialization
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ApiExceptionFilterAttribute>();
})
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

// Same-origin by default; cross-origin clients must be explicitly configured.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (allowedOrigins.Any(origin => !Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
    (uri.Scheme != "https" && !(builder.Environment.IsDevelopment() && uri.Scheme == "http")) ||
    origin != uri.GetLeftPart(UriPartial.Authority)))
    throw new InvalidOperationException("CORS origins must be explicit HTTPS origins (HTTP allowed in Development).");
builder.Services.AddCors(options => options.AddPolicy("ConfiguredOrigins", policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().WithMethods("GET", "POST", "DELETE")));
builder.Services.AddHsts(options => { options.MaxAge = TimeSpan.FromDays(180); });
builder.Services.AddHttpsRedirection(options => options.HttpsPort = 443);

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

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    if (!(app.Environment.IsDevelopment() && context.Request.Path.StartsWithSegments("/swagger")))
        context.Response.Headers["Content-Security-Policy"] =
            "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; " +
            "connect-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'; form-action 'self'";
    if (context.Request.Path.StartsWithSegments("/api"))
        context.Response.Headers.CacheControl = "no-store";
    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();
app.UseCors("ConfiguredOrigins");
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

// Guest-first discovery SPA fallback
app.MapFallbackToFile("index.html");

app.Run();

// Make the implicit Program class public for WebApplicationFactory in integration tests
public partial class Program { }
