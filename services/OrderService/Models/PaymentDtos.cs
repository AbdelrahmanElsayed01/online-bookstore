namespace OrderService.Models;

public class PaymentIntentRequestDto
{
    public decimal Amount { get; set; }
}

public class PaymentIntentResponseDto
{
    public string PaymentIntentId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
