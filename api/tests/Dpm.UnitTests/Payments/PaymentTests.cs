using Dpm.BuildingBlocks.Domain;
using Dpm.BuildingBlocks.IntegrationEvents;
using Dpm.Payments.Domain;
using FluentAssertions;

namespace Dpm.UnitTests.Payments;

public sealed class PaymentTests
{
    private readonly TestClock _clock = new();

    private static Money Inr(decimal amount) => Money.Create(amount, "INR").Value;

    private Payment CreatePayment() =>
        Payment.Create(1, "sandbox", "sbx_intent_1", Inr(1180m), _clock);

    [Fact]
    public void A_new_payment_starts_created_and_announces_nothing()
    {
        var payment = CreatePayment();

        payment.Status.Should().Be(PaymentStatus.Created);
        payment.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Capture_announces_the_amount_and_the_gateway_fee()
    {
        var payment = CreatePayment();

        var captured = payment.Capture("sbx_charge_1", Inr(25m));

        captured.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Captured);
        payment.ProviderChargeId.Should().Be("sbx_charge_1");

        var announced = payment.DomainEvents.OfType<PaymentCaptured>().Should().ContainSingle().Subject;
        announced.OrderId.Should().Be(1);
        announced.Amount.Should().Be(1180m);
        announced.GatewayFeeAmount.Should().Be(25m);
    }

    [Fact]
    public void A_redelivered_capture_does_not_announce_twice()
    {
        var payment = CreatePayment();
        payment.Capture("sbx_charge_1", Inr(25m));
        payment.ClearDomainEvents();

        var replay = payment.Capture("sbx_charge_1", Inr(25m));

        replay.IsSuccess.Should().BeTrue();
        payment.DomainEvents.Should().BeEmpty("a second ledger post would double-credit the seller");
    }

    [Fact]
    public void A_failed_payment_cannot_later_be_captured()
    {
        var payment = CreatePayment();
        payment.Fail("Card declined.");

        payment.Capture("sbx_charge_1", Inr(25m)).IsFailure.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Failed);
    }

    [Fact]
    public void A_captured_payment_cannot_be_marked_failed()
    {
        var payment = CreatePayment();
        payment.Capture("sbx_charge_1", Inr(25m));

        payment.Fail("Late decline.").IsFailure.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Captured);
    }

    [Fact]
    public void A_fee_in_another_currency_is_refused()
    {
        var payment = CreatePayment();

        var captured = payment.Capture("sbx_charge_1", Money.Create(25m, "USD").Value);

        captured.IsFailure.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Created);
    }
}
