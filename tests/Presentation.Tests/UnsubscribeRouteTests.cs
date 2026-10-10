using System.Net;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Infrastructure.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using Xunit;

namespace LinerNotes.Presentation.Tests;

public sealed class UnsubscribeRouteTests
{
    [Theory]
    [InlineData("Development", false)]
    [InlineData("Production", true)]
    public async Task DisabledOrProduction_Returns404(string env, bool enabled)
    {
        await using var app = new App(env, enabled);
        using var client = app.CreateClient(new() { AllowAutoRedirect = false });
        foreach (var method in new[] { HttpMethod.Get, HttpMethod.Post })
        {
            using var request = new HttpRequestMessage(method, "/api/digests/unsubscribe?token=valid");
            request.Headers.Add("X-Forwarded-Proto", "https");
            if (method == HttpMethod.Post) request.Content = new FormUrlEncodedContent(new Dictionary<string,string>{{"token","valid"}});
            Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(request)).StatusCode);
        }
    }

    [Fact]
    public async Task Confirmation_IsReadOnly_AndPostIsIdempotent()
    {
        await using var app = new App("Development", true);
        using var client = app.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/digests/unsubscribe?token=invalid")).StatusCode);
        var page = await client.GetStringAsync("/api/digests/unsubscribe?token=valid");
        Assert.Contains("Confirm unsubscribe", page);
        Assert.Equal(0, app.Store.Calls);
        for (int i=0; i<2; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/digests/unsubscribe", new FormUrlEncodedContent(new Dictionary<string,string>{{"token","valid"}}))).StatusCode);
        Assert.Equal(2, app.Store.Calls);
        Assert.NotNull(app.Store.First);
    }

    private sealed class App(string env, bool enabled) : WebApplicationFactory<Program>
    {
        public Store Store { get; } = new();
        public Logs Logs { get; } = new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(env);
            builder.UseSetting("LocalEmail:Enabled", enabled.ToString());
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=127.0.0.1;Database=unused;Username=unused;Password=unused");
            builder.UseSetting("Jwt:SecretKey", "unsubscribe-route-tests-only-12345678901234567890");
            builder.UseSetting("Jwt:Issuer", "tests"); builder.UseSetting("Jwt:Audience", "tests");
            builder.UseSetting("ForwardedHeaders:KnownProxies:0", "127.0.0.1");
            builder.ConfigureLogging(logging=>logging.AddProvider(Logs));
            builder.ConfigureServices(s =>
            {
                s.RemoveAll<LocalEmailOptions>();
                s.AddSingleton(new LocalEmailOptions { Enabled=enabled, ApplicationOrigin="http://127.0.0.1:5000" });
                s.RemoveAll<IUnsubscribeTokens>(); s.AddSingleton<IUnsubscribeTokens>(new Tokens());
                s.RemoveAll<IUnsubscribeStore>(); s.AddSingleton<IUnsubscribeStore>(Store);
            });
        }
    }
    [Fact]
    public async Task CapabilityQuery_IsExcludedFromRequestLogs()
    {
        await using var app=new App("Development",true);using var client=app.CreateClient();
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/api/digests/unsubscribe?token=valid")).StatusCode);
        Assert.DoesNotContain(app.Logs.Messages,message=>message.Contains("token=valid",StringComparison.Ordinal));
    }
    private sealed class Logs : ILoggerProvider
    {
        public ConcurrentBag<string> Messages { get; }=new();
        public ILogger CreateLogger(string categoryName)=>new Logger(Messages);
        public void Dispose() { }
        private sealed class Logger(ConcurrentBag<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState:notnull=>null;
            public bool IsEnabled(LogLevel level)=>true;
            public void Log<TState>(LogLevel level,EventId eventId,TState state,Exception? exception,Func<TState,Exception?,string> formatter)=>messages.Add(formatter(state,exception));
        }
    }
    private sealed class Tokens : IUnsubscribeTokens
    {
        public string Create(Guid userId) => "valid";
        public bool TryRead(string? token, out Guid userId) { userId = Guid.Parse("11111111-1111-1111-1111-111111111111"); return token == "valid"; }
    }
    private sealed class Store : IUnsubscribeStore
    {
        public int Calls; public DateTime? First;
        public Task UnsubscribeAsync(Guid userId, DateTime utcNow, CancellationToken ct) { Calls++; First ??= utcNow; return Task.CompletedTask; }
    }
}
