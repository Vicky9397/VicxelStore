namespace Dpm.Marketplace.Application.Contracts;

public sealed record StoreDto(
    Guid Id,
    string Slug,
    string Name,
    string? About,
    string? LogoUrl,
    string? BannerUrl,
    string Status);

public sealed record SellerProfileDto(
    string KycStatus,
    string? LegalName,
    string? TaxIdType,
    string? TaxId,
    bool BankVerified,
    bool IsPublishReady);

public sealed record MyStoreDto(StoreDto Store, SellerProfileDto Profile);
