#:project ../tests/Infrastructure.Tests/Infrastructure.Tests.csproj
#:property PublishAot=false

// Explicit local snapshots only. Python performs approved-manifest, path, hash and byte preflight.
using System.Text.Json;
using System.Text.Json.Serialization;
using LinerNotes.Application.Tests.Common;
using LinerNotes.Infrastructure.Tests.RecommendationSources;

var json = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
json.Converters.Add(new JsonStringEnumConverter());
try
{
    object result;
    if (args is ["metadata"])
    {
        var profiles = Phase2ProfileMembership.Locked();
        result = new { profileLock = new { version = "profile-lock-v1-nfc-invariant", hash = profiles.Hash,
            review_reference = profiles.ReviewReference, profiles = profiles.Profiles.Select(p => new
            { id = p.Id, set = p.Set.ToString(), genre = p.Genre, seeds = p.Seeds }) }, protocol = PilotProtocol.Initial() };
    }
    else if (args is ["replay" or "shape", var path])
    {
        if (new FileInfo(path).Length > 8_000_000) throw new ArgumentException("Input too large.");
        await using var input = File.OpenRead(path);
        var request = await JsonSerializer.DeserializeAsync<PilotReplayInput>(input, json) ?? throw new ArgumentException("Missing input.");
        result = await PilotReplay.BuildAsync(request, compare: args[0] == "replay");
    }
    else throw new ArgumentException("Unknown command.");
    // SDK diagnostics may precede this line. The operator consumes only this bounded JSON marker.
    Console.WriteLine("PHASE4_JSON=" + JsonSerializer.Serialize(result, json));
}
catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException or JsonException)
{
    Console.Error.WriteLine("Pilot replay stopped: invalid explicit snapshots/protocol. No network or fallback.");
    return 2;
}
return 0;
