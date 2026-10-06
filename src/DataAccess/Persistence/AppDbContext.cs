using LinerNotes.DataAccess.DataContexts;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace LinerNotes.DataAccess.Persistence;

/// <summary>
/// Entity Framework Core database context for Liner Notes persistence.
/// Extends DataContext to provide compatibility across existing handlers, tests, and migrations.
/// </summary>
public class AppDbContext : DataContext
{
    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        IHttpContextAccessor? httpContextAccessor = null)
        : base(options, httpContextAccessor)
    {
    }

    public AppDbContext(
        DbContextOptions<DataContext> options,
        IHttpContextAccessor? httpContextAccessor = null)
        : base(options, httpContextAccessor)
    {
    }
}
