using LinerNotes.Application.Common.Interfaces;
using Xunit;

namespace LinerNotes.Application.Tests.Common;

public sealed class ApplicationBoundaryTests
{
    [Fact]
    public void Application_DoesNotReferenceStorageOrProviderImplementations()
    {
        var forbidden = new[]
        {
            "Microsoft.EntityFrameworkCore", "Npgsql", "Dapper",
            "LinerNotes.DataAccess", "LinerNotes.Infrastructure", "LinerNotes.Presentation", "LinerNotes.Worker"
        };

        var references = typeof(IUnitOfWork).Assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(references, reference => forbidden.Any(prefix =>
            reference.Name == prefix || reference.Name!.StartsWith(prefix + ".", StringComparison.Ordinal)));
    }
}
