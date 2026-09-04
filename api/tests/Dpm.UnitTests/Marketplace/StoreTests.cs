using Dpm.Marketplace.Domain;
using Dpm.Marketplace.Domain.Events;
using FluentAssertions;

namespace Dpm.UnitTests.Marketplace;

public sealed class StoreTests
{
    private readonly TestClock _clock = new();

    private Store CreateStore() =>
        Store.Create(1, Guid.NewGuid(), "demo-studio", "Demo Studio", _clock).Value;

    [Fact]
    public void Creating_a_store_starts_it_active_and_unverified()
    {
        var store = CreateStore();

        store.Status.Should().Be(StoreStatus.Active);
        store.Profile.KycStatus.Should().Be(KycStatus.None);
        store.CanPublish.Should().BeFalse();
        store.DomainEvents.OfType<StoreCreated>().Should().ContainSingle();
    }

    [Theory]
    [InlineData("Demo-Studio", "demo-studio")]
    [InlineData("  DEMO  ", "demo")]
    public void Slugs_are_normalized_to_lowercase(string input, string expected)
    {
        Store.NormalizeSlug(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("-leading")]
    [InlineData("trailing-")]
    [InlineData("double--hyphen")]
    [InlineData("has space")]
    public void Malformed_slugs_are_rejected(string slug)
    {
        Store.Create(1, Guid.NewGuid(), slug, "Demo Studio", _clock)
            .IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Uppercase_slug_input_is_normalized_rather_than_rejected()
    {
        var store = Store.Create(1, Guid.NewGuid(), "Demo-Studio", "Demo Studio", _clock);

        store.IsSuccess.Should().BeTrue();
        store.Value.Slug.Should().Be("demo-studio");
    }

    [Fact]
    public void A_store_cannot_publish_until_kyc_tax_and_bank_are_all_complete()
    {
        var store = CreateStore();

        store.SubmitKyc("Demo Studio Private Limited", _clock);
        store.ApproveKyc(_clock);
        store.CanPublish.Should().BeFalse("tax and bank details are still missing");

        store.SetTaxInfo("GSTIN", "29ABCDE1234F1Z5", _clock);
        store.CanPublish.Should().BeFalse("the payout destination is not verified yet");

        store.SetBankInfo("provider-token-abc", _clock);
        store.CanPublish.Should().BeFalse("the provider has not confirmed the destination");

        store.VerifyBank(_clock);
        store.CanPublish.Should().BeTrue();
    }

    [Fact]
    public void Seller_verification_is_announced_once_onboarding_completes()
    {
        var store = CreateStore();
        store.SubmitKyc("Demo Studio Private Limited", _clock);
        store.SetTaxInfo("GSTIN", "29ABCDE1234F1Z5", _clock);
        store.SetBankInfo("provider-token-abc", _clock);
        store.ClearDomainEvents();

        store.ApproveKyc(_clock);
        store.DomainEvents.OfType<SellerVerified>().Should().BeEmpty("the bank is still unverified");

        store.VerifyBank(_clock);
        store.DomainEvents.OfType<SellerVerified>().Should().ContainSingle();
    }

    [Fact]
    public void Changing_the_payout_destination_clears_the_verified_flag()
    {
        var store = CreateStore();
        store.SubmitKyc("Demo Studio Private Limited", _clock);
        store.ApproveKyc(_clock);
        store.SetTaxInfo("GSTIN", "29ABCDE1234F1Z5", _clock);
        store.SetBankInfo("provider-token-abc", _clock);
        store.VerifyBank(_clock);
        store.CanPublish.Should().BeTrue();

        store.SetBankInfo("provider-token-xyz", _clock);

        store.Profile.BankVerified.Should().BeFalse();
        store.CanPublish.Should().BeFalse();
    }

    [Fact]
    public void A_suspended_store_cannot_publish_or_submit_verification()
    {
        var store = CreateStore();
        store.SubmitKyc("Demo Studio Private Limited", _clock);
        store.ApproveKyc(_clock);
        store.SetTaxInfo("GSTIN", "29ABCDE1234F1Z5", _clock);
        store.SetBankInfo("provider-token-abc", _clock);
        store.VerifyBank(_clock);

        store.Suspend("Policy violation.");

        store.CanPublish.Should().BeFalse();
        store.SubmitKyc("Demo Studio Private Limited", _clock).IsFailure.Should().BeTrue();
        store.DomainEvents.OfType<StoreSuspended>().Should().ContainSingle();
    }

    [Fact]
    public void Reinstating_restores_publish_eligibility()
    {
        var store = CreateStore();
        store.SubmitKyc("Demo Studio Private Limited", _clock);
        store.ApproveKyc(_clock);
        store.SetTaxInfo("GSTIN", "29ABCDE1234F1Z5", _clock);
        store.SetBankInfo("provider-token-abc", _clock);
        store.VerifyBank(_clock);
        store.Suspend("Policy violation.");

        store.Reinstate();

        store.Status.Should().Be(StoreStatus.Active);
        store.CanPublish.Should().BeTrue();
        store.DomainEvents.OfType<StoreReinstated>().Should().ContainSingle();
    }

    [Fact]
    public void A_rejected_verification_blocks_publishing()
    {
        var store = CreateStore();
        store.SubmitKyc("Demo Studio Private Limited", _clock);
        store.SetTaxInfo("GSTIN", "29ABCDE1234F1Z5", _clock);
        store.SetBankInfo("provider-token-abc", _clock);
        store.VerifyBank(_clock);

        store.RejectKyc(_clock);

        store.Profile.KycStatus.Should().Be(KycStatus.Rejected);
        store.CanPublish.Should().BeFalse();
    }
}
