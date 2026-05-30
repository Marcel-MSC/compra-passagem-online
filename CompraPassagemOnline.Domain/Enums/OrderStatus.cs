namespace CompraPassagemOnline.Domain.Enums;

public enum OrderStatus
{
    AwaitingPayment = 0,
    Paid = 1,
    Cancelled = 2,
    Refunding = 3
}
