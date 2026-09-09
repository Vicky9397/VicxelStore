namespace Dpm.Orders.Domain;

public enum OrderStatus : byte
{
    Pending = 1,
    Paid = 2,
    Completed = 3,
    Refunded = 4,
    Disputed = 5,
    Cancelled = 6,
}
