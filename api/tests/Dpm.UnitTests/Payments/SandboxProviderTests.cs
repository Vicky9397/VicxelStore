using Dpm.BuildingBlocks.Domain;
using Dpm.Payments.Domain;
using Dpm.Payments.Infrastructure.Providers;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Dpm.UnitTests.Payments;

public sealed class SandboxProviderTests
{
    private readonly SandboxPaymentProvider _provider = new(
        Options.Create(new PaymentProviderOptions { SandboxWebhookSecret = "test-secret", SandboxFeePct = 2.5m }));

    private Dictionary<string, string> SignedHeaders(string payload) =>
        new(StringComparer.OrdinalIgnoreCase) { ["X-Sandbox-Signature"] = _provider.Sign(payload) };

    [Fact]
    public void A_correctly_signed_webhook_verifies_and_maps_to_the_canonical_event()
    {
        var payload = SandboxPaymentProvider.BuildSucceededPayload("sbx_intent_1", 1180m, "INR");

        var parsed = _provider.ParseWebhook(payload, SignedHeaders(payload));

        parsed.IsVerified.Should().BeTrue();
        parsed.Type.Should().Be(WebhookEventType.PaymentSucceeded);
        parsed.ProviderIntentId.Should().Be("sbx_intent_1");
        parsed.ProviderEventId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void A_webhook_with_no_signature_is_rejected()
    {
        var payload = SandboxPaymentProvider.BuildSucceededPayload("sbx_intent_1", 1180m, "INR");

        var parsed = _provider.ParseWebhook(payload, new Dictionary<string, string>());

        parsed.IsVerified.Should().BeFalse();
        parsed.ProviderEventId.Should().BeNull("nothing on an unverified payload may be trusted");
    }

    [Fact]
    public void A_webhook_with_a_wrong_signature_is_rejected()
    {
        var payload = SandboxPaymentProvider.BuildSucceededPayload("sbx_intent_1", 1180m, "INR");
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["X-Sandbox-Signature"] = new string('a', 64),
        };

        _provider.ParseWebhook(payload, headers).IsVerified.Should().BeFalse();
    }

    [Fact]
    public void A_tampered_body_no_longer_matches_its_signature()
    {
        var payload = SandboxPaymentProvider.BuildSucceededPayload("sbx_intent_1", 1180m, "INR");
        var headers = SignedHeaders(payload);
        var tampered = payload.Replace("1180", "1", StringComparison.Ordinal);

        _provider.ParseWebhook(tampered, headers).IsVerified.Should().BeFalse();
    }

    [Fact]
    public void A_malformed_body_is_rejected_rather_than_throwing()
    {
        const string payload = "not json at all";

        _provider.ParseWebhook(payload, SignedHeaders(payload)).IsVerified.Should().BeFalse();
    }

    [Fact]
    public void A_failure_webhook_maps_to_the_canonical_failure_event()
    {
        var payload = SandboxPaymentProvider.BuildFailedPayload("sbx_intent_1", "Card declined.");

        var parsed = _provider.ParseWebhook(payload, SignedHeaders(payload));

        parsed.IsVerified.Should().BeTrue();
        parsed.Type.Should().Be(WebhookEventType.PaymentFailed);
        parsed.FailureReason.Should().Be("Card declined.");
    }

    [Fact]
    public void The_processing_fee_follows_the_configured_rate()
    {
        _provider.CalculateFee(Money.Create(1000m, "INR").Value).Amount.Should().Be(25.00m);
    }
}

public sealed class ManualBankProviderTests
{
    private readonly ManualBankTransferProvider _provider = new(
        Options.Create(new PaymentProviderOptions()));

    [Fact]
    public async Task The_intent_returns_transfer_instructions_and_a_reference()
    {
        var intent = await _provider.CreateIntentAsync(
            new Dpm.Payments.Application.Abstractions.CreateIntent(
                Guid.NewGuid(), Money.Create(1180m, "INR").Value, null),
            CancellationToken.None);

        intent.Instructions.Should().Contain("1180.00");
        intent.Instructions.Should().Contain("reference");
        intent.ProviderIntentId.Should().StartWith("MB-");
    }

    [Fact]
    public void A_direct_transfer_carries_no_gateway_fee()
    {
        _provider.CalculateFee(Money.Create(1000m, "INR").Value).Amount.Should().Be(0m);
    }

    [Fact]
    public void A_bank_never_calls_back_so_nothing_verifies()
    {
        _provider.ParseWebhook("{}", new Dictionary<string, string>())
            .IsVerified.Should().BeFalse();
    }
}
