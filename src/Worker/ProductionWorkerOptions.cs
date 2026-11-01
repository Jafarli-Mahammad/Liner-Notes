using LinerNotes.Application.Common.Models.Recommendation;
using Microsoft.Extensions.Configuration;

namespace LinerNotes.Worker;

public sealed record ProductionWorkerOptions(LocalWorkerOptions Request, TimeSpan Timeout)
{
    public static ProductionWorkerOptions Parse(IConfiguration configuration)
    {
        var request = LocalWorkerOptions.Parse(configuration);
        if (request.Action != "generate" || request.Origin != EvidenceOrigin.Live ||
            configuration["Generation:Origin"] != "Live" || configuration["ListenBrainz:Mode"] != "LiveOnly" ||
            !configuration.GetValue<bool>("ListenBrainz:LiveUseApproved") || configuration.GetValue<bool>("LocalEmail:Enabled") ||
            !configuration.GetValue<bool>("DiscoveryRuntime:EnforceStorageAdmission"))
            throw new ArgumentException("production_explicit_live_generation_required");
        var seconds = configuration.GetValue<int?>("ProductionWorker:TimeoutSeconds") ?? 180;
        if (seconds is < 30 or > 600) throw new ArgumentException("production_worker_timeout_invalid");
        return new(request,TimeSpan.FromSeconds(seconds));
    }
}
