using LinerNotes.Application.Common.Interfaces;
using Npgsql;
namespace LinerNotes.DataAccess.Persistence;

internal sealed class MonitoredEmailLease : IEmailLeaseGuard
{
    private readonly NpgsqlConnection connection;
    private readonly CancellationTokenSource stop = new(), lost = new();
    private readonly Task monitor;
    public CancellationToken Lost => lost.Token;
    public MonitoredEmailLease(NpgsqlConnection connection) { this.connection = connection; monitor = MonitorAsync(); }
    private async Task MonitorAsync()
    {
        try
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), stop.Token);
                await using var command = new NpgsqlCommand("SELECT 1", connection) { CommandTimeout = 2 };
                await command.ExecuteScalarAsync(stop.Token);
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch (Exception) { await lost.CancelAsync(); }
    }
    public async ValueTask DisposeAsync()
    {
        await stop.CancelAsync(); await monitor; await connection.DisposeAsync(); stop.Dispose(); lost.Dispose();
    }
}
