using System.Reflection;
using LinerNotes.Domain.Catalog;
using Xunit;

namespace LinerNotes.Domain.Tests.Architecture;

public sealed class OnionArchitectureTests
{
    [Fact]
    public void Domain_MustHaveZeroExternalDependencies()
    {
        var domainAssembly = typeof(Tag).Assembly;
        var referencedAssemblies = domainAssembly.GetReferencedAssemblies();

        var allowedPrefixes = new[]
        {
            "System",
            "Microsoft.CSharp",
            "netstandard",
            "mscorlib"
        };

        foreach (var assemblyName in referencedAssemblies)
        {
            var isAllowed = allowedPrefixes.Any(prefix =>
                assemblyName.Name != null &&
                (assemblyName.Name == prefix || assemblyName.Name.StartsWith(prefix + ".")));

            Assert.True(isAllowed,
                $"Domain layer must have zero external dependencies, but references '{assemblyName.Name}'.");
        }
    }
}
