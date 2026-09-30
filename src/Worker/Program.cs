using LinerNotes.Application;
using LinerNotes.DataAccess;
using LinerNotes.Infrastructure;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddDataAccess(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

var host = builder.Build();
host.Run();
