using System.Text;
using System.Xml.Linq;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Features.Discovery;
using LinerNotes.DataAccess.DataContexts;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinerNotes.DataAccess.Persistence;

/// <summary>Uses the standard Data Protection XML contract; key writes participate in managed accounting.</summary>
internal sealed class ManagedDataProtectionXmlRepository(IServiceScopeFactory scopes) : IXmlRepository
{
    public IReadOnlyCollection<XElement> GetAllElements()
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        return db.DataProtectionKeys.AsNoTracking().Select(key => key.Xml).AsEnumerable()
            .Where(xml => !string.IsNullOrEmpty(xml)).Select(xml => XElement.Parse(xml!)).ToList().AsReadOnly();
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        if (ManagedWriteBoundary.IsActive)
            throw new GenerationStoppedException("data_protection_rotation_requires_preparation");
        using var scope = scopes.CreateScope();
        var provider = scope.ServiceProvider;
        var db = provider.GetRequiredService<DataContext>();
        string xml = element.ToString(SaveOptions.DisableFormatting);
        var key = new DataProtectionKey { FriendlyName = friendlyName, Xml = xml };
        if (provider.GetRequiredService<DiscoveryRuntimeOptions>().ManagedWritesEnabled)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            provider.GetRequiredService<IDiscoveryWriteCoordinator>().ExecuteAsync(
                checked(Encoding.UTF8.GetByteCount(xml) * 4L + 65_536), async ct =>
                {
                    db.DataProtectionKeys.Add(key);
                    return await db.SaveChangesAsync(ct);
                }, timeout.Token).GetAwaiter().GetResult();
        }
        else
        {
            db.DataProtectionKeys.Add(key);
            db.SaveChanges();
        }
    }
}
