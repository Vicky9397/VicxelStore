using Dpm.BuildingBlocks.Domain;
using Dpm.Catalog.Domain;
using FluentAssertions;

namespace Dpm.UnitTests.Catalog;

public sealed class ProductVariantTests
{
    private readonly TestClock _clock = new();

    private Product CreateDraft() =>
        Product.CreateDraft(1, 1, "Sci-fi UI Kit", "sci-fi-ui-kit", null, _clock).Value;

    private static Money Inr(decimal amount) => Money.Create(amount, "INR").Value;

    [Fact]
    public void A_variant_price_must_be_greater_than_zero()
    {
        var product = CreateDraft();

        product.AddVariant("Free", Inr(0m), LicenseType.Personal, 5, _clock)
            .IsFailure.Should().BeTrue();
    }

    [Fact]
    public void A_download_limit_below_one_is_rejected()
    {
        var product = CreateDraft();

        product.AddVariant("Personal", Inr(900m), LicenseType.Personal, 0, _clock)
            .IsFailure.Should().BeTrue();
    }

    [Fact]
    public void All_variants_of_a_product_must_share_one_currency()
    {
        var product = CreateDraft();
        product.AddVariant("Personal", Inr(900m), LicenseType.Personal, 5, _clock);

        var mixed = product.AddVariant(
            "Commercial", Money.Create(30m, "USD").Value, LicenseType.Commercial, 10, _clock);

        mixed.IsFailure.Should().BeTrue();
        mixed.Error.Message.Should().Contain("currency");
    }

    [Fact]
    public void Variants_carry_their_own_price_and_license_terms()
    {
        var product = CreateDraft();

        product.AddVariant("Personal", Inr(900m), LicenseType.Personal, 5, _clock);
        product.AddVariant("Commercial", Inr(2400m), LicenseType.Commercial, 10, _clock);

        product.Variants.Should().HaveCount(2);
        product.Variants[1].Price.Amount.Should().Be(2400m);
        product.Variants[1].LicenseType.Should().Be(LicenseType.Commercial);
        product.Variants[1].DownloadLimit.Should().Be(10);
    }

    [Fact]
    public void Updating_an_unknown_variant_reports_not_found()
    {
        var product = CreateDraft();

        var update = product.UpdateVariant(
            Guid.NewGuid(), "Personal", Inr(900m), LicenseType.Personal, 5, true, _clock);

        update.IsFailure.Should().BeTrue();
        update.Error.Code.Should().Be("NOT_FOUND");
    }

    [Fact]
    public void File_readiness_is_replaced_rather_than_accumulated()
    {
        var product = CreateDraft();
        var variant = product.AddVariant("Personal", Inr(900m), LicenseType.Personal, 5, _clock).Value;

        product.ApplyVariantFileReadiness(variant.PublicId, 2, _clock);
        product.ApplyVariantFileReadiness(variant.PublicId, 2, _clock);

        variant.CleanFileCount.Should().Be(2, "replaying the same event must converge, not double-count");
    }

    [Fact]
    public void A_version_number_cannot_be_reused()
    {
        var product = CreateDraft();
        product.ReleaseVersion("1.0.0", "Initial release.", _clock);

        product.ReleaseVersion("1.0.0", "Duplicate.", _clock).IsFailure.Should().BeTrue();
        product.ReleaseVersion("1.1.0", "Adds dark mode.", _clock).IsSuccess.Should().BeTrue();
        product.Versions.Should().HaveCount(2);
    }
}
