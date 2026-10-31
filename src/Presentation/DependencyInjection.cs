using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Text;
using LinerNotes.Application.Services;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.DataAccess.IdentityEntities;
using LinerNotes.Presentation.Filters;
using LinerNotes.Presentation.Options;
using LinerNotes.Presentation.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using NSwag;

namespace LinerNotes.Presentation;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Options
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        if (string.Equals(jwtOptions.SecretKey, "liner-notes-development-signing-key-change-before-deployment", StringComparison.Ordinal) &&
            !string.Equals(configuration["Environment"], "Development", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The development signing key is forbidden outside Development.");

        // Enforce required secure key
        if (string.IsNullOrWhiteSpace(jwtOptions.SecretKey) || Encoding.UTF8.GetByteCount(jwtOptions.SecretKey) < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SecretKey configuration is missing or insufficiently secure (must be at least 32 bytes / 256 bits). " +
                "Provide a secure key via user-secrets (dotnet user-secrets set \"Jwt:SecretKey\" \"<key>\") or environment variable \"Jwt__SecretKey\".");
        }

        if (jwtOptions.ExpiryMinutes is < 1 or > 60 || jwtOptions.RefreshExpiryDays is < 1 or > 30 ||
            string.IsNullOrWhiteSpace(jwtOptions.Issuer) || string.IsNullOrWhiteSpace(jwtOptions.Audience))
            throw new InvalidOperationException("JWT issuer/audience and bounded token lifetimes are required.");

        services.AddSingleton(TimeProvider.System);
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            var guestStarts = configuration.GetValue<int?>("RateLimits:GuestStartsPerMinute") ?? 60;
            if (guestStarts is < 1 or > 6000) throw new InvalidOperationException("Invalid guest start request rate.");
            options.AddPolicy("guest-start", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
                { PermitLimit = guestStarts, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            options.AddPolicy("catalog", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
                { PermitLimit = 1200, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
            options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
        });

        // 2. Core Presentation Services
        services.AddHttpContextAccessor();
        services.Configure<LinerNotes.Presentation.Discovery.GuestDiscoveryOptions>(configuration.GetSection("GuestDiscovery"));
        services.AddScoped<LinerNotes.Presentation.Discovery.IListenerContext, LinerNotes.Presentation.Discovery.ListenerContext>();
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "liner.csrf";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        });
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ApiExceptionFilterAttribute>();
        services.AddScoped<DiscoveryWriteFilter>();

        // 3. ASP.NET Core Identity Configuration
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequiredLength = 12;
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.User.RequireUniqueEmail = true;
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<DataContext>()
        .AddDefaultTokenProviders();
        services.AddScoped<PasswordHasher<ApplicationUser>>();
        services.AddScoped<PreparedPasswordHasher>();
        services.AddScoped<IPasswordHasher<ApplicationUser>>(sp => sp.GetRequiredService<PreparedPasswordHasher>());
        services.AddSingleton<PasswordVerificationGate>();
        services.AddScoped<PasswordVerificationPreparation>();

        // 4. JWT Bearer Authentication & Authorization
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.SaveToken = false;
            options.RequireHttpsMetadata = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
                ValidateIssuer = !string.IsNullOrWhiteSpace(jwtOptions.Issuer),
                ValidIssuer = jwtOptions.Issuer,
                ValidateAudience = !string.IsNullOrWhiteSpace(jwtOptions.Audience),
                ValidAudience = jwtOptions.Audience,
                ValidateLifetime = true,
                ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
                ClockSkew = TimeSpan.FromMinutes(1)
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var id = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                    var db = context.HttpContext.RequestServices.GetRequiredService<DataContext>();
                    if (!Guid.TryParse(id, out var userId) ||
                        !await db.ApplicationUsers.AnyAsync(u => u.Id == userId && !u.IsDeleted &&
                            db.Users.Any(subscriber => subscriber.Id == u.Id), context.HttpContext.RequestAborted))
                        context.Fail("Account is not active.");
                }
            };
        });

        services.AddAuthorization();

        // 5. Unified Swagger / OpenAPI documentation
        services.AddEndpointsApiExplorer();
        services.AddOpenApiDocument(options =>
        {
            options.DocumentName = "v1";
            options.Title = "Liner Notes API";
            options.Version = "v1";
            options.Description = "Weekly music discovery with transparent scoring and feedback.";
            options.AddSecurity("Bearer", [], new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. Format: Bearer {token}",
                Name = "Authorization",
                In = OpenApiSecurityApiKeyLocation.Header,
                Type = OpenApiSecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });
        });

        // 6. Health Checks
        services.AddHealthChecks()
            .AddDbContextCheck<DataContext>("database");

        return services;
    }
}
