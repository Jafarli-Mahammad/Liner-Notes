using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Domain.Digest;
using Microsoft.Extensions.Configuration;

namespace LinerNotes.Worker;

public sealed record LocalWorkerOptions(string Action, Guid UserId, IsoWeek Week, EvidenceOrigin Origin)
{
    public static LocalWorkerOptions Parse(IConfiguration config)
    {
        string? action=config["action"], user=config["user"], week=config["week"], origin=config["origin"];
        if(action is not ("reconcile" or "generate" or "capture") || !Guid.TryParse(user,out var id) || id==Guid.Empty ||
            week is null || !Enum.TryParse<EvidenceOrigin>(origin,false,out var evidence) || evidence is not (EvidenceOrigin.Synthetic or EvidenceOrigin.Recorded))
            throw new ArgumentException("explicit_action_user_week_origin_required");
        var isoWeek=IsoWeek.Parse(week);
        if(week!=isoWeek.Value || isoWeek.WeekNumber>System.Globalization.ISOWeek.GetWeeksInYear(isoWeek.Year)) throw new ArgumentException("invalid_iso_week");
        return new(action,id,isoWeek,evidence);
    }
}
