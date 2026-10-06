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
using Microsoft.OpenApi.Models;

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
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ApiExceptionFilterAttribute>();

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
                        !await db.ApplicationUsers.AnyAsync(u => u.Id == userId && !u.IsDeleted, context.HttpContext.RequestAborted) ||
                        !await db.Users.AnyAsync(u => u.Id == userId, context.HttpContext.RequestAborted))
                        context.Fail("Account is not active.");
                }
            };
        });

        services.AddAuthorization();

        // 5. Unified Swagger / OpenAPI documentation
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Liner Notes API",
                Version = "v1",
                Description = "Open-source weekly music recommendation digest platform with transparent scoring and data sovereignty."
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. Format: Bearer {token}",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });

        // 6. Health Checks
        services.AddHealthChecks()
            .AddDbContextCheck<DataContext>("database");

        return services;
    }
}
