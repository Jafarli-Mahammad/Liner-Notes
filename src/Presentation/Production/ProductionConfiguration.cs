using System.Net;
using Microsoft.Extensions.Configuration;

namespace LinerNotes.Presentation.Production;

public static class ProductionConfiguration
{
    public static void Validate(IConfiguration configuration)
    {
        if (Flag(configuration,"ForwardedHeaders_Enabled") || Flag(configuration,"FORWARDEDHEADERS_ENABLED"))
            throw new InvalidOperationException("production_unrestricted_forwarding_forbidden");
        if (Flag(configuration,"LocalLive:Enabled") || Flag(configuration,"LocalEmail:Enabled"))
            throw new InvalidOperationException("production_local_services_forbidden");
        var origin = configuration["GuestDiscovery:PublicOrigin"];
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            uri.UserInfo.Length != 0 || origin != uri.GetLeftPart(UriPartial.Authority))
            throw new InvalidOperationException("production_https_origin_required");
        var hosts = configuration["AllowedHosts"]?.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (hosts is not { Length: 1 } || !string.Equals(hosts[0], uri.Host, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("production_exact_host_required");
        var proxies = configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [];
        if (proxies.Length == 0 || proxies.Any(p => !IPAddress.TryParse(p, out var ip) ||
                ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any)))
            throw new InvalidOperationException("production_explicit_proxy_required");
        if (configuration["Generation:Provider"] != "listenbrainz" || configuration["Generation:Origin"] != "Live" ||
            !Flag(configuration,"Generation:LiveEnabled") || configuration["ListenBrainz:Mode"] != "LiveOnly" ||
            !Flag(configuration,"ListenBrainz:LiveUseApproved"))
            throw new InvalidOperationException("production_live_provider_configuration_required");
        if (!Flag(configuration,"DiscoveryRuntime:EnforceStorageAdmission"))
            throw new InvalidOperationException("production_storage_admission_required");
        foreach (var key in new[] { "GenerationStorage:InventoryPath", "GenerationStorage:LeasePath", "DataProtection:KeyPath" })
            if (!Path.IsPathFullyQualified(configuration[key] ?? ""))
                throw new InvalidOperationException("production_absolute_state_paths_required");
    }

    private static bool Flag(IConfiguration configuration,string key) => configuration[key] is not { } value ? false :
        bool.TryParse(value,out var enabled) ? enabled : throw new InvalidOperationException("production_setting_invalid");
}
