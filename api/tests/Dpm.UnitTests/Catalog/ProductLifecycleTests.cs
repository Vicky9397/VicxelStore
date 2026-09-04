using Dpm.BuildingBlocks.Domain;
using Dpm.Catalog.Domain;
using Dpm.Catalog.Domain.Events;
using FluentAssertions;

namespace Dpm.UnitTests.Catalog;

public sealed class ProductLifecycleTests
{
    private readonly TestClock _clock = new();
    private readonly Guid _storePublicId = Guid.NewGuid();

    private Product CreateDraft() =>
        Product.CreateDraft(1, 1, "Sci-fi UI Kit", "sci-fi-ui-kit", "A dark interface kit.", _clock).Value;

    private static Money Price(decimal amount = 900m) => Money.Create(amount, "INR").Value;

    private Product DraftWithDeliverableVariant()
    {
        var product = CreateDraft();
        var variant = product.AddVariant("Personal", Price(), LicenseType.Personal, 5, _clock).Value;
        product.ApplyVariantFileReadiness(variant.PublicId, cleanFileCount: 1, _clock);
        return product;
    }

    private Product ApprovedProduct()
    {
        var product = DraftWithDeliverableVariant();
        product.SubmitForReview(_storePublicId, _clock);
        product.Approve(_clock);
        return product;
    }

    [Fact]
    public void A_new_product_starts_as_a_draft()
    {
        var product = CreateDraft();

        product.Status.Should().Be(ProductStatus.Draft);
        product.PublicId.Should().NotBe(Guid.Empty);
        product.IsPubliclyVisible.Should().BeFalse();
    }

    [Fact]
    public void A_product_without_variants_cannot_be_submitted()
    {
        var product = CreateDraft();

        var submit = product.SubmitForReview(_storePublicId, _clock);

        submit.IsFailure.Should().BeTrue();
        submit.Error.Message.Should().Contain("variant");
        product.Status.Should().Be(ProductStatus.Draft);
    }

    [Fact]
    public void A_variant_without_a_scanned_file_blocks_submission()
    {
        var product = CreateDraft();
        product.AddVariant("Personal", Price(), LicenseType.Personal, 5, _clock);

        var submit = product.SubmitForReview(_storePublicId, _clock);

        submit.IsFailure.Should().BeTrue();
        submit.Error.Message.Should().Contain("virus scan");
    }

    [Fact]
    public void A_variant_with_a_clean_file_can_be_submitted()
    {
        var product = DraftWithDeliverableVariant();

        var submit = product.SubmitForReview(_storePublicId, _clock);

        submit.IsSuccess.Should().BeTrue();
        product.Status.Should().Be(ProductStatus.Submitted);
        product.DomainEvents.OfType<ProductSubmitted>().Should().ContainSingle();
    }

