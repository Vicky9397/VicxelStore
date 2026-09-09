namespace Dpm.Orders.Application.Contracts;

public sealed record MoneyDto(decimal Amount, string Currency);

public sealed record CartItemDto(
    Guid VariantId,
    string ProductSlug,
    string ProductTitle,
    string VariantName,
    MoneyDto Price,
    bool SavedForLater,
    bool IsAvailable);

public sealed record CartDto(
    Guid Id,
    IReadOnlyList<CartItemDto> Items,
    MoneyDto Subtotal,
    int ItemCount);

public sealed record TotalsDto(
    decimal Subtotal,
    decimal Discount,
    decimal Tax,
    decimal GrandTotal,
    string Currency);

public sealed record QuoteDto(TotalsDto Totals, IReadOnlyList<CartItemDto> Items);

public sealed record PaymentIntentDto(string Provider, string ClientSecret, string? Instructions);

public sealed record CheckoutResultDto(
    Guid OrderPublicId,
    PaymentIntentDto PaymentIntent,
    TotalsDto Totals);

public sealed record LicenseFileDto(Guid Id, string FileName, long SizeBytes);

public sealed record LicenseDto(
    Guid Id,
    string ProductTitle,
    string VariantName,
    int DownloadLimit,
    int DownloadsUsed,
    DateTime IssuedAtUtc,
    IReadOnlyList<LicenseFileDto> Files);

public sealed record OrderLineDto(
    string ProductTitle,
    string VariantName,
    MoneyDto UnitPrice);

public sealed record OrderDto(
    Guid Id,
    string Status,
    string? InvoiceNo,
    TotalsDto Totals,
    DateTime PlacedAtUtc,
    IReadOnlyList<OrderLineDto> Lines,
    IReadOnlyList<LicenseDto> Licenses);
