namespace Dpm.Payments.Domain;

public enum PaymentStatus : byte
{
    Created = 1,
    Captured = 2,
    Failed = 3,
    Refunded = 4,
    Disputed = 5,
}
