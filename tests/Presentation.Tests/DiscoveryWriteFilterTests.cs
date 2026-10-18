using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Features.Discovery;
using LinerNotes.Presentation.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace LinerNotes.Presentation.Tests;

public sealed class DiscoveryWriteFilterTests
{
    [Theory]
    [InlineData(false, "POST", false)]
    [InlineData(true, "GET", false)]
    [InlineData(true, "POST", true)]
    public async Task OnlyEnabledMutationsUseAdmission(bool enabled, string method, bool admitted)
    {
        var writes = new Admission();
        var filter = new DiscoveryWriteFilter(writes, new() { RunJobs = enabled });
        var context = Context(method);
        var invoked = false;

        await filter.OnActionExecutionAsync(context, () =>
        {
            invoked = true;
            Assert.Equal(admitted, writes.Active);
            return Task.FromResult(new ActionExecutedContext(context, [], context.Controller));
        });

        Assert.True(invoked);
        Assert.Equal(admitted, writes.Committed);
    }

    [Fact]
    public async Task CapturedMvcExceptionRollsBackAdmission()
    {
        var writes = new Admission();
        var filter = new DiscoveryWriteFilter(writes, new() { RunJobs = true });
        var context = Context("POST");
        var failure = new InvalidOperationException("synthetic action failure");

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => filter.OnActionExecutionAsync(context,
            () => Task.FromResult(new ActionExecutedContext(context, [], context.Controller) { Exception = failure })));

        Assert.Same(failure, actual);
        Assert.False(writes.Committed);
        Assert.False(writes.Active);
    }

    private static ActionExecutingContext Context(string method)
    {
        var http = new DefaultHttpContext();
        http.Request.Method = method;
        return new(new ActionContext(http, new RouteData(), new ActionDescriptor()), [], new Dictionary<string, object?>(), new object());
    }

    private sealed class Admission : IDiscoveryWriteCoordinator
    {
        public bool Active { get; private set; }
        public bool Committed { get; private set; }
        public async Task<T> ExecuteAsync<T>(long estimatedBytes, Func<CancellationToken, Task<T>> write, CancellationToken ct)
        {
            Assert.Equal(131_072, estimatedBytes);
            Active = true;
            try
            {
                var result = await write(ct);
                Committed = true;
                return result;
            }
            finally { Active = false; }
        }
    }
}
