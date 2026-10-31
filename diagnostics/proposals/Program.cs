using System.Text.Json;
using LinerNotes.Infrastructure.Storage;

internal static partial class Audit
{
    public static async Task Main(string[] args)
    {
        if (args.Length != 1 || args[0] is not ("storage-alternatives" or "auth-boundary" or "artwork-persist" or "artwork-restore" or "storage-protocol"))
            throw new ArgumentException("Specify storage-alternatives, auth-boundary, artwork-persist or artwork-restore; production attachment is not supported.");
        if (args[0] == "storage-alternatives") await StorageAlternatives();
        else if (args[0] == "storage-protocol") await StorageProtocol();
        else if (args[0] == "auth-boundary") AuthBoundary();
        else await ArtworkRestart(args[0] == "artwork-persist");
    }

    static string Scratch(string name) => System.IO.Path.Combine(System.IO.Path.GetTempPath(), name);
    static void Emit(object value) => Console.WriteLine(JsonSerializer.Serialize(value));
    static long IO(string key) => OperatingSystem.IsLinux()
        ? long.Parse(File.ReadLines("/proc/self/io").Single(line => line.StartsWith(key + ":", StringComparison.Ordinal)).Split(':')[1].Trim())
        : 0;
    static GenerationStorageOptions StorageOptions(string directory)
    {
        Directory.CreateDirectory(directory);
        var options = new GenerationStorageOptions
        {
            InventoryPath = System.IO.Path.Combine(directory, "inventory.json"),
            LeasePath = System.IO.Path.Combine(directory, "lease"), DatabaseOverheadBytesPerPick = 1000,
            ArtifactRoots = LocalGenerationStorage.Categories.ToDictionary(category => category,
                category => new[] { System.IO.Path.Combine(directory, category) })
        };
        foreach (string path in options.ArtifactRoots.Values.SelectMany(value => value)) Directory.CreateDirectory(path);
        return options;
    }
}
