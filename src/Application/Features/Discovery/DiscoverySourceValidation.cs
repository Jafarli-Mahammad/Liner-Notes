using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Models.Recommendation;

namespace LinerNotes.Application.Features.Discovery;

/// <summary>Incomplete acquisition must not become a successful reduced recommendation pool.</summary>
public static class DiscoverySourceValidation
{
    public static void ThrowIfFailed(IEnumerable<CoverageGap> gaps)
    {
        if (gaps.Any(g => g.Reason is "permission_denied" or "http_error" or "invalid_json" or "invalid_discovery_shape_or_identifier"))
            throw new DiscoverySourceException("source_configuration_or_response_invalid", false);
        if (gaps.Any(g => g.Reason is "rate_limit" or "transport_error" or "request_timeout" or "upstream_unavailable"))
            throw new DiscoverySourceException("source_temporarily_unavailable", true);
    }
}
