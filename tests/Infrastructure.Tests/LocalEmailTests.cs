using System.Text;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.DTOs.Digests;
using LinerNotes.Application.DTOs.Subscribers;
using LinerNotes.Domain.Enums;
using LinerNotes.Infrastructure.Email;
using LinerNotes.Infrastructure.Storage;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace LinerNotes.Infrastructure.Tests;

public sealed class LocalEmailTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "liner-email-" + Guid.NewGuid().ToString("N"));
    private readonly LocalEmailOptions options;
    private readonly GenerationStorageOptions storageOptions;
    private readonly LocalGenerationStorage storage;
    private readonly DigestEmailRenderer renderer;
    private readonly LocalEmailMessageSerializer serializer;
    private readonly LocalEmailSink sink;
    private readonly LocalEmailArchive archive;
    private static readonly GenerationStorageState Database = new(1000, "initial");
    public LocalEmailTests()
    {
        Directory.CreateDirectory(root);
        storageOptions = new() { InventoryPath=Path.Combine(root,"inventory.json"), LeasePath=Path.Combine(root,"lease"), DatabaseOverheadBytesPerPick=100,
            ArtifactRoots=LocalGenerationStorage.Categories.ToDictionary(c=>c,c=>new[] { Path.Combine(root,c) }) };
        foreach (var path in storageOptions.ArtifactRoots.Values.SelectMany(v=>v)) Directory.CreateDirectory(path);
        options = new() { Enabled=true, ApplicationOrigin="http://127.0.0.1:5000", SinkRoot=Path.Combine(root,"copies") };
        var protection = new Protection(); var tokens = new UnsubscribeTokenService(protection);
        renderer = new(options,tokens); serializer = new(options,protection,tokens);
        storage = new(storageOptions,TimeProvider.System); sink = new(options,storageOptions,storage,renderer,serializer);
        archive = new(options,storageOptions,serializer);
    }
    public void Dispose() => Directory.Delete(root,true);
    private sealed class Protection : IUnsubscribeTokenProtection
    {
        private readonly IDataProtector protector = new EphemeralDataProtectionProvider().CreateProtector("email-test");
        public string Protect(string value)=>protector.Protect(value);
        public string Unprotect(string value)=>protector.Unprotect(value);
    }
    private static (WeeklyDigestDto Digest, SubscriberDto Subscriber) Inputs(int count)
    {
        var id = Guid.NewGuid(); var digest = Guid.NewGuid();
        var picks = Enumerable.Range(1,count).Select(rank=>new WeeklyRecommendationDto(Guid.NewGuid(),rank,
            new(Guid.NewGuid(),"Şarkı <script>&\"", "Artist 日本",null,null,null,null,null),
            new(1,1,0,.2,0,[]),"Stored tags <rock> & novelty explains this pick.",UserFeedback.None,null,null)).ToArray();
        return (new(digest,id,"2026-W41",new(2026,10,5,0,0,0,DateTimeKind.Utc),new(2026,10,12,0,0,0,DateTimeKind.Utc),DigestStatus.Pending,null,picks),
            new(id,"local@example.test","UTC",DigestDeliveryDay.Sunday,8,null,DateTime.UtcNow));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(3)] [InlineData(5)]
    public void Alternatives_UseActualCountPersistedExplanationsAndUtf8(int count)
    {
        var input=Inputs(count); var message=renderer.Render(input.Digest,input.Subscriber);
        var bytes=serializer.Serialize(message); var parsed=serializer.Parse(bytes);
        Assert.Equal(message.PlainText,parsed.PlainText); Assert.Equal(message.Html,parsed.Html);
        Assert.Contains($"{count} ",parsed.PlainText);
        Assert.All(input.Digest.Recommendations,p=>Assert.Contains(p.WhyThisPick,parsed.PlainText));
        if (count>0) { Assert.Contains("&lt;script&gt;&amp;&quot;",parsed.Html); Assert.Contains("Şarkı",parsed.PlainText); }
        else Assert.Contains("No eligible picks",parsed.Html);
        foreach (var forbidden in new[]{"<script","<img","<iframe","<link","@font-face","url(","<audio"}) Assert.DoesNotContain(forbidden,parsed.Html,StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Unsubscribe:",parsed.PlainText); Assert.Contains("Last.fm",parsed.Html);
        var mime=Encoding.UTF8.GetString(bytes);
        Assert.True(mime.IndexOf("text/plain",StringComparison.Ordinal)<mime.IndexOf("text/html",StringComparison.Ordinal));
        Assert.All(mime.Split("\r\n"),line=>Assert.True(line.Length<998));
        Assert.InRange(bytes.Length,1,400_000);
    }

    [Fact]
    public void Fingerprint_ExcludesFeedbackLifecycleAndAudit_ButDetectsChangedInputs()
    {
        var input=Inputs(1); var first=renderer.Render(input.Digest,input.Subscriber);
        var changed=input.Digest with { Status=DigestStatus.LocalCaptured,LastModifiedAt=DateTime.UtcNow,
            Recommendations=[input.Digest.Recommendations[0] with { Rating=9, Feedback=UserFeedback.AlreadyKnown,FeedbackGivenAt=DateTime.UtcNow, LastModifiedAt=DateTime.UtcNow }] };
        Assert.Equal(first.Fingerprint,renderer.Render(changed,input.Subscriber).Fingerprint);
        Assert.NotEqual(first.Fingerprint,renderer.Render(input.Digest,input.Subscriber with { Email="changed@example.test" }).Fingerprint);
        Assert.Throws<InvalidOperationException>(()=>renderer.Render(input.Digest,input.Subscriber with { Email="local@example.test\r\nBcc: evil" }));
        var link=input.Digest with { Recommendations=[input.Digest.Recommendations[0] with { Track=input.Digest.Recommendations[0].Track with { ExternalSpotifyUrl="javascript:alert(1)" } }] };
        Assert.Throws<GenerationStoppedException>(()=>renderer.Render(link,input.Subscriber));
        options.AlternativeByteLimit=10;
        Assert.Throws<GenerationStoppedException>(()=>renderer.Render(input.Digest,input.Subscriber));
    }

    [Fact]
    public async Task Capture_RestartReceiptConflictSanitizedExportAndOwnedDeletion()
    {
        var input=Inputs(3); await storage.ReconcileAsync(Database);
        await using (var capture=await sink.OpenAsync(input.Digest,input.Subscriber,Database,default))
        { Assert.False(capture.ReceiptExists); await capture.PublishAsync(input.Digest,input.Subscriber,Database,default); }
        var final=Directory.GetFiles(options.SinkRoot!,"*.eml").Single(); var bytes=await File.ReadAllBytesAsync(final);
        await using (var recovery=await sink.OpenAsync(input.Digest,input.Subscriber,Database with { Revision="changed" },default))
        { Assert.True(recovery.ReceiptExists); await recovery.PublishAsync(input.Digest,input.Subscriber,Database,default); }
        Assert.Equal(bytes,await File.ReadAllBytesAsync(final));
        var result=await archive.ReadAsync(input.Subscriber.Id,[input.Digest with {Status=DigestStatus.LocalCaptured}],default);
        Assert.Equal("complete",result.Status); var copy=Assert.Single(result.Copies);
        Assert.Contains("capability-removed",copy.PlainText);
        Assert.DoesNotContain(serializer.Parse(bytes).Receipt.Token,copy.PlainText);
        await Assert.ThrowsAsync<GenerationStoppedException>(()=>sink.OpenAsync(input.Digest,input.Subscriber with {Email="other@example.test"},Database,default));
        Assert.True((await archive.DeleteAsync(input.Subscriber.Id,default)).Complete);
        Assert.False(File.Exists(final));
    }

    [Fact]
    public async Task OwnershipCleanup_LeavesOtherAccountAndUnknownCorruptCopies()
    {
        var own=Inputs(1);var other=Inputs(1);
        foreach (var input in new[]{own,other})
        {
            await storage.ReconcileAsync(Database);
            await using var capture=await sink.OpenAsync(input.Digest,input.Subscriber,Database,default);
            await capture.PublishAsync(input.Digest,input.Subscriber,Database,default);
        }
        var corrupt=Path.Combine(options.SinkRoot!,"unknown.eml");await File.WriteAllTextAsync(corrupt,"bad");
        var cleanup=await archive.DeleteAsync(own.Subscriber.Id,default);
        Assert.False(cleanup.Complete);Assert.True(File.Exists(corrupt));
        Assert.False(File.Exists(Path.Combine(options.SinkRoot!,own.Digest.Id.ToString("N")+".eml")));
        Assert.True(File.Exists(Path.Combine(options.SinkRoot!,other.Digest.Id.ToString("N")+".eml")));
    }

    [Fact]
    public async Task MissingAndTamperedReceipts_StopWithoutOverwrite()
    {
        var input=Inputs(1);await storage.ReconcileAsync(Database);
        await Assert.ThrowsAsync<GenerationStoppedException>(()=>sink.OpenAsync(input.Digest with {Status=DigestStatus.LocalCaptured},input.Subscriber,Database,default));
        var final=Path.Combine(options.SinkRoot!,input.Digest.Id.ToString("N")+".eml");await File.WriteAllTextAsync(final,"truncated");
        await Assert.ThrowsAsync<GenerationStoppedException>(()=>sink.OpenAsync(input.Digest,input.Subscriber,Database,default));
        Assert.Equal("truncated",await File.ReadAllTextAsync(final));
        var export=await archive.ReadAsync(input.Subscriber.Id,[input.Digest with {Status=DigestStatus.LocalCaptured}],default);
        Assert.Equal("incomplete",export.Status);Assert.Contains("captured_copy_missing",export.Issues);
    }

    [Fact]
    public async Task Reservation_TracksPartialsAndRejectsUnownedOrSameSizeChanges()
    {
        await storage.ReconcileAsync(Database);
        string[] paths=Enumerable.Range(0,4).Select(i=>Path.Combine(options.SinkRoot!,i+".partial")).ToArray();
        await using var lease=await storage.AcquireEmailAsync(Database,100,paths,false,default);
        await File.WriteAllTextAsync(paths[1],"partial"); await lease.RecordOwnWriteAsync(paths[1],default);await lease.ValidateAsync(Database,default);
        await File.WriteAllTextAsync(paths[1],"changed");
        Assert.Equal("email_owned_artifact_changed",(await Assert.ThrowsAsync<GenerationStoppedException>(()=>lease.ValidateAsync(Database,default))).Message);
        await File.WriteAllTextAsync(paths[1],"partial");
        await File.WriteAllTextAsync(Path.Combine(root,"reports","unexpected"),"new");
        Assert.Equal("artifact_inventory_changed",(await Assert.ThrowsAsync<GenerationStoppedException>(()=>lease.ValidateAsync(Database,default))).Message);
    }

    [Fact]
    public async Task SymlinkAndExactReservationThreshold_Stop()
    {
        var input=Inputs(1);options.SinkRoot=Path.Combine(root,"link");Directory.CreateSymbolicLink(options.SinkRoot,Path.Combine(root,"copies"));
        storageOptions.ArtifactRoots["copies"]=[options.SinkRoot];
        await Assert.ThrowsAsync<GenerationStoppedException>(()=>sink.OpenAsync(input.Digest,input.Subscriber,Database,default));
        Directory.Delete(options.SinkRoot);options.SinkRoot=Path.Combine(root,"copies");storageOptions.ArtifactRoots["copies"]=[options.SinkRoot];
        var db=new GenerationStorageState(LocalGenerationStorage.StopBytes-300,"threshold");await storage.ReconcileAsync(db);
        string[] paths=Enumerable.Range(0,4).Select(i=>Path.Combine(options.SinkRoot!,i+".partial")).ToArray();
        await Assert.ThrowsAsync<GenerationStoppedException>(()=>storage.AcquireEmailAsync(db,100,paths,false,default));
    }

    [Fact]
    public async Task FailedPartial_RemainsCounted_AndTransientWriteRetryPublishesOnce()
    {
        var input=Inputs(1);await storage.ReconcileAsync(Database);
        var retrySink=new LocalEmailSink(options,storageOptions,new FailingStorage(storage),renderer,serializer);
        await using(var capture=await retrySink.OpenAsync(input.Digest,input.Subscriber,Database,default))
            await capture.PublishAsync(input.Digest,input.Subscriber,Database,default);
        var final=Assert.Single(Directory.GetFiles(options.SinkRoot!,"*.eml"));
        var partial=Assert.Single(Directory.GetFiles(options.SinkRoot!,"*.partial"));
        Assert.Equal(new FileInfo(final).Length,new FileInfo(partial).Length);
        var measured=await storage.ReconcileAsync(Database);
        Assert.Equal(new FileInfo(final).Length*2,measured.Categories["copies"].Bytes);
        Assert.True((await archive.DeleteAsync(input.Subscriber.Id,default)).Complete);
        Assert.Empty(Directory.GetFiles(options.SinkRoot!));
    }

    [Fact]
    public void Serializer_RejectsTamperedBodyAndTotalByteOverflow()
    {
        var input=Inputs(1);var message=renderer.Render(input.Digest,input.Subscriber);
        var bytes=serializer.Serialize(message);bytes[^10] = (byte)'?';
        Assert.Throws<GenerationStoppedException>(()=>serializer.Parse(bytes));
        options.MessageByteLimit=100;
        Assert.Throws<GenerationStoppedException>(()=>serializer.Serialize(message));
    }

    private sealed class FailingStorage(ILocalEmailStorage inner) : ILocalEmailStorage
    {
        public async Task<ILocalEmailWriteLease> AcquireEmailAsync(GenerationStorageState database,long bytes,IReadOnlyList<string> paths,bool recovery,CancellationToken ct)
            =>new FailingLease(await inner.AcquireEmailAsync(database,bytes,paths,recovery,ct));
        private sealed class FailingLease(ILocalEmailWriteLease inner) : ILocalEmailWriteLease
        {
            private int calls;
            public Task RecordOwnWriteAsync(string path,CancellationToken ct)=>inner.RecordOwnWriteAsync(path,ct);
            public Task ValidateAsync(GenerationStorageState database,CancellationToken ct)
            { if(++calls==2) throw new IOException("injected after partial flush");return inner.ValidateAsync(database,ct); }
            public ValueTask DisposeAsync()=>inner.DisposeAsync();
        }
    }

    [Fact]
    public async Task InterruptedWrite_RetainsBoundedTruncatedPartial_AndPublishesOneRetry()
    {
        var input=Inputs(1);await storage.ReconcileAsync(Database);
        var interrupted=new LocalEmailSink(options,storageOptions,storage,renderer,serializer,new InterruptedWriter());
        await using(var capture=await interrupted.OpenAsync(input.Digest,input.Subscriber,Database,default))
            await capture.PublishAsync(input.Digest,input.Subscriber,Database,default);
        var final=Assert.Single(Directory.GetFiles(options.SinkRoot!,"*.eml"));
        var partial=Assert.Single(Directory.GetFiles(options.SinkRoot!,"*.partial"));Assert.Equal(100,new FileInfo(partial).Length);
        var inventory=await storage.ReconcileAsync(Database);Assert.Equal(new FileInfo(final).Length+100,inventory.Categories["copies"].Bytes);
        Assert.False((await archive.DeleteAsync(input.Subscriber.Id,default)).Complete);Assert.True(File.Exists(partial));Assert.False(File.Exists(final));
    }
    private sealed class InterruptedWriter : ILocalEmailBodyWriter
    {
        private bool interrupted;
        public async Task WriteAsync(Stream stream,ReadOnlyMemory<byte> bytes,CancellationToken ct)
        {
            if(!interrupted){interrupted=true;await stream.WriteAsync(bytes[..100],ct);throw new IOException("injected interrupted write");}
            await stream.WriteAsync(bytes,ct);
        }
    }
}
