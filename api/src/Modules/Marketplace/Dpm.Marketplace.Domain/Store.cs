using System.Text.RegularExpressions;
using Dpm.BuildingBlocks.Application;
using Dpm.BuildingBlocks.Domain;
using Dpm.Marketplace.Domain.Events;

namespace Dpm.Marketplace.Domain;

/// <summary>
/// Seller storefront aggregate (market.Stores). Owns its SellerProfile; a user
/// may own exactly one store.
/// </summary>
public sealed partial class Store : AggregateRoot
{
    public const int SlugMinLength = 3;
    public const int SlugMaxLength = 80;

    public long OwnerUserId { get; private set; }

    public string Slug { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? About { get; private set; }

    public string? LogoUrl { get; private set; }

    public string? BannerUrl { get; private set; }

    public string? ThemeJson { get; private set; }

    public StoreStatus Status { get; private set; } = StoreStatus.Active;

    public DateTime CreatedAtUtc { get; private set; }

    public SellerProfile Profile { get; private set; } = null!;

    private Store()
    {
    }

    public static Result<Store> Create(
        long ownerUserId,
        Guid ownerUserPublicId,
        string slug,
        string name,
        IClock clock)
    {
        var normalizedSlug = NormalizeSlug(slug);
        if (!IsValidSlug(normalizedSlug))
        {
            return Result.Failure<Store>(Error.Validation(
                "VALIDATION_ERROR",
                "Slug must be 3 to 80 characters of lowercase letters, digits and single hyphens."));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<Store>(Error.Validation("VALIDATION_ERROR", "Store name is required."));
        }

        var store = new Store
        {
            PublicId = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            Slug = normalizedSlug,
            Name = name.Trim(),
            CreatedAtUtc = clock.UtcNow,
            Profile = SellerProfile.Create(clock),
        };

        store.Raise(new StoreCreated(store.PublicId, ownerUserPublicId, store.Slug));
        return Result.Success(store);
    }

    public static string NormalizeSlug(string slug) => slug.Trim().ToLowerInvariant();

    public static bool IsValidSlug(string slug) =>
        slug.Length >= SlugMinLength && slug.Length <= SlugMaxLength && SlugPattern().IsMatch(slug);

    public Result UpdateDetails(string name, string? about)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(Error.Validation("VALIDATION_ERROR", "Store name is required."));
        }

        Name = name.Trim();
        About = about?.Trim();
        return Result.Success();
    }

    public void UpdateBranding(string? logoUrl, string? bannerUrl, string? themeJson)
    {
        LogoUrl = logoUrl;
        BannerUrl = bannerUrl;
        ThemeJson = themeJson;
    }

    public Result SubmitKyc(string legalName, IClock clock)
    {
        if (Status == StoreStatus.Suspended)
        {
            return Result.Failure(Error.Locked("ACCOUNT_LOCKED", "A suspended store cannot submit verification."));
        }

        Profile.SubmitKyc(legalName, clock);
        return Result.Success();
    }

    public void SetTaxInfo(string taxIdType, string taxId, IClock clock) =>
        Profile.SetTaxInfo(taxIdType, taxId, clock);

    public void SetBankInfo(string bankRef, IClock clock) => Profile.SetBankInfo(bankRef, clock);

    public void ApproveKyc(IClock clock)
    {
        Profile.MarkKycVerified(clock);
        if (Profile.IsPublishReady)
        {
            Raise(new SellerVerified(PublicId));
        }
    }

    public void RejectKyc(IClock clock) => Profile.MarkKycRejected(clock);

    public void VerifyBank(IClock clock)
    {
        Profile.MarkBankVerified(clock);
        if (Profile.IsPublishReady)
        {
            Raise(new SellerVerified(PublicId));
        }
    }

    public Result Suspend(string reason)
    {
        if (Status == StoreStatus.Suspended)
        {
            return Result.Success();
        }

        Status = StoreStatus.Suspended;
        Raise(new StoreSuspended(PublicId, reason));
        return Result.Success();
    }

    public Result Reinstate()
    {
        if (Status != StoreStatus.Suspended)
        {
            return Result.Success();
        }

        Status = StoreStatus.Active;
        Raise(new StoreReinstated(PublicId));
        return Result.Success();
    }

    public bool IsActive => Status == StoreStatus.Active;

    /// <summary>
    /// A store may publish only while active and once seller onboarding is
    /// complete (spec 02 section 2.4 Seller Management).
    /// </summary>
    public bool CanPublish => IsActive && Profile.IsPublishReady;

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();
}
