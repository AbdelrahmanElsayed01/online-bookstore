namespace PaymentService.Options;

public class StripeOptions
{
    public string? SecretKey { get; set; }
    public string? PublishableKey { get; set; }
    public string? WebhookSecret { get; set; }
}
