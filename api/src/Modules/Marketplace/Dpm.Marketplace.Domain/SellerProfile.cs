using Dpm.BuildingBlocks.Domain;

namespace Dpm.Marketplace.Domain;

/// <summary>
/// Verification, tax and payout setup for a store (market.SellerProfiles).
/// Part of the Store aggregate: it is never loaded or changed on its own.
/// </summary>
public sealed class SellerProfile
{
    public long StoreId { get; private set; }

    public KycStatus KycStatus { get; private set; } = KycStatus.None;

    public string? LegalName { get; private set; }

    public string? TaxIdType { get; private set; }

    public string? TaxId { get; private set; }

    public bool BankVerified { get; private set; }

    /// <summary>Provider token for the payout destination. Raw account details are never stored.</summary>
    public string? BankRef { get; private set; }

    public decimal? CommissionOverridePct { get; private set; }

    public bool PayoutMinorityFrozen { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    private SellerProfile()
    {
    }

    internal static SellerProfile Create(IClock clock) => new() { UpdatedAtUtc = clock.UtcNow };

    internal void SubmitKyc(string legalName, IClock clock)
    {
        LegalName = Guard.AgainstNullOrWhiteSpace(legalName, nameof(legalName)).Trim();
        KycStatus = KycStatus.Submitted;
        UpdatedAtUtc = clock.UtcNow;
    }

    internal void MarkKycVerified(IClock clock)
    {
        KycStatus = KycStatus.Verified;
        UpdatedAtUtc = clock.UtcNow;
    }

    internal void MarkKycRejected(IClock clock)
    {
        KycStatus = KycStatus.Rejected;
        UpdatedAtUtc = clock.UtcNow;
    }

    internal void SetTaxInfo(string taxIdType, string taxId, IClock clock)
    {
        TaxIdType = Guard.AgainstNullOrWhiteSpace(taxIdType, nameof(taxIdType)).Trim();
        TaxId = Guard.AgainstNullOrWhiteSpace(taxId, nameof(taxId)).Trim();
        UpdatedAtUtc = clock.UtcNow;
    }

    internal void SetBankInfo(string bankRef, IClock clock)
    {
        BankRef = Guard.AgainstNullOrWhiteSpace(bankRef, nameof(bankRef)).Trim();
        // Verification is asserted by the payment provider, never by the seller.
        BankVerified = false;
        UpdatedAtUtc = clock.UtcNow;
    }

    internal void MarkBankVerified(IClock clock)
    {
        BankVerified = true;
        UpdatedAtUtc = clock.UtcNow;
    }

    public bool HasTaxInfo => !string.IsNullOrWhiteSpace(TaxId) && !string.IsNullOrWhiteSpace(TaxIdType);

    /// <summary>
    /// Seller onboarding is complete enough to publish and to be paid: identity
    /// verified, tax registration on file, payout destination verified.
    /// </summary>
    public bool IsPublishReady => KycStatus == KycStatus.Verified && BankVerified && HasTaxInfo;
}
