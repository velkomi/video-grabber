using Npgsql;

namespace VideoGrabber.Platform.Persistence;

public static class ConsistencyReport
{
    private static readonly (string Name, string Sql)[] Checks =
    [
        ("negative_grant_buckets", """
            select count(*) from licensing.entitlement_grants
            where available < 0 or reserved < 0
            """),
        ("credit_grant_ledger_mismatch", """
            select count(*)
            from licensing.entitlement_grants g
            left join (
              select grant_id,
                     coalesce(sum(available_delta),0) as available,
                     coalesce(sum(reserved_delta),0) as reserved
              from licensing.credit_ledger
              where grant_id is not null
              group by grant_id
            ) l on l.grant_id=g.grant_id
            where g.kind in ('credits','hybrid')
              and (coalesce(l.available,0)<>g.available
                   or coalesce(l.reserved,0)<>g.reserved)
            """),
        ("completed_job_missing_artifact", """
            select count(*)
            from licensing.jobs j
            left join licensing.artifacts a on a.artifact_id=j.artifact_id
            where j.state='completed'
              and (j.artifact_id is null or a.artifact_id is null)
            """),
        ("artifact_job_account_mismatch", """
            select count(*)
            from licensing.artifacts a
            join licensing.jobs j on j.job_id=a.job_id
            where a.account_id<>j.account_id
               or a.fence<>j.fence
            """),
        ("terminal_reservation_missing_fence", """
            select count(*)
            from licensing.reservations
            where state in ('completed','review_required')
              and (attempt_id is null or fence is null or evidence_id is null)
            """),
        ("held_credit_reservation_missing_grant", """
            select count(*)
            from licensing.reservations
            where state='reserved' and uses_credit and grant_id is null
            """),
        ("duplicate_purchase_source_reference", """
            select count(*) from (
              select source_reference
              from licensing.entitlement_grants
              where source='purchase' and source_reference is not null
              group by source_reference
              having count(*)>1
            ) q
            """),
        ("payment_success_without_event", """
            select count(*)
            from licensing.payments p
            where p.state='succeeded'
              and not exists (
                select 1 from licensing.payment_events e
                where e.payment_id=p.payment_id and e.status='succeeded')
            """),
        ("refunded_payment_without_refund_event", """
            select count(*)
            from licensing.payments p
            where p.state='refunded'
              and not exists (
                select 1 from licensing.payment_events e
                where e.payment_id=p.payment_id and e.status='refunded')
            """),
        ("subscription_paid_through_invalid", """
            select count(*)
            from licensing.subscriptions
            where paid_through<=created_at
               or (state in ('canceled','review_required') and auto_renew)
            """),
        ("duplicate_identity_binding", """
            select count(*) from (
              select issuer,provider,provider_subject
              from licensing.identities
              group by issuer,provider,provider_subject
              having count(*)>1
            ) q
            """),
        ("live_artifact_missing_storage_file_marker", """
            select count(*)
            from licensing.artifacts
            where expired_at is null
              and storage_path is not null
              and length(storage_path)=0
            """)
    ];

    public static async Task<Dictionary<string,long>> RunAsync(
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        await using var connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        var result = new Dictionary<string,long>(StringComparer.Ordinal);
        foreach (var check in Checks)
        {
            await using var command =
                new NpgsqlCommand(check.Sql, connection);
            var value = Convert.ToInt64(
                await command.ExecuteScalarAsync(cancellationToken));
            result.Add(check.Name, value);
        }
        return result;
    }
}
