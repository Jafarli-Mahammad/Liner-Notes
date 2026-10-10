using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Domain.Digest;

namespace LinerNotes.Infrastructure.RecommendationSources.LastFm;

public sealed class LocalRecordedInputApproval
{
    public string? ManifestPath { get; set; }
    public string? InputRoot { get; set; }
    public string? ApprovedManifestSha256 { get; set; }
}
public sealed record LocalRecordedResponseInput(string Method,string Identity,string Path,string Sha256,DateTimeOffset RetrievedAtUtc,int Limit,string Origin);
public sealed record LocalRecordedGenerationManifest(int Version,string Use,string Origin,Guid UserId,string Week,
    DateTimeOffset ExpiresAtUtc,string DeveloperReference,string SourceManifestPath,string SourceManifestSha256,
    IReadOnlyList<LocalRecordedResponseInput> Responses);
public sealed record ValidatedLocalRecording(IReadOnlyList<RecordedLastFmResponse> Responses,DateTimeOffset ExpiresAtUtc,string ManifestSha256);
public sealed class LocalRecordedInputs
{
    public IReadOnlyList<RecordedLastFmResponse>? Responses { get; private set; }
    public void Set(ValidatedLocalRecording recording) => Responses=recording.Responses;
}

/// <summary>Exact local-generation approval. Acquisition/evaluation approval alone cannot enable use.</summary>
public sealed class LocalRecordedInputLoader(LocalRecordedInputApproval approval,TimeProvider clock)
{
    public async Task<ValidatedLocalRecording> LoadAsync(Guid userId,IsoWeek week,CancellationToken ct)
    {
        try
        {
            if(!Path.IsPathFullyQualified(approval.InputRoot??"") || !Path.IsPathFullyQualified(approval.ManifestPath??"") || !Hash(approval.ApprovedManifestSha256))
                throw new GenerationStoppedException("recording_use_unapproved");
            string root=Path.TrimEndingDirectorySeparator(Path.GetFullPath(approval.InputRoot!)); CheckPath(root);
            var bytes=await ReadAsync(approval.ManifestPath!,1_000_000,ct);
            string digest=Convert.ToHexStringLower(SHA256.HashData(bytes));
            if(digest!=approval.ApprovedManifestSha256) throw new GenerationStoppedException("recording_manifest_changed");
            using var document=JsonDocument.Parse(bytes); RejectSecrets(document.RootElement);
            var manifest=JsonSerializer.Deserialize<LocalRecordedGenerationManifest>(bytes)??throw new GenerationStoppedException("recording_manifest_invalid");
            if(manifest.Version!=1 || manifest.Use!="local-digest-generation" || manifest.Origin!="Recorded" || manifest.UserId!=userId || manifest.Week!=week.Value ||
                manifest.ExpiresAtUtc<=clock.GetUtcNow() || string.IsNullOrWhiteSpace(manifest.DeveloperReference) || manifest.DeveloperReference.Length>200 ||
                manifest.Responses is not {Count: >0 and <=1000} || !Hash(manifest.SourceManifestSha256)) throw new GenerationStoppedException("recording_use_unapproved_or_expired");
            string Contained(string relative)
            {
                if(Path.IsPathFullyQualified(relative)) throw new GenerationStoppedException("recording_path_invalid");
                string path=Path.GetFullPath(Path.Combine(root,relative));
                if(!path.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.Ordinal)) throw new GenerationStoppedException("recording_path_invalid");
                CheckPath(path);return path;
            }
            var sourceBytes=await ReadAsync(Contained(manifest.SourceManifestPath),1_000_000,ct);
            if(Convert.ToHexStringLower(SHA256.HashData(sourceBytes))!=manifest.SourceManifestSha256) throw new GenerationStoppedException("recording_source_changed");
            using var source=JsonDocument.Parse(sourceBytes);var acquisition=source.RootElement;RejectSecrets(acquisition);
            if(acquisition.GetProperty("status").GetString()!="approved" || acquisition.GetProperty("provider").GetString()!="Last.fm" || acquisition.GetProperty("phase").GetInt32() is not (4 or 5) ||
                acquisition.GetProperty("retention").GetProperty("expires_at_utc").GetDateTimeOffset()<=clock.GetUtcNow() || manifest.ExpiresAtUtc>acquisition.GetProperty("retention").GetProperty("expires_at_utc").GetDateTimeOffset())
                throw new GenerationStoppedException("recording_source_unapproved_or_expired");
            string acquisitionHash=acquisition.GetProperty("approval").GetProperty("approved_manifest_sha256").GetString()??"";
            if(!Hash(acquisitionHash) || CanonicalHash(acquisition)!=acquisitionHash) throw new GenerationStoppedException("recording_source_approval_changed");
            var responses=new List<RecordedLastFmResponse>();long total=0;
            foreach(var response in manifest.Responses)
            {
                ct.ThrowIfCancellationRequested();
                if(response.Origin!="Recorded" || response.Method is not ("artist.getsimilar" or "artist.gettoptags" or "artist.gettoptracks" or "tag.gettoptracks") || !Hash(response.Sha256) || response.Limit<=0 || response.RetrievedAtUtc>clock.GetUtcNow())
                    throw new GenerationStoppedException("recording_response_invalid");
                var body=await ReadAsync(Contained(response.Path),100_000,ct);total=checked(total+body.Length);
                if(total>20_000_000) throw new GenerationStoppedException("recording_input_limit");
                using var json=JsonDocument.Parse(body);RejectSecrets(json.RootElement);
                responses.Add(new(response.Method,response.Identity,body,response.Sha256,response.RetrievedAtUtc,response.Limit));
            }
            return new(responses.AsReadOnly(),manifest.ExpiresAtUtc,digest);
        }
        catch(GenerationStoppedException){throw;}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException or KeyNotFoundException)
        {throw new GenerationStoppedException("recording_input_invalid");}
    }
    private static bool Hash(string? value)=>value is {Length:64} && value.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static void CheckPath(string path)
    {
        for(string? current=Path.GetFullPath(path);current is not null;current=Path.GetDirectoryName(current))
            if((File.Exists(current)||Directory.Exists(current))&&(File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0) throw new GenerationStoppedException("recording_symlink");
    }
    private static async Task<byte[]> ReadAsync(string path,int bound,CancellationToken ct)
    {
        CheckPath(path);await using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,8192,true);
        if(file.Length>bound)throw new GenerationStoppedException("recording_input_limit");
        var bytes=new byte[checked((int)file.Length)];await file.ReadExactlyAsync(bytes,ct);
        if(file.Length!=bytes.Length)throw new GenerationStoppedException("recording_input_changed");return bytes;
    }
    private static void RejectSecrets(JsonElement element)
    {
        if(element.ValueKind==JsonValueKind.Object) foreach(var property in element.EnumerateObject())
        {
            string key=property.Name.ToLowerInvariant().Replace("_","");
            if(key.Contains("apikey")||key.Contains("password")||key.Contains("accesstoken")||key.Contains("refreshtoken")||key.Contains("secret")|| key is "authorization" or "credential" or "credentials") throw new GenerationStoppedException("recording_credentials_forbidden");
            if(key is "heldout" or "evaluationonly" && property.Value.ValueKind==JsonValueKind.True) throw new GenerationStoppedException("recording_evaluation_only");
            RejectSecrets(property.Value);
        }
        if(element.ValueKind==JsonValueKind.Array)foreach(var item in element.EnumerateArray())RejectSecrets(item);
        if(element.ValueKind==JsonValueKind.String)
        {
            string value=element.GetString()??"";
            if(value.Contains("api_key=",StringComparison.OrdinalIgnoreCase)||value.Contains("Bearer ",StringComparison.OrdinalIgnoreCase)||value.Replace("-", "").Replace("_", "").Equals("heldout",StringComparison.OrdinalIgnoreCase)||value.Replace("-", "").Replace("_", "").Equals("evaluationonly",StringComparison.OrdinalIgnoreCase))
                throw new GenerationStoppedException("recording_credentials_or_evaluation_forbidden");
        }
    }
    private static string CanonicalHash(JsonElement root)
    {
        using var stream=new MemoryStream();using(var writer=new Utf8JsonWriter(stream,new(){Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping}))
        {
            void Write(JsonElement element,string? name=null)
            {
                if(name=="approved_manifest_sha256"){writer.WriteNullValue();return;}
                if(element.ValueKind==JsonValueKind.Object){writer.WriteStartObject();foreach(var p in element.EnumerateObject().OrderBy(p=>p.Name,StringComparer.Ordinal)){writer.WritePropertyName(p.Name);Write(p.Value,p.Name);}writer.WriteEndObject();}
                else if(element.ValueKind==JsonValueKind.Array){writer.WriteStartArray();foreach(var v in element.EnumerateArray())Write(v);writer.WriteEndArray();}
                else element.WriteTo(writer);
            }
            Write(root);
        }
        return Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));
    }
}
