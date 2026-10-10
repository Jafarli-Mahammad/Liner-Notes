using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Domain.Digest;
using LinerNotes.Infrastructure.RecommendationSources.LastFm;
using Xunit;

namespace LinerNotes.Infrastructure.Tests;
public sealed class LocalRecordedInputTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"liner-local-recorded-"+Guid.NewGuid().ToString("N"));
    private readonly Guid user=Guid.NewGuid();
    private readonly IsoWeek week=new(2026,41);
    private readonly LocalRecordedInputApproval approval=new();
    private LocalRecordedGenerationManifest manifest=null!;
    public LocalRecordedInputTests(){Directory.CreateDirectory(root);}
    public void Dispose()=>Directory.Delete(root,true);
    private static string Sha(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private async Task Setup()
    {
        byte[] source=Encoding.UTF8.GetBytes("{\"approval\":{\"approved_manifest_sha256\":null,\"developer_reference\":\"synthetic-test-approval\"},\"phase\":4,\"provider\":\"Last.fm\",\"retention\":{\"expires_at_utc\":\"2099-01-01T00:00:00Z\"},\"status\":\"approved\"}");
        string acquisitionHash=Sha(source);source=Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(source).Replace("sha256\":null","sha256\":\""+acquisitionHash+"\""));
        await File.WriteAllBytesAsync(Path.Combine(root,"source.json"),source);
        byte[] response=Encoding.UTF8.GetBytes("{\"toptags\":{\"tag\":[{\"name\":\"rock\",\"count\":\"100\"}]}}");await File.WriteAllBytesAsync(Path.Combine(root,"response.json"),response);
        manifest=new(1,"local-digest-generation","Recorded",user,week.Value,DateTimeOffset.UtcNow.AddHours(1),"synthetic-test-local-generation-approval",
            "source.json",Sha(source),[new("artist.gettoptags","Example Artist","response.json",Sha(response),DateTimeOffset.UnixEpoch,15,"Recorded")]);
        approval.InputRoot=root;approval.ManifestPath=Path.Combine(root,"local-use.json");await Approve();
    }
    private async Task Approve()
    {var bytes=JsonSerializer.SerializeToUtf8Bytes(manifest);await File.WriteAllBytesAsync(approval.ManifestPath!,bytes);approval.ApprovedManifestSha256=Sha(bytes);}
    [Fact]
    public async Task ExactApprovedImmutableBytes_ReplayWithoutNetworkOrDiscovery()
    {
        await Setup();var result=await new LocalRecordedInputLoader(approval,TimeProvider.System).LoadAsync(user,week,default);
        await File.WriteAllTextAsync(Path.Combine(root,"response.json"),"changed after loading");
        var replay=await new RecordedLastFmApiClient(result.Responses).GetArtistTopTagsAsync("Example Artist",15);
        Assert.Single(replay.Items);Assert.Equal("rock",replay.Items[0].Name);Assert.Equal(LinerNotes.Application.Common.Models.Recommendation.EvidenceOrigin.Recorded,replay.Reference.Origin);
    }
    [Theory]
    [InlineData("approval")] [InlineData("expired")] [InlineData("hash")] [InlineData("user")] [InlineData("heldout")]
    [InlineData("escape")] [InlineData("symlink")] [InlineData("secret")] [InlineData("origin")]
    public async Task UnapprovedExpiredChangedMixedOrUnsafeInputs_Stop(string kind)
    {
        await Setup();
        switch(kind)
        {
            case "approval":approval.ApprovedManifestSha256=null;break;
            case "expired":manifest=manifest with{ExpiresAtUtc=DateTimeOffset.UnixEpoch};await Approve();break;
            case "hash":await File.WriteAllTextAsync(Path.Combine(root,"response.json"),"{}");break;
            case "user":manifest=manifest with{UserId=Guid.NewGuid()};await Approve();break;
            case "heldout":manifest=manifest with{Use="held-out"};await Approve();break;
            case "escape":manifest=manifest with{Responses=[manifest.Responses[0] with{Path="../outside.json"}]};await Approve();break;
            case "symlink":File.Delete(Path.Combine(root,"response.json"));File.CreateSymbolicLink(Path.Combine(root,"response.json"),Path.Combine(root,"source.json"));break;
            case "secret":manifest=manifest with{DeveloperReference="api_key=forbidden-fixture"};await Approve();break;
            case "origin":manifest=manifest with{Responses=[manifest.Responses[0] with{Origin="Live"}]};await Approve();break;
        }
        await Assert.ThrowsAsync<GenerationStoppedException>(()=>new LocalRecordedInputLoader(approval,TimeProvider.System).LoadAsync(user,week,default));
    }
}
