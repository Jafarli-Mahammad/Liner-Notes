using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using LinerNotes.Application.Common.Interfaces;
using Npgsql;

namespace LinerNotes.DataAccess.Persistence;

/// <summary>Session ownership: account lease, artifact lease, final account-row lock.</summary>
public sealed class AccountEmailLease(string connectionString) : IAccountEmailLease
{
    public async Task<IAsyncDisposable> AcquireAsync(Guid userId, CancellationToken ct)
    {
        if (userId == Guid.Empty) throw new ArgumentException("Account required.", nameof(userId));
        long key = BinaryPrimitives.ReadInt64LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes("local-email:" + userId.ToString("N"))));
        var connection = new NpgsqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(ct);
            await using var command = new NpgsqlCommand("SELECT pg_advisory_lock(@key)", connection);
            command.Parameters.AddWithValue("key", key);
            await command.ExecuteNonQueryAsync(ct);
            return new Lease(connection, key);
        }
        catch { await connection.DisposeAsync(); throw; }
    }

    private sealed class Lease(NpgsqlConnection connection, long key) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var command = new NpgsqlCommand("SELECT pg_advisory_unlock(@key)", connection);
                command.Parameters.AddWithValue("key", key);
                await command.ExecuteNonQueryAsync(CancellationToken.None);
            }
            finally { await connection.DisposeAsync(); }
        }
    }
}
