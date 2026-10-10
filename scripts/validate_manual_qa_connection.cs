#:project ../src/DataAccess/DataAccess.csproj

using Npgsql;
using System.Text;

const string message = "The manual QA launcher requires a loopback host and database liner_notes_manual_qa.";
try
{
    string? connectionString = Environment.GetEnvironmentVariable("LINER_MANUAL_QA_CONNECTION_STRING");
    if (string.IsNullOrWhiteSpace(connectionString)) throw new ArgumentException();
    if (HasRepeatedTargetField(connectionString)) throw new ArgumentException();
    var connection = new NpgsqlConnectionStringBuilder(connectionString);
    if (connection.Host is not ("localhost" or "127.0.0.1" or "::1") ||
        connection.Database != "liner_notes_manual_qa") throw new ArgumentException();
    return 0;
}
catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
{
    Console.Error.WriteLine(message);
    return 2;
}

static bool HasRepeatedTargetField(string value)
{
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var field = new StringBuilder();
    char quote = '\0';
    for (int index = 0; index <= value.Length; index++)
    {
        char current = index < value.Length ? value[index] : ';';
        if (quote != '\0')
        {
            if (current == quote)
            {
                if (index + 1 < value.Length && value[index + 1] == quote) { field.Append(current); index++; }
                else quote = '\0';
            }
            else field.Append(current);
            continue;
        }
        if (current is '\'' or '"') { quote = current; field.Append(current); continue; }
        if (current != ';') { field.Append(current); continue; }

        string key = field.ToString().Split('=', 2)[0].Trim();
        string? target = key.ToLowerInvariant() switch
        {
            "host" or "server" or "address" or "addr" or "network address" => "host",
            "database" or "initial catalog" or "initialcatalog" or "db" => "database",
            _ => null
        };
        if (target is not null && !seen.Add(target)) return true;
        field.Clear();
    }
    return quote != '\0';
}
