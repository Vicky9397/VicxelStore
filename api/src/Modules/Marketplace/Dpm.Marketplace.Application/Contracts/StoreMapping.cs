using Dpm.Marketplace.Domain;

namespace Dpm.Marketplace.Application.Contracts;

public static class StoreMapping
{
    public static StoreDto ToDto(this Store store) => new(
        store.PublicId,
        store.Slug,
        store.Name,
        store.About,
        store.LogoUrl,
        store.BannerUrl,
        store.Status.ToString());

    public static SellerProfileDto ToProfileDto(this Store store) => new(
        store.Profile.KycStatus.ToString(),
        store.Profile.LegalName,
        store.Profile.TaxIdType,
        store.Profile.TaxId,
        store.Profile.BankVerified,
        store.Profile.IsPublishReady);

    public static MyStoreDto ToMyStoreDto(this Store store) => new(store.ToDto(), store.ToProfileDto());
}
