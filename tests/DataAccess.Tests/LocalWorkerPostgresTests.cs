using LinerNotes.Application;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Application.Features.Digests.Commands.CaptureLocalDigest;
using LinerNotes.Application.Features.Export.Queries.GetUserDataExport;
using LinerNotes.Application.Features.Subscribers.Commands.DeleteUserAccount;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.DataAccess.IdentityEntities;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Taste;
using LinerNotes.Infrastructure;
using LinerNotes.Infrastructure.Email;
using LinerNotes.Infrastructure.Storage;
using LinerNotes.Worker;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace LinerNotes.DataAccess.Tests;

public sealed class LocalWorkerPostgresTests(ITestOutputHelper output) : IAsyncLifetime
{
    private readonly string name="liner_worker_"+Guid.NewGuid().ToString("N");
    private readonly string root=Path.Combine(Path.GetTempPath(),"liner-worker-"+Guid.NewGuid().ToString("N"));
    private readonly Guid userId=Guid.NewGuid();
    private readonly IsoWeek week=new(2026,41);
    private readonly Faults faults=new();
    private ServiceProvider services=null!;
    private IConfiguration config=null!;
    private GenerationStorageOptions storage=null!;
    private LocalEmailOptions email=null!;
    private string admin="";
    public async Task InitializeAsync()
    {
        admin=Environment.GetEnvironmentVariable("LINER_PHASE1_POSTGRES")??"";if(admin.Length==0)return;
        var connection=new NpgsqlConnectionStringBuilder(admin);Assert.Contains(connection.Host,new[]{"localhost","127.0.0.1","::1"});
        await using(var db=new NpgsqlConnection(admin)){await db.OpenAsync();await using var create=new NpgsqlCommand($"CREATE DATABASE \"{name}\"",db);await create.ExecuteNonQueryAsync();}
        connection.Database=name;Directory.CreateDirectory(root);
        storage=new(){InventoryPath=Path.Combine(root,"inventory.json"),LeasePath=Path.Combine(root,"lease"),DatabaseOverheadBytesPerPick=1_000_000,
            ArtifactRoots=LocalGenerationStorage.Categories.ToDictionary(c=>c,c=>new[]{Path.Combine(root,c)})};
        foreach(var path in storage.ArtifactRoots.Values.SelectMany(x=>x))Directory.CreateDirectory(path);
        email=new(){Enabled=true,ApplicationOrigin="http://127.0.0.1:5000",SinkRoot=Path.Combine(root,"copies")};
        if(!OperatingSystem.IsWindows())File.SetUnixFileMode(email.SinkRoot,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
        config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["ConnectionStrings:DefaultConnection"]=connection.ConnectionString,["Generation:Origin"]="Synthetic",["LastFm:ApiKey"]="fixture-only-do-not-dispatch"}).Build();
        services=Build();
        await using var scope=services.CreateAsyncScope();var context=scope.ServiceProvider.GetRequiredService<DataContext>();await context.Database.EnsureCreatedAsync();
        context.ApplicationUsers.Add(new ApplicationUser{Id=userId,UserName="local",Email="local@example.test"});context.Users.Add(new User("local@example.test",id:userId));
        context.TasteSignals.Add(TasteSignal.CreateSeedTag(userId,"rock",1,"synthetic QA"));await context.SaveChangesAsync();
    }
    private ServiceProvider Build()
    {
        var registrations=new ServiceCollection().AddLogging().AddApplication().AddDataAccess(config).AddInfrastructure(config);
        registrations.AddSingleton(storage);registrations.AddSingleton(email);
        registrations.AddScoped<ILocalDigestCaptureStore>(sp=>new FaultStore(new LinerNotes.DataAccess.Persistence.Repositories.LocalDigestCaptureStore(config["ConnectionStrings:DefaultConnection"]!),faults));
        registrations.AddSingleton<LocalDigestWorkerRunner>();return registrations.BuildServiceProvider();
    }
    public async Task DisposeAsync()
    {
        if(services is not null)await services.DisposeAsync();NpgsqlConnection.ClearAllPools();
        if(admin.Length>0){await using var db=new NpgsqlConnection(admin);await db.OpenAsync();await using var drop=new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)",db);await drop.ExecuteNonQueryAsync();}
        if(Directory.Exists(root))Directory.Delete(root,true);
    }
    private Task<LocalWorkerResult> Run(string action,IsoWeek? target=null,CancellationToken ct=default)=>services.GetRequiredService<LocalDigestWorkerRunner>().RunAsync(new(action,userId,target??week,EvidenceOrigin.Synthetic),ct);
    private async Task Prepare()
    {
        Assert.Equal("reconciled",(await Run("reconcile")).Status);
        Assert.Equal("generated",(await Run("generate")).Status);
        Assert.Equal("reconciled",(await Run("reconcile")).Status);
    }
    [LocalPostgresMigrationFact]
    public async Task ActualComposition_RepeatedConcurrentAndRestartCapture_ExportAndDeletion()
    {
        await Prepare();var results=await Task.WhenAll(Run("capture"),Run("capture"));
        Assert.Contains(results,r=>r.Status=="local_captured");Assert.Contains(results,r=>r.Status=="already_captured");
        var file=Assert.Single(Directory.GetFiles(email.SinkRoot!,"*.eml"));var bytes=await File.ReadAllBytesAsync(file);
        await services.DisposeAsync();services=Build();Assert.Equal("already_captured",(await Run("capture")).Status);
        Assert.Equal(bytes,await File.ReadAllBytesAsync(file));
        await using var scope=services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<DataContext>();
        var digest=await db.WeeklyDigests.SingleAsync();Assert.Equal(DigestStatus.LocalCaptured,digest.Status);Assert.Null(digest.SentAt);
        var export=await scope.ServiceProvider.GetRequiredService<ISender>().Send(new GetUserDataExportQuery(userId));
        Assert.Equal("2.1",export.ExportVersion);Assert.Equal("complete",export.LocalEmail!.Status);
        var copy=Assert.Single(export.LocalEmail.Copies);Assert.Contains("capability-removed",copy.PlainText);
        output.WriteLine($"Actual synthetic Worker MIME={bytes.Length} bytes; picks={export.Digests.Single().Recommendations.Count}; copy plaintext={copy.PlainText.Length} chars; HTML={copy.Html.Length} chars");
        await using(var ownership=await scope.ServiceProvider.GetRequiredService<IAccountEmailLease>().AcquireAsync(userId,default))
        {
            Assert.True(await scope.ServiceProvider.GetRequiredService<ISender>().Send(new DeleteUserAccountCommand(userId)));
            Assert.True((await scope.ServiceProvider.GetRequiredService<ILocalEmailArchive>().DeleteAsync(userId,default)).Complete);
        }
        Assert.Empty(Directory.GetFiles(email.SinkRoot!));Assert.Equal("user_not_found",(await Run("capture")).Reason);
    }
    [LocalPostgresMigrationFact]
    public async Task PublishedBeforeSqlFailure_IsRecoveredWithoutRecreationOrInventoryRefresh()
    {
        await Prepare();faults.FailCommits=3;
        Assert.Equal("capture_database_unavailable",(await Run("capture")).Reason);
        string final=Assert.Single(Directory.GetFiles(email.SinkRoot!,"*.eml"));var bytes=await File.ReadAllBytesAsync(final);
        await using(var scope=services.CreateAsyncScope())Assert.Equal(DigestStatus.Pending,(await scope.ServiceProvider.GetRequiredService<DataContext>().WeeklyDigests.SingleAsync()).Status);
        await services.DisposeAsync();services=Build();Assert.Equal("already_captured",(await Run("capture")).Status);
        Assert.Equal(bytes,await File.ReadAllBytesAsync(final));
    }
    [LocalPostgresMigrationFact]
    public async Task CancellationAfterPublication_ReleasesBothLeasesAndRecovers()
    {
        await Prepare();using var cancelled=new CancellationTokenSource();faults.CancelAfterPublish=cancelled;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>Run("capture",ct:cancelled.Token));
        Assert.Single(Directory.GetFiles(email.SinkRoot!,"*.eml"));
        Assert.Equal("already_captured",(await Run("capture")).Status);
    }
    [LocalPostgresMigrationFact]
    public async Task OptOutImmediatelyBeforeFinalLock_PreventsPublication()
    {
        await Prepare();faults.BeforeBegin=async()=>
        {
            await using var scope=services.CreateAsyncScope();await scope.ServiceProvider.GetRequiredService<IUnsubscribeStore>().UnsubscribeAsync(userId,DateTime.UtcNow,default);
        };
        Assert.Equal("email_unsubscribed",(await Run("capture")).Reason);Assert.Empty(Directory.GetFiles(email.SinkRoot!));
    }
    [LocalPostgresMigrationFact]
    public async Task FinalRowLock_SerializesUnsubscribe_AndAccountLeaseSerializesDeletion()
    {
        await Prepare();await using var scope=services.CreateAsyncScope();var sp=scope.ServiceProvider;
        await using(var account=await sp.GetRequiredService<IAccountEmailLease>().AcquireAsync(userId,default))
        {
            using var cancelled=new CancellationTokenSource(150);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async()=>{await using var blocked=await sp.GetRequiredService<IAccountEmailLease>().AcquireAsync(userId,cancelled.Token);});
        }
        await using(var transaction=await sp.GetRequiredService<ILocalDigestCaptureStore>().BeginAsync(userId,week,default))
        {
            using var cancelled=new CancellationTokenSource(150);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>sp.GetRequiredService<IUnsubscribeStore>().UnsubscribeAsync(userId,DateTime.UtcNow,cancelled.Token));
        }
        await sp.GetRequiredService<IUnsubscribeStore>().UnsubscribeAsync(userId,DateTime.UtcNow,default);
        Assert.Equal("email_unsubscribed",(await Run("capture")).Reason);
    }
    [LocalPostgresMigrationFact]
    public async Task EmptyDigest_CapturesAndMissingCompletedReceiptIsIntegrityStop()
    {
        await using(var scope=services.CreateAsyncScope())await scope.ServiceProvider.GetRequiredService<DataContext>().TasteSignals.ExecuteDeleteAsync();
        await Prepare();Assert.Equal("local_captured",(await Run("capture")).Status);
        var file=Assert.Single(Directory.GetFiles(email.SinkRoot!,"*.eml"));await using(var scope=services.CreateAsyncScope())
        {
            var parsed=scope.ServiceProvider.GetRequiredService<LocalEmailMessageSerializer>().Parse(await File.ReadAllBytesAsync(file));Assert.Contains("0 picks",parsed.PlainText);
        }
        File.Delete(file);Assert.Equal("email_receipt_missing",(await Run("capture")).Reason);Assert.Empty(Directory.GetFiles(email.SinkRoot!));
    }
    [LocalPostgresMigrationFact]
    public async Task FailureAfterSqlCommit_ReturnsVerifiedExistingReceiptOnFreshAttempt()
    {
        await Prepare();faults.FailAfterCommit=true;Assert.Equal("already_captured",(await Run("capture")).Status);
        Assert.Single(Directory.GetFiles(email.SinkRoot!,"*.eml"));Assert.Equal("already_captured",(await Run("capture")).Status);
    }
    [LocalPostgresMigrationFact]
    public async Task DeletionWaitsForPublication_ThenRemovesOwnedFinal()
    {
        await Prepare();var begun=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        faults.BeforeBegin=async()=>{begun.SetResult();await release.Task;};
        var capture=Run("capture");await begun.Task;
        var deletion=Task.Run(async()=>
        {
            await using var scope=services.CreateAsyncScope();var sp=scope.ServiceProvider;
            await using var lease=await sp.GetRequiredService<IAccountEmailLease>().AcquireAsync(userId,default);
            Assert.True(await sp.GetRequiredService<ISender>().Send(new DeleteUserAccountCommand(userId)));
            Assert.True((await sp.GetRequiredService<ILocalEmailArchive>().DeleteAsync(userId,default)).Complete);
        });
        await Task.Delay(100);Assert.False(deletion.IsCompleted);release.SetResult();
        Assert.Equal("local_captured",(await capture).Status);await deletion;Assert.Empty(Directory.GetFiles(email.SinkRoot!));
    }
    private sealed class Faults { public int FailCommits;public bool FailAfterCommit;public CancellationTokenSource? CancelAfterPublish;public Func<Task>? BeforeBegin; }
    private sealed class FaultStore(ILocalDigestCaptureStore inner,Faults faults):ILocalDigestCaptureStore
    {
        public Task<LocalCaptureInputs> ReadAsync(Guid id,IsoWeek week,CancellationToken ct)=>inner.ReadAsync(id,week,ct);
        public async Task<ILocalDigestCaptureTransaction> BeginAsync(Guid id,IsoWeek week,CancellationToken ct)
        {var before=faults.BeforeBegin;faults.BeforeBegin=null;if(before is not null)await before();return new FaultTransaction(await inner.BeginAsync(id,week,ct),faults);}
        public Task RecordFailureAsync(Guid id,IsoWeek week,string reason,CancellationToken ct)=>inner.RecordFailureAsync(id,week,reason,ct);
    }
    private sealed class FaultTransaction(ILocalDigestCaptureTransaction inner,Faults faults):ILocalDigestCaptureTransaction
    {
        public LocalCaptureInputs Inputs=>inner.Inputs;
        public async Task CommitCapturedAsync(CancellationToken ct)
        {
            var cancellation=faults.CancelAfterPublish;faults.CancelAfterPublish=null;cancellation?.Cancel();ct.ThrowIfCancellationRequested();
            if(faults.FailCommits>0){faults.FailCommits--;throw new LocalCaptureTransientException();}
            await inner.CommitCapturedAsync(ct);
            if(faults.FailAfterCommit){faults.FailAfterCommit=false;throw new LocalCaptureTransientException();}
        }
        public ValueTask DisposeAsync()=>inner.DisposeAsync();
    }
}
