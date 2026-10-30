using LinerNotes.Application.Common.Interfaces;
using LinerNotes.DataAccess.DataContexts;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinerNotes.DataAccess.Persistence;

/// <summary>Owns a key provider isolated from Identity's application discriminator.</summary>
public sealed class LocalUnsubscribeProtection : IUnsubscribeTokenProtection, ILocalEmailFileProtection, IDisposable
{
    private readonly Lazy<(ServiceProvider Services, IDataProtector Protector, IDataProtector FileProtector)> _provider;

    /// <summary>Defers creation of database-backed keys with separate protection purposes for unsubscribe tokens and captured email files.</summary>
    public LocalUnsubscribeProtection(string connectionString, IServiceScopeFactory? managedScopes = null)
    {
        _provider = new(() =>
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<DataContext>(o => o.UseNpgsql(connectionString));
            var protection = services.AddDataProtection().SetApplicationName("LinerNotes.LocalEmail.Unsubscribe.v1");
            if (managedScopes is null) protection.PersistKeysToDbContext<DataContext>();
            else services.Configure<KeyManagementOptions>(options =>
                options.XmlRepository = new ManagedDataProtectionXmlRepository(managedScopes));
            var provider = services.BuildServiceProvider();
            var keys = provider.GetRequiredService<IDataProtectionProvider>();
            return (provider, keys.CreateProtector("Unsubscribe/v1"), keys.CreateProtector("CapturedEmailFile/v1"));
        });
    }

    public string Protect(string value) => _provider.Value.Protector.Protect(value);
    public string Unprotect(string value) => _provider.Value.Protector.Unprotect(value);
    /// <summary>Encrypts and authenticates file bytes using the captured-email purpose and persistent keys.</summary>
    /// <remarks>Key-store and protection failures propagate.</remarks>
    byte[] ILocalEmailFileProtection.Protect(byte[] plaintext) => _provider.Value.FileProtector.Protect(plaintext);
    /// <summary>Authenticates and decrypts bytes protected for captured email files.</summary>
    /// <remarks>Key-store and cryptographic failures propagate.</remarks>
    byte[] ILocalEmailFileProtection.Unprotect(byte[] ciphertext) => _provider.Value.FileProtector.Unprotect(ciphertext);
    public void Dispose() { if (_provider.IsValueCreated) _provider.Value.Services.Dispose(); }
}
