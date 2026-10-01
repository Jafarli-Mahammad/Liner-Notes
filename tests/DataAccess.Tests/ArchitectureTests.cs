using LinerNotes.DataAccess.Persistence;
using Xunit;

namespace LinerNotes.DataAccess.Tests;

public sealed class ArchitectureTests
{
    [Fact]
    public void DataAccess_MustNeverReferenceWebOrWorker()
    {
        var dataAccessAssembly = typeof(AppDbContext).Assembly;
        var referencedAssemblies = dataAccessAssembly.GetReferencedAssemblies();

        var forbiddenPrefixes = new[]
        {
            "LinerNotes.Web",
            "LinerNotes.Presentation",
            "LinerNotes.Worker"
        };

        foreach (var assemblyName in referencedAssemblies)
        {
            var isForbidden = forbiddenPrefixes.Any(forbidden =>
                assemblyName.Name != null &&
                (assemblyName.Name == forbidden || assemblyName.Name.StartsWith(forbidden + ".")));

            Assert.False(isForbidden,
                $"DataAccess layer must never reference Web or Worker hosts, but references '{assemblyName.Name}'.");
        }
    }
}
