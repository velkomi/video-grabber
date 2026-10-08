namespace VideoGrabber.Platform.Contracts;

public sealed record PromotionQuoteRequest(string Sku, string Provider, string? PromoCode, bool UseBonuses, bool Recurring = false);
public sealed record PromotionQuote(Guid QuoteId, Money Original, Money Discount, Money Bonus, Money Payable, DateTimeOffset ExpiresAt, string? PromoCode, bool ReferralApplied);
public sealed record ReferralClaimRequest(string Code);
public sealed record BonusBalance(string Currency, long AvailableMinor, long PendingMinor, long ReservedMinor, long DebtMinor);
public sealed record BonusHistoryItem(string Kind, Money Amount, DateTimeOffset CreatedAt, DateTimeOffset? AvailableAt, DateTimeOffset? ExpiresAt);
public sealed record ReferralSummary(string Code, Uri WebLink, Uri TelegramLink, int Invited, int Paid, BonusBalance[] Balances, BonusHistoryItem[] History);
public sealed record PromoDefinition(string Code, int DiscountBasisPoints, string? Sku, string Currency, DateTimeOffset StartsAt, DateTimeOffset EndsAt, int MaxUses, int PerAccountLimit, long BudgetMinor, bool FirstPurchaseOnly = true, bool Active = true);
public sealed record PromoView(PromoDefinition Definition, int Used, int Reserved, long SpentMinor, long ReservedMinor);
public sealed record PromotionOptions(bool Enabled = false, string PublicOrigin = "https://videograbber.srv1902378.hstgr.cloud", string BotUsername = "VideoGra_bot");
