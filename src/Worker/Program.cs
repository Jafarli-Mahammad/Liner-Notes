using System.Text.Json;
using LinerNotes.Application;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.DataAccess;
using LinerNotes.Infrastructure;
using LinerNotes.Infrastructure.Email;
using LinerNotes.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Npgsql;

try
{
    var builder=Host.CreateApplicationBuilder(args);
if (builder.Configuration["Production:ConfigurationFile"] is { } configurationFile)
{
    if (!Path.IsPathFullyQualified(configurationFile)) throw new ArgumentException("absolute_operator_configuration_required");
    builder.Configuration.AddJsonFile(configurationFile,optional:false,reloadOnChange:false);
}
if (builder.Configuration["action"] is "verify-storage" or "establish-storage" or "change-storage-policy" or "restore-storage")
{
    var action = builder.Configuration["action"]!;
    builder.Logging.ClearProviders();
    builder.Configuration["DiscoveryRuntime:RunJobs"] = "false";
    builder.Configuration["DiscoveryRuntime:EnforceStorageAdmission"] = "true";
    builder.Services.AddApplication().AddDataAccess(builder.Configuration).AddInfrastructure(builder.Configuration);
    using var operatorHost = builder.Build();
    await using var operatorScope = operatorHost.Services.CreateAsyncScope();
    var verification = await StorageOperator.ExecuteAsync(operatorScope.ServiceProvider,action,builder.Configuration);
    Console.WriteLine(JsonSerializer.Serialize(verification));
    return 0;
}
    bool scheduled=builder.Configuration["action"]=="schedule";
    var options=scheduled?null:LocalWorkerOptions.Parse(builder.Configuration);
    var scheduleOptions=scheduled?LocalScheduleOptions.Parse(builder.Configuration,args):null;
    if (scheduled && builder.Configuration.GetValue<bool>("DiscoveryRuntime:RunJobs"))
        throw new ArgumentException("synthetic_schedule_requires_jobs_disabled_use_storage_admission");
    if(scheduled) builder.Logging.ClearProviders(); // Scheduled CLI emits only its bounded result JSON.
    if(!builder.Environment.IsDevelopment()) throw new ArgumentException("development_worker_required");
    var connection=new NpgsqlConnectionStringBuilder(builder.Configuration["ConnectionStrings:DefaultConnection"]);
    if(connection.Host is not ("127.0.0.1" or "::1" or "localhost")) throw new ArgumentException("loopback_database_required");
    builder.Configuration["Generation:Origin"]=scheduled?"Synthetic":options!.Origin.ToString();
    builder.Services.AddApplication().AddDataAccess(builder.Configuration).AddInfrastructure(builder.Configuration);
    builder.Services.AddSingleton<LocalDigestWorkerRunner>();
    if(scheduleOptions is not null)
    {
        builder.Services.AddSingleton(scheduleOptions);
        builder.Services.AddSingleton<LocalScheduleRunState>();
        builder.Services.AddHostedService<LocalScheduledWorker>();
    }
    using var host=builder.Build();
    using var cancellation=new CancellationTokenSource();
    Console.CancelKeyPress+=(_,e)=>{e.Cancel=true;cancellation.Cancel();};
    if(scheduled)
    {
        host.Services.GetRequiredService<LocalEmailOptions>().Validate();
        host.Services.GetRequiredService<LinerNotes.Application.Common.Models.Recommendation.GenerationConfiguration>().Scoring();
        var runState=host.Services.GetRequiredService<LocalScheduleRunState>();
        await host.RunAsync(cancellation.Token);
        var scheduledResult=runState.Result;
        Console.WriteLine(JsonSerializer.Serialize(scheduledResult));
        return scheduledResult.Status=="cancelled"?130:scheduledResult.Status=="completed"?0:2;
    }
    var result=await host.Services.GetRequiredService<LocalDigestWorkerRunner>().RunAsync(options!,cancellation.Token);
    Console.WriteLine(JsonSerializer.Serialize(result));
    return result.Status=="stopped"?2:0;
}
catch(OperationCanceledException){Console.WriteLine("{\"Status\":\"cancelled\"}");return 130;}
catch(GenerationStoppedException ex){Console.WriteLine(JsonSerializer.Serialize(new LocalWorkerResult("stopped",ex.Message)));return 2;}
catch(Exception){Console.WriteLine("{\"Status\":\"stopped\",\"Reason\":\"local_worker_configuration_or_io\"}");return 2;}
