#:property PublishAot=false
#:project ../src/Worker/Worker.csproj

using System.Text.Json;
using LinerNotes.Application;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.DataAccess;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

// Explicit operator retry after SQL/Identity deletion; never a retention scheduler.
try
{
    var builder=Host.CreateApplicationBuilder(args);
    if(!builder.Environment.IsDevelopment() || !Guid.TryParse(builder.Configuration["user"],out var userId) || userId==Guid.Empty)
        throw new ArgumentException();
    var connection=new NpgsqlConnectionStringBuilder(builder.Configuration["ConnectionStrings:DefaultConnection"]);
    if(connection.Host is not ("127.0.0.1" or "::1" or "localhost")) throw new ArgumentException();
    builder.Services.AddApplication().AddDataAccess(builder.Configuration).AddInfrastructure(builder.Configuration);
    using var host=builder.Build();await using var scope=host.Services.CreateAsyncScope();var services=scope.ServiceProvider;
    await using var lease=await services.GetRequiredService<IAccountEmailLease>().AcquireAsync(userId,CancellationToken.None);
    var db=services.GetRequiredService<DataContext>();
    if(await db.Users.IgnoreQueryFilters().AnyAsync(u=>u.Id==userId) || await db.ApplicationUsers.IgnoreQueryFilters().AnyAsync(u=>u.Id==userId))
    {
        Console.WriteLine("{\"Status\":\"stopped\",\"Reason\":\"account_still_exists\"}");return 2;
    }
    var result=await services.GetRequiredService<ILocalEmailArchive>().DeleteAsync(userId,CancellationToken.None);
    Console.WriteLine(JsonSerializer.Serialize(new {Status=result.Complete?"cleanup_complete":"cleanup_incomplete",result.Issues}));
    return result.Complete?0:2;
}
catch(Exception)
{
    Console.WriteLine("{\"Status\":\"stopped\",\"Reason\":\"local_copy_cleanup_unavailable\"}");return 2;
}
