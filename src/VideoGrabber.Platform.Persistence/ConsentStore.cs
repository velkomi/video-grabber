using Npgsql;
using VideoGrabber.Platform.Contracts;

namespace VideoGrabber.Platform.Persistence;

public sealed class ConsentConflictException : Exception;

public sealed class ConsentStore(CreditLedger ledger, TimeProvider clock)
{
    public async Task<ConsentEvent> RecordAsync(Guid accountId, ConsentRequest request, string documentSnapshot, CancellationToken ct)
    {
        if (documentSnapshot.Length > 64 * 1024 || Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(documentSnapshot))).ToLowerInvariant() != request.DocumentHash)
            throw new ConsentConflictException();
        var operation = request.Purpose == "course_rights" ? request.OperationId ?? request.IntentId : (Guid?)null;
        await using var c = await ledger.DataSource.OpenConnectionAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);
        await using (var addDocument = new NpgsqlCommand("insert into licensing.document_versions(document_id,document_version,document_hash,content_text) values(@d,@v,@h,@text) on conflict(document_id,document_version) do nothing", c, tx))
        {
            addDocument.Parameters.AddWithValue("d", request.DocumentId); addDocument.Parameters.AddWithValue("v", request.Version);
            addDocument.Parameters.AddWithValue("h", request.DocumentHash); addDocument.Parameters.AddWithValue("text", documentSnapshot);
            await addDocument.ExecuteNonQueryAsync(ct);
        }
        await using (var checkDocument = new NpgsqlCommand("select document_hash from licensing.document_versions where document_id=@d and document_version=@v", c, tx))
        {
            checkDocument.Parameters.AddWithValue("d", request.DocumentId); checkDocument.Parameters.AddWithValue("v", request.Version);
            if ((string?)await checkDocument.ExecuteScalarAsync(ct) != request.DocumentHash) throw new ConsentConflictException();
        }
        await using (var cmd = new NpgsqlCommand("""
            insert into licensing.consent_events(account_id,document_id,document_version,document_hash,decision,purpose,intent_id,operation_id,recorded_at)
            values(@a,@d,@v,@h,@decision,@purpose,@intent,@operation,@now) on conflict(account_id,intent_id) do nothing
            """, c, tx))
        {
            cmd.Parameters.AddWithValue("a", accountId); cmd.Parameters.AddWithValue("d", request.DocumentId);
            cmd.Parameters.AddWithValue("v", request.Version); cmd.Parameters.AddWithValue("h", request.DocumentHash);
            cmd.Parameters.AddWithValue("decision", request.Decision); cmd.Parameters.AddWithValue("purpose", request.Purpose);
            cmd.Parameters.AddWithValue("intent", request.IntentId);
            cmd.Parameters.AddWithValue("operation", NpgsqlTypes.NpgsqlDbType.Uuid, (object?)operation ?? DBNull.Value);
            cmd.Parameters.AddWithValue("now", clock.GetUtcNow()); await cmd.ExecuteNonQueryAsync(ct);
        }
        await using var read = new NpgsqlCommand("select sequence,document_id,document_version,document_hash,decision,purpose,intent_id,operation_id,recorded_at from licensing.consent_events where account_id=@a and intent_id=@i", c, tx);
        read.Parameters.AddWithValue("a", accountId); read.Parameters.AddWithValue("i", request.IntentId);
        await using var r = await read.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new InvalidOperationException();
        var result = Event(r);
        if (result.DocumentId != request.DocumentId || result.Version != request.Version || result.DocumentHash != request.DocumentHash
            || result.Decision != request.Decision || result.Purpose != request.Purpose || result.OperationId != operation) throw new ConsentConflictException();
        await r.DisposeAsync(); await tx.CommitAsync(ct);
        return result;
    }

    public async Task<IReadOnlyList<ConsentEvent>> ReadAsync(Guid accountId, CancellationToken ct)
    {
        await using var c = await ledger.DataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("select sequence,document_id,document_version,document_hash,decision,purpose,intent_id,operation_id,recorded_at from licensing.consent_events where account_id=@a order by sequence desc limit 100", c);
        cmd.Parameters.AddWithValue("a", accountId); await using var r = await cmd.ExecuteReaderAsync(ct);
        var result = new List<ConsentEvent>(); while (await r.ReadAsync(ct)) result.Add(Event(r)); return result;
    }

    public async Task<bool> HasCourseRightsAsync(Guid accountId, Guid operationId, ProductDocument document, CancellationToken ct)
    {
        await using var c = await ledger.DataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            select decision='accepted' and document_version=@v and document_hash=@h
            from licensing.consent_events where account_id=@a and operation_id=@o and purpose='course_rights'
            order by sequence desc limit 1
            """, c);
        cmd.Parameters.AddWithValue("a", accountId); cmd.Parameters.AddWithValue("o", operationId);
        cmd.Parameters.AddWithValue("v", document.Version); cmd.Parameters.AddWithValue("h", document.Sha256);
        return await cmd.ExecuteScalarAsync(ct) is true;
    }

    private static ConsentEvent Event(NpgsqlDataReader r) => new(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5), r.GetGuid(6), r.IsDBNull(7) ? null : r.GetGuid(7), r.GetFieldValue<DateTimeOffset>(8));
}
