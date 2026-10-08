using Npgsql;

namespace VideoGrabber.Platform.Persistence;

public sealed partial class PromotionStore
{
    private sealed record Lot(Guid Id, string Currency, long Remaining, long Reserved, DateTimeOffset Available, DateTimeOffset Expires);
    private async Task<List<Lot>> LotsAsync(NpgsqlConnection c, NpgsqlTransaction tx, Guid a, CancellationToken ct)
    {
        await using var cmd = Command(c, tx, "select lot_id,currency,remaining_minor,reserved_minor,valid_from,expires_at from licensing.bonus_lots where account_id=@a order by expires_at,created_at,lot_id for update", ("a", a));
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var result = new List<Lot>();
        while (await r.ReadAsync(ct)) result.Add(new(r.GetGuid(0), r.GetString(1), r.GetInt64(2), r.GetInt64(3), r.GetFieldValue<DateTimeOffset>(4), r.GetFieldValue<DateTimeOffset>(5)));
        return result;
    }

    private async Task NormalizeDebtAsync(NpgsqlConnection c, NpgsqlTransaction tx, Guid a, CancellationToken ct)
    {
        var now = _clock.GetUtcNow();
        var lots = await LotsAsync(c, tx, a, ct);
        var remaining = lots.ToDictionary(x => x.Id, x => x.Remaining);
        foreach (var debt in lots.Where(x => x.Remaining < 0))
        {
            // A reservation shortfall is not a realized debit. Transferring
            // funds into an expired reserved lot would destroy them on cancel.
            // Settle only actual negative remaining, bringing it back to zero.
            var needed = checked(-remaining[debt.Id]);
            foreach (var credit in lots.Where(x => x.Id != debt.Id && x.Currency == debt.Currency && x.Available <= now && x.Expires > now))
            {
                var amount = Math.Min(needed, Math.Max(0, checked(remaining[credit.Id] - credit.Reserved)));
                if (amount == 0) continue;
                remaining[credit.Id] = checked(remaining[credit.Id] - amount);
                remaining[debt.Id] = checked(remaining[debt.Id] + amount);
                needed -= amount;
                await ExecAsync(c, tx, "update licensing.bonus_lots set remaining_minor=remaining_minor-@d where lot_id=@l", ct, ("d", amount), ("l", credit.Id));
                await ExecAsync(c, tx, "update licensing.bonus_lots set remaining_minor=remaining_minor+@d where lot_id=@l", ct, ("d", amount), ("l", debt.Id));
                var key = "offset:" + Guid.NewGuid();
                await EventAsync(c, tx, credit.Id, a, null, key + ":debit", "offset_debit", -amount, -amount, 0, ct);
                await EventAsync(c, tx, debt.Id, a, null, key + ":credit", "offset_credit", amount, amount, 0, ct);
                if (needed == 0) break;
            }
        }
    }

    private async Task<long> AvailableAsync(NpgsqlConnection c, NpgsqlTransaction tx, Guid a, string currency, CancellationToken ct)
    {
        var now = _clock.GetUtcNow();
        // The debt term deliberately includes expired and pending lots. A
        // clawback remains payable from future bonuses after its source expires.
        var value = await ScalarAsync(c, tx, """
            select greatest(0,
                coalesce(sum(greatest(remaining_minor-reserved_minor,0)) filter(where valid_from<=@now and expires_at>@now),0)
                - coalesce(sum(greatest(reserved_minor-remaining_minor,0)),0))::bigint
            from licensing.bonus_lots where account_id=@a and currency=@currency
            """, ct, ("a", a), ("currency", currency), ("now", now));
        return Convert.ToInt64(value);
    }

    private async Task ReserveWalletAsync(NpgsqlConnection c, NpgsqlTransaction tx, Guid account, Guid payment, string currency, long amount, CancellationToken ct)
    {
        if (await AvailableAsync(c, tx, account, currency, ct) < amount) throw new PromotionUnavailableException("bonus_unavailable");
        var now = _clock.GetUtcNow();
        var remaining = amount;
        foreach (var lot in (await LotsAsync(c, tx, account, ct)).Where(x => x.Currency == currency && x.Available <= now && x.Expires > now))
        {
            var take = Math.Min(remaining, Math.Max(0, checked(lot.Remaining - lot.Reserved)));
            if (take == 0) continue;
            await ExecAsync(c, tx, "update licensing.bonus_lots set reserved_minor=reserved_minor+@n where lot_id=@l", ct, ("n", take), ("l", lot.Id));
            await ExecAsync(c, tx, "insert into licensing.bonus_allocations(payment_id,lot_id,amount_minor,state) values(@p,@l,@n,'reserved')", ct, ("p", payment), ("l", lot.Id), ("n", take));
            await EventAsync(c, tx, lot.Id, account, payment, "reserve:" + payment + ":" + lot.Id, "reserve", -take, 0, take, ct);
            remaining -= take;
            if (remaining == 0) break;
        }
        if (remaining != 0) throw new PromotionUnavailableException("bonus_unavailable");
    }

    private async Task FinalizeWalletAsync(NpgsqlConnection c, NpgsqlTransaction tx, Guid payment, bool success, CancellationToken ct)
    {
        var allocations = new List<(Guid Lot, Guid Account, long Amount)>();
        await using (var cmd = Command(c, tx, """
            select x.lot_id,l.account_id,x.amount_minor from licensing.bonus_allocations x
            join licensing.bonus_lots l using(lot_id) where payment_id=@p and x.state='reserved' order by x.lot_id for update of x,l
            """, ("p", payment)))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct)) allocations.Add((r.GetGuid(0), r.GetGuid(1), r.GetInt64(2)));
        foreach (var (lot, account, amount) in allocations)
        {
            // Expiry/hold are checked only when reserving. Existing invoices
            // retain their terms and cannot lose funds during provider delay.
            await ExecAsync(c, tx, "update licensing.bonus_lots set reserved_minor=reserved_minor-@n,remaining_minor=remaining_minor-@spent where lot_id=@l", ct,
                ("n", amount), ("spent", success ? amount : 0L), ("l", lot));
            await ExecAsync(c, tx, "update licensing.bonus_allocations set state=@state where payment_id=@p and lot_id=@l", ct,
                ("state", success ? "spent" : "released"), ("p", payment), ("l", lot));
            var kind = success ? "spend" : "release";
            await EventAsync(c, tx, lot, account, payment, kind + ":" + payment + ":" + lot, kind, success ? -amount : amount, success ? -amount : 0, -amount, ct);
        }
    }

    private Task<int> EventAsync(NpgsqlConnection c, NpgsqlTransaction tx, Guid lot, Guid account, Guid? payment, string key, string kind, long amount, long remaining, long reserved, CancellationToken ct)
        => ExecAsync(c, tx, """
            insert into licensing.bonus_events(event_id,lot_id,account_id,payment_id,event_key,kind,amount_minor,remaining_delta,reserved_delta,created_at)
            values(@event,@lot,@account,@payment,@key,@kind,@amount,@remaining,@reserved,@now)
            """, ct, ("event", Guid.NewGuid()), ("lot", lot), ("account", account), ("payment", payment), ("key", key), ("kind", kind),
            ("amount", amount), ("remaining", remaining), ("reserved", reserved), ("now", _clock.GetUtcNow()));
}
