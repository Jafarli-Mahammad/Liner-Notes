using LinerNotes.Application.Common.Interfaces;
using LinerNotes.DataAccess.DataContexts;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinerNotes.DataAccess.Persistence;

/// <summary>Owns a key provider isolated from Identity's application discriminator.</summary>
public sealed class LocalUnsubscribeProtection : IUnsubscribeTokenProtection, ILocalEmailFileProtection, IDisposable
{
    private readonly Lazy<(ServiceProvider Services, IDataProtector Protector, IDataProtector FileProtector)> _provider;

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
            var keys = provider.GetRequiredService<IDataProtectionProvider>();
            return (provider, keys.CreateProtector("Unsubscribe/v1"), keys.CreateProtector("CapturedEmailFile/v1"));
        });
    }

    public string Protect(string value) => _provider.Value.Protector.Protect(value);
    public string Unprotect(string value) => _provider.Value.Protector.Unprotect(value);
    byte[] ILocalEmailFileProtection.Protect(byte[] plaintext) => _provider.Value.FileProtector.Protect(plaintext);
    byte[] ILocalEmailFileProtection.Unprotect(byte[] ciphertext) => _provider.Value.FileProtector.Unprotect(ciphertext);
    public void Dispose() { if (_provider.IsValueCreated) _provider.Value.Services.Dispose(); }
}
