using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PaymentService.Models;
using PaymentService.Options;
using Stripe;

namespace PaymentService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly StripeOptions _stripeOptions;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(IOptions<StripeOptions> stripeOptions, ILogger<PaymentsController> logger)
    {
        _stripeOptions = stripeOptions.Value;
        _logger = logger;
    }

    [HttpPost("intent")]
    [Authorize]
    public async Task<IActionResult> CreatePaymentIntent([FromBody] CreatePaymentIntentRequest request)
    {
        if (request == null)
        {
            return BadRequest("Request body cannot be empty.");
        }

        if (request.Amount <= 0)
        {
            return BadRequest("Amount must be greater than zero.");
        }

        var currency = string.IsNullOrWhiteSpace(request.Currency) ? "usd" : request.Currency.ToLowerInvariant();
        var amountInMinorUnit = ConvertToMinorUnits(request.Amount, currency);
        var userId = User.FindFirst("sub")?.Value ?? "anonymous";

        var createOptions = new PaymentIntentCreateOptions
        {
            Amount = amountInMinorUnit,
            Currency = currency,
            Description = string.IsNullOrWhiteSpace(request.Description)
                ? $"Book order {request.OrderId?.ToString() ?? "N/A"}"
                : request.Description,
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
            {
                Enabled = true
            },
            Metadata = new Dictionary<string, string>
            {
                ["userId"] = userId
            }
        };

        if (request.OrderId.HasValue)
        {
            createOptions.Metadata["orderId"] = request.OrderId.Value.ToString();
        }

        if (!string.IsNullOrWhiteSpace(request.ReceiptEmail))
        {
            createOptions.ReceiptEmail = request.ReceiptEmail;
        }

        if (!string.IsNullOrWhiteSpace(request.StatementDescriptor))
        {
            createOptions.StatementDescriptor = request.StatementDescriptor;
        }

        try
        {
            var service = new PaymentIntentService();
            var intent = await service.CreateAsync(createOptions);

            var response = new PaymentIntentResponse
            {
                PaymentIntentId = intent.Id,
                ClientSecret = intent.ClientSecret ?? string.Empty,
                Amount = intent.Amount ?? amountInMinorUnit,
                Currency = intent.Currency ?? currency,
                Status = intent.Status,
                PublishableKey = _stripeOptions.PublishableKey ?? string.Empty
            };

            _logger.LogInformation("Payment intent {IntentId} created for order {OrderId}", intent.Id, request.OrderId);
            return Ok(response);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe rejected payment intent for order {OrderId}", request.OrderId);
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                message = ex.StripeError?.Message ?? ex.Message
            });
        }
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> HandleWebhook()
    {
        var payload = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        try
        {
            Event stripeEvent;
            if (string.IsNullOrWhiteSpace(_stripeOptions.WebhookSecret))
            {
                stripeEvent = EventUtility.ParseEvent(payload);
            }
            else
            {
                var signatureHeader = Request.Headers["Stripe-Signature"];
                stripeEvent = EventUtility.ConstructEvent(payload, signatureHeader, _stripeOptions.WebhookSecret);
            }

            switch (stripeEvent.Type)
            {
                case Events.PaymentIntentSucceeded:
                    if (stripeEvent.Data.Object is PaymentIntent successIntent)
                    {
                        _logger.LogInformation("✅ Payment succeeded for intent {IntentId}", successIntent.Id);
                    }
                    break;
                case Events.PaymentIntentPaymentFailed:
                    if (stripeEvent.Data.Object is PaymentIntent failedIntent)
                    {
                        _logger.LogWarning("❌ Payment failed for intent {IntentId}: {Message}",
                            failedIntent.Id,
                            failedIntent.LastPaymentError?.Message ?? "Unknown error");
                    }
                    break;
                default:
                    _logger.LogInformation("Received unhandled Stripe event type: {EventType}", stripeEvent.Type);
                    break;
            }

            return Ok(new { received = true });
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe webhook signature validation failed.");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Stripe webhook payload.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Error processing webhook." });
        }
    }

    private static long ConvertToMinorUnits(decimal amount, string currency)
    {
        var normalized = currency switch
        {
            "jpy" or "krw" => Math.Round(amount, 0, MidpointRounding.AwayFromZero),
            _ => Math.Round(amount * 100, 0, MidpointRounding.AwayFromZero)
        };

        return (long)normalized;
    }
}
