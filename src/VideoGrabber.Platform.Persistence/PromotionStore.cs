using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Npgsql;
using VideoGrabber.Platform.Contracts;
using VideoGrabber.Platform.Core.Access;
using VideoGrabber.Platform.Core.Payments;

namespace VideoGrabber.Platform.Persistence;

public sealed class PromotionDisabledException : Exception;
public sealed class PromotionConflictException : Exception;
public sealed class PromotionUnavailableException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

public sealed partial class PromotionStore(CreditLedger ledger, TimeProvider clock, PromotionOptions options)
{
    private readonly NpgsqlDataSource _dataSource = ledger.DataSource;
    private readonly TimeProvider _clock = clock;
    private readonly PromotionOptions _options = options;
    private const long ReferralLock = 2026100801;

    public async Task<ReferralSummary> SummaryAsync(Guid accountId, CancellationToken ct)
    {
        RequireEnabled();
        await using var c = await _dataSource.OpenConnectionAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);
        await AccountLockAsync(c, tx, accountId, ct);
        await RequireAccountAsync(c, tx, accountId, false, ct);
        await NormalizeDebtAsync(c, tx, accountId, ct);
        var code = (string?)await ScalarAsync(c, tx, "select code from licensing.referral_codes where account_id=@a", ct, ("a", accountId));
        if (code is null)
        {
            code = Convert.ToHexString(RandomNumberGenerator.GetBytes(6));
            await ExecAsync(c, tx, "insert into licensing.referral_codes(account_id,code,created_at) values(@a,@code,@now)", ct,
                ("a", accountId), ("code", code), ("now", _clock.GetUtcNow()));
        }
        var balances = new List<BonusBalance>();
        await using (var command = Command(c, tx, """
            select currency,
              coalesce(sum(greatest(remaining_minor-reserved_minor,0)) filter(where valid_from<=@now and expires_at>@now),0)::bigint,
              coalesce(sum(greatest(remaining_minor-reserved_minor,0)) filter(where valid_from>@now and expires_at>@now),0)::bigint,
              coalesce(sum(reserved_minor),0)::bigint,
              coalesce(sum(greatest(reserved_minor-remaining_minor,0)),0)::bigint
            from licensing.bonus_lots where account_id=@a group by currency order by currency
            """, ("a", accountId), ("now", _clock.GetUtcNow())))
        await using (var r = await command.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct)) balances.Add(new(r.GetString(0), Math.Max(0, checked(r.GetInt64(1) - r.GetInt64(4))), r.GetInt64(2), r.GetInt64(3), r.GetInt64(4)));
        foreach (var currency in new[] { "RUB", "XTR" })
            if (balances.All(x => x.Currency != currency)) balances.Add(new(currency, 0, 0, 0, 0));
        var history = new List<BonusHistoryItem>();
        await using (var command = Command(c, tx, """
            select e.kind,e.amount_minor,l.currency,e.created_at,l.valid_from,l.expires_at
            from licensing.bonus_events e join licensing.bonus_lots l using(lot_id)
            where e.account_id=@a order by e.created_at desc,e.event_id desc limit 50
            """, ("a", accountId)))
        await using (var r = await command.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct)) history.Add(new(r.GetString(0), new(r.GetInt64(1), r.GetString(2)), r.GetFieldValue<DateTimeOffset>(3), r.GetFieldValue<DateTimeOffset>(4), r.GetFieldValue<DateTimeOffset>(5)));
        int invited, paid;
        await using (var stats = Command(c, tx, """
            with recursive owners as (
              select account_id from licensing.accounts where account_id=@a
              union select x.account_id from licensing.accounts x join owners p on x.merged_into=p.account_id),
            friends as (
              select x.account_id,x.merged_into,r.qualified_payment_id from licensing.referrals r
                join licensing.accounts x on x.account_id=r.referee_id where r.referrer_id in(select account_id from owners)
              union select x.account_id,x.merged_into,p.qualified_payment_id from licensing.accounts x join friends p on x.account_id=p.merged_into)
            select count(distinct account_id) filter(where merged_into is null and account_id<>@a)::integer,
              count(distinct account_id) filter(where merged_into is null and account_id<>@a and qualified_payment_id is not null)::integer
            from friends
            """, ("a", accountId)))
        await using (var r = await stats.ExecuteReaderAsync(ct))
        { await r.ReadAsync(ct); invited = r.GetInt32(0); paid = r.GetInt32(1); }
        await tx.CommitAsync(ct);
        return new(code, new Uri(_options.PublicOrigin.TrimEnd('/') + "/?ref=" + code),
            new Uri("https://t.me/" + _options.BotUsername + "?start=ref_" + code), invited, paid, balances.ToArray(), history.ToArray());
    }

    public async Task<bool> ClaimAsync(Guid accountId, string code, CancellationToken ct)
    {
        RequireEnabled();
        code = NormalizeCode(code, true);
        await using var c = await _dataSource.OpenConnectionAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);
        await ExecAsync(c, tx, "select pg_advisory_xact_lock(@key)", ct, ("key", ReferralLock));
        await RequireAccountAsync(c, tx, accountId, true, ct);
        var owner = await ScalarAsync(c, tx, "select account_id from licensing.referral_codes where code=@code", ct, ("code", code));
        if (owner is not Guid rawOwner) throw new PromotionUnavailableException("referral_invalid");
        var referrer = await CanonicalAsync(c, tx, rawOwner, ct);
        foreach (var id in new[] { accountId, referrer }.Distinct().Order()) await AccountLockAsync(c, tx, id, ct);
        await RequireAccountAsync(c, tx, accountId, true, ct);
        await RequireAccountAsync(c, tx, referrer, false, ct);
        if (await CanonicalAsync(c, tx, accountId, ct) == referrer) throw new PromotionConflictException();
        var previous = await ReferralAsync(c, tx, accountId, ct);
        if (previous is Guid previousOwner)
        {
            if (previousOwner != referrer) throw new PromotionConflictException();
            await tx.CommitAsync(ct);
            return true;
        }
        var alreadyPaid = await ScalarAsync(c, tx, "select first_purchase_at from licensing.accounts where account_id=@a", ct, ("a", accountId));
        if (alreadyPaid is not null && alreadyPaid is not DBNull) throw new PromotionUnavailableException("first_purchase_required");
        await ExecAsync(c, tx, "insert into licensing.referrals(referee_id,referrer_id,created_at) values(@a,@r,@now)", ct,
            ("a", accountId), ("r", referrer), ("now", _clock.GetUtcNow()));
        await tx.CommitAsync(ct);
        return true;
    }

    public async Task<PromotionQuote> QuoteAsync(Guid accountId, PromotionQuoteRequest request, CatalogProduct product, string catalogVersion, CancellationToken ct)
    {
        RequireEnabled();
        if (request.Sku != product.Sku || !product.Prices.TryGetValue(request.Provider, out var price)) throw new PromotionConflictException();
        var code = string.IsNullOrWhiteSpace(request.PromoCode) ? null : NormalizeCode(request.PromoCode, false);
        await using var c = await _dataSource.OpenConnectionAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);
        await AccountLockAsync(c, tx, accountId, ct);
        var first = await RequireAccountAsync(c, tx, accountId, false, ct);
        await NormalizeDebtAsync(c, tx, accountId, ct);
        var qualified = Qualifies(product);
        var allowOneTimeBenefits = qualified && !request.Recurring;
        var referrer = qualified && first ? await ReferralAsync(c, tx, accountId, ct) : null;
        if (referrer == accountId) referrer = null;
        var promo = code is null ? null : await ReadPromoAsync(c, tx, code, ct);
        if (code is not null && (!allowOneTimeBenefits || promo is null)) throw new PromotionUnavailableException("promo_unavailable");
        if (promo is not null) await CheckPromoAsync(c, tx, accountId, promo, product.Sku, price.Currency, first, 0, ct);
        var available = allowOneTimeBenefits && request.UseBonuses ? await AvailableAsync(c, tx, accountId, price.Currency, ct) : 0;
        var amounts = PromotionPolicy.Calculate(new(price.MinorUnits, price.Currency), promo?.Definition.DiscountBasisPoints ?? 0,
            available, referrer is not null, first && qualified, request.Recurring, request.UseBonuses && allowOneTimeBenefits);
        // The policy selects the best one-time promotion. A losing coupon never
        // consumes its limit or budget; referral attribution still earns cash reward.
        if (amounts.ReferralApplied) code = null;
        if (code is not null && promo is not null)
            await CheckPromoAsync(c, tx, accountId, promo, product.Sku, price.Currency, first, amounts.Discount.MinorUnits, ct);
        var id = Guid.NewGuid();
        var now = _clock.GetUtcNow();
        var expires = now.AddMinutes(10);
        await ExecAsync(c, tx, """
            insert into licensing.promotion_quotes(quote_id,account_id,sku,provider,catalog_version,product_snapshot,currency,
              original_minor,discount_minor,bonus_minor,payable_minor,promo_code,referrer_id,referral_applied,recurring,use_bonuses,created_at,expires_at)
            values(@id,@a,@sku,@provider,@version,@snapshot,@currency,@original,@discount,@bonus,@payable,@code,@referrer,@referral,@recurring,@use,@now,@expires)
            """, ct, ("id", id), ("a", accountId), ("sku", request.Sku), ("provider", request.Provider), ("version", catalogVersion),
            ("snapshot", ProductSnapshot(product, request.Provider)), ("currency", price.Currency), ("original", amounts.Original.MinorUnits),
            ("discount", amounts.Discount.MinorUnits), ("bonus", amounts.Bonus.MinorUnits), ("payable", amounts.Payable.MinorUnits),
            ("code", code), ("referrer", referrer), ("referral", amounts.ReferralApplied), ("recurring", request.Recurring),
            ("use", request.UseBonuses), ("now", now), ("expires", expires));
        await tx.CommitAsync(ct);
        return new(id, amounts.Original, amounts.Discount, amounts.Bonus, amounts.Payable, expires, code, amounts.ReferralApplied);
    }

    public async Task<PromoView> SavePromoAsync(Guid actorId, PromoDefinition definition, CancellationToken ct)
    {
        RequireEnabled();
        var d = definition with { Code = NormalizeCode(definition.Code, false), StartsAt = definition.StartsAt.ToUniversalTime(), EndsAt = definition.EndsAt.ToUniversalTime() };
        if (d.DiscountBasisPoints is < 1 or > 2000 || d.Currency is not ("RUB" or "XTR") || d.EndsAt <= d.StartsAt
            || d.MaxUses <= 0 || d.PerAccountLimit <= 0 || d.BudgetMinor <= 0 || d.Sku?.Length > 80) throw new PromotionUnavailableException("promo_invalid");
        await using var c = await _dataSource.OpenConnectionAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);
        await AccountLockAsync(c, tx, actorId, ct);
        await using (var actor = Command(c, tx, "select base_role,blocked_at,merged_into from licensing.accounts where account_id=@a for update", ("a", actorId)))
        await using (var r = await actor.ExecuteReaderAsync(ct))
            if (!await r.ReadAsync(ct) || r.GetString(0) != "owner_admin" || !r.IsDBNull(1) || !r.IsDBNull(2)) throw new PromotionUnavailableException("owner_required");
        await ExecAsync(c, tx, "select pg_advisory_xact_lock(hashtextextended(@code,2026100802))", ct, ("code", d.Code));
        // Financial terms of any previously issued quote stay immutable. An
        // existing code may only be activated/deactivated; create another code for new terms.
        var previous = await ReadPromoAsync(c, tx, d.Code, ct);
        if (previous is not null && previous.Definition with { Active = d.Active } != d) throw new PromotionConflictException();
        await ExecAsync(c, tx, """
            insert into licensing.promo_codes(code,discount_bps,sku,currency,starts_at,ends_at,max_uses,per_account_limit,budget_minor,first_purchase_only,active,actor_id)
            values(@code,@bps,@sku,@currency,@starts,@ends,@max,@per,@budget,@first,@active,@actor)
            on conflict(code) do update set active=excluded.active
            """, ct, ("code", d.Code), ("bps", d.DiscountBasisPoints), ("sku", d.Sku), ("currency", d.Currency), ("starts", d.StartsAt),
            ("ends", d.EndsAt), ("max", d.MaxUses), ("per", d.PerAccountLimit), ("budget", d.BudgetMinor), ("first", d.FirstPurchaseOnly), ("active", d.Active), ("actor", actorId));
        var view = (await ReadPromoAsync(c, tx, d.Code, ct))!;
        await tx.CommitAsync(ct);
        return view;
    }

    public async Task<PromoView[]> ListPromosAsync(CancellationToken ct)
    {
        RequireEnabled();
        await using var c = await _dataSource.OpenConnectionAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);
        var result = new List<PromoView>();
        await using var cmd = Command(c, tx, PromoSelect + " order by code");
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) result.Add(PromoRow(r));
        return result.ToArray();
    }

    private void RequireEnabled() { if (!_options.Enabled) throw new PromotionDisabledException(); }
    private static bool Qualifies(CatalogProduct p) => p.Kind == "time" && p.PlanId is "start" or "unlimited_video" or "full_course";
    private static string ProductSnapshot(CatalogProduct p, string provider) => JsonSerializer.Serialize(new { p.Sku, p.Kind, p.Credits, p.Days, p.PlanId, p.RecurringAllowed, Price = p.Prices[provider] });
    private static string NormalizeCode(string? value, bool referral)
    {
        if (value is null || value.Length > 64) throw new PromotionUnavailableException(referral ? "referral_invalid" : "promo_invalid");
        var code = value.Trim().ToUpperInvariant();
        if (!Regex.IsMatch(code, referral ? "^[A-Z0-9]{12}$" : "^[A-Z0-9_-]{3,32}$", RegexOptions.CultureInvariant))
            throw new PromotionUnavailableException(referral ? "referral_invalid" : "promo_invalid");
        return code;
    }
    private static NpgsqlCommand Command(NpgsqlConnection c, NpgsqlTransaction tx, string sql, params (string Name, object? Value)[] parameters)
    {
        var cmd = new NpgsqlCommand(sql, c, tx);
        foreach (var (name, value) in parameters) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return cmd;
    }
    private static async Task<int> ExecAsync(NpgsqlConnection c, NpgsqlTransaction tx, string sql, CancellationToken ct, params (string Name, object? Value)[] p)
    { await using var cmd = Command(c, tx, sql, p); return await cmd.ExecuteNonQueryAsync(ct); }
    private static async Task<object?> ScalarAsync(NpgsqlConnection c, NpgsqlTransaction tx, string sql, CancellationToken ct, params (string Name, object? Value)[] p)
    { await using var cmd = Command(c, tx, sql, p); return await cmd.ExecuteScalarAsync(ct); }
    private static Task<int> AccountLockAsync(NpgsqlConnection c, NpgsqlTransaction tx, Guid a, CancellationToken ct)
        => ExecAsync(c, tx, "select pg_advisory_xact_lock(hashtextextended(@a::text,20260918))", ct, ("a", a));
    private static bool IsPrimaryAccount(Guid id, string role, string? provider)
        => AccountEligibility.CanUseProtectedDownloads(new AccountProfile(id, role, false, [], null) { PrimaryAuthProvider = provider });
    private static async Task<Guid> CanonicalAsync(NpgsqlConnection c, NpgsqlTransaction tx, Guid a, CancellationToken ct)
    {
        var seen = new HashSet<Guid>();
        while (seen.Add(a) && seen.Count <= 16)
        {
            var next = await ScalarAsync(c, tx, "select merged_into from licensing.accounts where account_id=@a", ct, ("a", a));
            if (next is not Guid target) return a;
            a = target;
        }
        throw new PromotionConflictException();
    }
    private static async Task<bool> RequireAccountAsync(NpgsqlConnection c, NpgsqlTransaction tx, Guid a, bool allowGuest, CancellationToken ct)
    {
        await using var cmd = Command(c, tx, "select base_role,blocked_at,merged_into,primary_auth_provider,first_purchase_at from licensing.accounts where account_id=@a", ("a", a));
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct) || !r.IsDBNull(1) || !r.IsDBNull(2)) throw new PromotionUnavailableException("account_required");
        var primary = r.IsDBNull(3) ? null : r.GetString(3);
        // New primary signups legitimately retain the unpaid guest role.
        // Reuse the domain primary-account rule instead of requiring payment.
        var registered = IsPrimaryAccount(a, r.GetString(0), primary);
        if (!registered && !(allowGuest && primary == "telegram")) throw new PromotionUnavailableException("primary_account_required");
        return r.IsDBNull(4);
    }
    private static async Task<Guid?> ReferralAsync(NpgsqlConnection c, NpgsqlTransaction tx, Guid a, CancellationToken ct)
    {
        // A Telegram guest remains an ancestor after link/merge. This read
        // follows that ancestor without moving its historical financial data.
        await using var cmd = Command(c, tx, """
            with recursive ancestors as (
              select account_id,merged_into from licensing.accounts where account_id=@a
              union select x.account_id,x.merged_into from licensing.accounts x join ancestors p on x.merged_into=p.account_id)
            select distinct r.referrer_id from licensing.referrals r join ancestors x on r.referee_id=x.account_id
            """, ("a", a));
        var ids = new List<Guid>();
        await using (var r = await cmd.ExecuteReaderAsync(ct)) while (await r.ReadAsync(ct)) ids.Add(r.GetGuid(0));
        var canonical = new HashSet<Guid>();
        foreach (var id in ids) canonical.Add(await CanonicalAsync(c, tx, id, ct));
        if (canonical.Count > 1) throw new PromotionConflictException();
        return canonical.Count == 0 ? null : canonical.Single();
    }
    private const string PromoSelect = "select code,discount_bps,sku,currency,starts_at,ends_at,max_uses,per_account_limit,budget_minor,first_purchase_only,active,used,reserved,spent_minor,reserved_minor from licensing.promo_codes";
    private static PromoView PromoRow(NpgsqlDataReader r) => new(new(r.GetString(0), r.GetInt32(1), r.IsDBNull(2) ? null : r.GetString(2), r.GetString(3), r.GetFieldValue<DateTimeOffset>(4), r.GetFieldValue<DateTimeOffset>(5), r.GetInt32(6), r.GetInt32(7), r.GetInt64(8), r.GetBoolean(9), r.GetBoolean(10)), r.GetInt32(11), r.GetInt32(12), r.GetInt64(13), r.GetInt64(14));
    private static async Task<PromoView?> ReadPromoAsync(NpgsqlConnection c, NpgsqlTransaction tx, string code, CancellationToken ct)
    { await using var cmd = Command(c, tx, PromoSelect + " where code=@code for update", ("code", code)); await using var r = await cmd.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? PromoRow(r) : null; }
    private async Task CheckPromoAsync(NpgsqlConnection c, NpgsqlTransaction tx, Guid a, PromoView view, string sku, string currency, bool first, long discount, CancellationToken ct)
    {
        var d = view.Definition;
        var now = _clock.GetUtcNow();
        var count = Convert.ToInt64(await ScalarAsync(c, tx, "select count(*) from licensing.promotion_payments where account_id=@a and promo_code=@code and state <> 'canceled'", ct, ("a", a), ("code", d.Code)));
        if (!d.Active || d.StartsAt > now || d.EndsAt <= now || d.Currency != currency || d.Sku is not null && d.Sku != sku
            || d.FirstPurchaseOnly && !first || view.Used + view.Reserved >= d.MaxUses || count >= d.PerAccountLimit
            || discount > d.BudgetMinor - view.SpentMinor - view.ReservedMinor) throw new PromotionUnavailableException("promo_unavailable");
    }
}
