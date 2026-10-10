using LinerNotes.Application.Common.Interfaces;
using LinerNotes.DataAccess.DataContexts;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinerNotes.DataAccess.Persistence;

/// <summary>Owns a key provider isolated from Identity's application discriminator.</summary>
public sealed class LocalUnsubscribeProtection : IUnsubscribeTokenProtection, IDisposable
{
    private readonly Lazy<(ServiceProvider Services, IDataProtector Protector)> _provider;

    public LocalUnsubscribeProtection(string connectionString)
    {
        _provider = new(() =>
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<DataContext>(o => o.UseNpgsql(connectionString));
            services.AddDataProtection().SetApplicationName("LinerNotes.LocalEmail.Unsubscribe.v1")
                .PersistKeysToDbContext<DataContext>();
            var provider = services.BuildServiceProvider();
            return (provider, provider.GetRequiredService<IDataProtectionProvider>().CreateProtector("Unsubscribe/v1"));
        });
    }

    public string Protect(string value) => _provider.Value.Protector.Protect(value);
    public string Unprotect(string value) => _provider.Value.Protector.Unprotect(value);
    public void Dispose() { if (_provider.IsValueCreated) _provider.Value.Services.Dispose(); }
}
