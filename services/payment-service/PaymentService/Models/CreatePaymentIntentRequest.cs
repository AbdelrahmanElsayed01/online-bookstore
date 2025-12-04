namespace PaymentService.Models;

public class CreatePaymentIntentRequest
{
    public decimal Amount { get; set; }
    public string? Currency { get; set; } = "usd";
    public Guid? OrderId { get; set; }
    public string? Description { get; set; }
    public string? ReceiptEmail { get; set; }
    public string? StatementDescriptor { get; set; }
}
