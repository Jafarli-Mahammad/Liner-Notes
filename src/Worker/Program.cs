using System.Text.Json;
using LinerNotes.Application;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.DataAccess;
using LinerNotes.Infrastructure;
using LinerNotes.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

try
{
    var builder=Host.CreateApplicationBuilder(args);
    var options=LocalWorkerOptions.Parse(builder.Configuration);
    if(!builder.Environment.IsDevelopment()) throw new ArgumentException("development_worker_required");
    var connection=new NpgsqlConnectionStringBuilder(builder.Configuration["ConnectionStrings:DefaultConnection"]);
    if(connection.Host is not ("127.0.0.1" or "::1" or "localhost")) throw new ArgumentException("loopback_database_required");
    builder.Configuration["Generation:Origin"]=options.Origin.ToString();
    builder.Services.AddApplication().AddDataAccess(builder.Configuration).AddInfrastructure(builder.Configuration);
    builder.Services.AddSingleton<LocalDigestWorkerRunner>();
    using var host=builder.Build();
    using var cancellation=new CancellationTokenSource();
    Console.CancelKeyPress+=(_,e)=>{e.Cancel=true;cancellation.Cancel();};
    var result=await host.Services.GetRequiredService<LocalDigestWorkerRunner>().RunAsync(options,cancellation.Token);
    Console.WriteLine(JsonSerializer.Serialize(result));
    return result.Status=="stopped"?2:0;
}
catch(OperationCanceledException){Console.WriteLine("{\"Status\":\"cancelled\"}");return 130;}
catch(GenerationStoppedException ex){Console.WriteLine(JsonSerializer.Serialize(new LocalWorkerResult("stopped",ex.Message)));return 2;}
catch(Exception){Console.WriteLine("{\"Status\":\"stopped\",\"Reason\":\"local_worker_configuration_or_io\"}");return 2;}