    [Fact]
    public void An_inactive_variant_does_not_satisfy_the_deliverability_invariant()
    {
        var product = CreateDraft();
        var variant = product.AddVariant("Personal", Price(), LicenseType.Personal, 5, _clock).Value;
        product.ApplyVariantFileReadiness(variant.PublicId, cleanFileCount: 1, _clock);
        product.UpdateVariant(
            variant.PublicId, "Personal", Price(), LicenseType.Personal, 5, isActive: false, _clock);

        product.CheckPublishReadiness().IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Publishing_requires_moderator_approval_first()
    {
        var product = DraftWithDeliverableVariant();
        product.SubmitForReview(_storePublicId, _clock);

        var publish = product.Publish(storeCanPublish: true, _clock);

        publish.IsFailure.Should().BeTrue();
        publish.Error.Code.Should().Be("CONFLICT");
    }

    [Fact]
    public void Publishing_requires_a_store_that_has_completed_onboarding()
    {
        var product = ApprovedProduct();

        var publish = product.Publish(storeCanPublish: false, _clock);

        publish.IsFailure.Should().BeTrue();
        publish.Error.Code.Should().Be("KYC_REQUIRED");
        product.Status.Should().Be(ProductStatus.Approved);
    }

    [Fact]
    public void An_approved_product_with_a_ready_store_publishes()
    {
        var product = ApprovedProduct();

        var publish = product.Publish(storeCanPublish: true, _clock);

        publish.IsSuccess.Should().BeTrue();
        product.Status.Should().Be(ProductStatus.Published);
        product.PublishedAtUtc.Should().Be(_clock.UtcNow);
        product.IsPubliclyVisible.Should().BeTrue();
        product.DomainEvents.OfType<ProductPublished>().Should().ContainSingle();
    }

    [Fact]
    public void Losing_the_only_clean_file_blocks_a_later_publish()
    {
        var product = DraftWithDeliverableVariant();
        product.SubmitForReview(_storePublicId, _clock);
        product.Approve(_clock);

        // The file was quarantined after approval, so the product is no longer deliverable.
        product.ApplyVariantFileReadiness(product.Variants[0].PublicId, cleanFileCount: 0, _clock);

        product.Publish(storeCanPublish: true, _clock).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Rejection_requires_a_reason_and_returns_the_product_to_the_seller()
    {
        var product = DraftWithDeliverableVariant();
        product.SubmitForReview(_storePublicId, _clock);

        product.Reject("   ", _clock).IsFailure.Should().BeTrue();

        var reject = product.Reject("Screenshots do not match the files.", _clock);

        reject.IsSuccess.Should().BeTrue();
        product.Status.Should().Be(ProductStatus.Rejected);
        product.DomainEvents.OfType<ProductRejected>().Should().ContainSingle()
            .Which.Reason.Should().Be("Screenshots do not match the files.");
    }

    [Fact]
    public void A_rejected_product_can_be_fixed_and_resubmitted()
    {
        var product = DraftWithDeliverableVariant();
        product.SubmitForReview(_storePublicId, _clock);
        product.Reject("Screenshots do not match the files.", _clock);

        var resubmit = product.SubmitForReview(_storePublicId, _clock);

        resubmit.IsSuccess.Should().BeTrue();
        product.Status.Should().Be(ProductStatus.Submitted);
    }

    [Fact]
    public void A_published_product_cannot_be_submitted_or_approved_again()
    {
        var product = ApprovedProduct();
        product.Publish(storeCanPublish: true, _clock);

        product.SubmitForReview(_storePublicId, _clock).IsFailure.Should().BeTrue();
        product.Approve(_clock).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Unpublishing_returns_the_product_to_approved_and_hides_it()
    {
        var product = ApprovedProduct();
        product.Publish(storeCanPublish: true, _clock);

        var unpublish = product.Unpublish(_clock);

        unpublish.IsSuccess.Should().BeTrue();
        product.Status.Should().Be(ProductStatus.Approved);
        product.IsPubliclyVisible.Should().BeFalse();
        product.DomainEvents.OfType<ProductUnpublished>().Should().ContainSingle();
    }

    [Fact]
    public void Scheduling_rejects_a_time_in_the_past()
    {
        var product = ApprovedProduct();

        product.Schedule(_clock.UtcNow.AddMinutes(-1), _clock).IsFailure.Should().BeTrue();
        product.Schedule(_clock.UtcNow.AddDays(1), _clock).IsSuccess.Should().BeTrue();
        product.ScheduledPublishUtc.Should().Be(_clock.UtcNow.AddDays(1));
    }

    [Fact]
    public void An_archived_product_cannot_be_edited()
    {
        var product = DraftWithDeliverableVariant();
        product.Archive(_clock);

        product.UpdateDetails("New title", null, 1, null, null, _clock).IsFailure.Should().BeTrue();
        product.AddVariant("Commercial", Price(2400m), LicenseType.Commercial, 10, _clock)
            .IsFailure.Should().BeTrue();
    }
}
