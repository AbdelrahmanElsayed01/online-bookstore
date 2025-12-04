using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderService.Models;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace OrderService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly HttpClient _http;
        private readonly HttpClient _catalogClient;
        private readonly HttpClient _paymentClient;
        private readonly string _ordersEndpoint;
        private readonly string _anonKey;
        private readonly string _serviceKey;
        private readonly string _paymentEndpoint;
        private readonly ILogger<OrdersController> _logger;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public OrdersController(IConfiguration config, IHttpClientFactory httpClientFactory, ILogger<OrdersController> logger)
        {
            _logger = logger;

            var baseUrl = config["ORDERS_SUPABASE_URL"] ?? throw new Exception("ORDERS_SUPABASE_URL missing");
            _anonKey = config["ORDERS_SUPABASE_KEY"] ?? throw new Exception("ORDERS_SUPABASE_KEY missing");
            _serviceKey = config["ORDERS_SUPABASE_SERVICE_KEY"] ?? throw new Exception("ORDERS_SUPABASE_SERVICE_KEY missing");
            _ordersEndpoint = baseUrl.TrimEnd('/') + "/rest/v1/orders";

            _http = new HttpClient();

            var catalogBase = config["CATALOG_BASE_URL"] ?? "http://catalog-service:8080/";
            _catalogClient = httpClientFactory.CreateClient("CatalogService");
            if (_catalogClient.BaseAddress == null)
                _catalogClient.BaseAddress = new Uri(catalogBase);

            var paymentBase = config["PAYMENT_BASE_URL"] ?? "http://payment-service:8080/";
            _paymentEndpoint = paymentBase.TrimEnd('/') + "/api/payments/intent";
            _paymentClient = httpClientFactory.CreateClient("PaymentService");
            if (_paymentClient.BaseAddress == null)
                _paymentClient.BaseAddress = new Uri(paymentBase);
        }

        private async Task<(bool Success, HttpStatusCode StatusCode, string? Message)> ProcessPaymentAsync(Order createdOrder, string? bearerToken)
        {
            var paymentPayload = new PaymentIntentRequestDto
            {
                Amount = createdOrder.amount
            };
            var json = JsonSerializer.Serialize(paymentPayload);

            using var paymentRequest = new HttpRequestMessage(HttpMethod.Post, _paymentEndpoint)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            if (!string.IsNullOrWhiteSpace(bearerToken))
            {
                paymentRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            }

            try
            {
                var response = await _paymentClient.SendAsync(paymentRequest);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Payment service responded with {Status} for order {OrderId}: {Body}",
                        response.StatusCode, createdOrder.id, body);
                    return (false, response.StatusCode, body);
                }

                PaymentIntentResponseDto? paymentResponse = null;
                try
                {
                    paymentResponse = JsonSerializer.Deserialize<PaymentIntentResponseDto>(body, _jsonOptions);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Unable to parse payment service response for order {OrderId}", createdOrder.id);
                }

                if (paymentResponse != null &&
                    !string.Equals(paymentResponse.Status, "succeeded", StringComparison.OrdinalIgnoreCase))
                {
                    var message = $"Payment status was {paymentResponse.Status ?? "unknown"}.";
                    _logger.LogWarning("Payment for order {OrderId} returned non-success status: {Status}",
                        createdOrder.id, paymentResponse.Status);
                    return (false, HttpStatusCode.BadRequest, message);
                }

                _logger.LogInformation("Payment succeeded for order {OrderId}", createdOrder.id);
                return (true, HttpStatusCode.OK, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Payment service call failed for order {OrderId}", createdOrder.id);
                return (false, HttpStatusCode.BadGateway, "Payment service unavailable.");
            }
        }

        private async Task UpdateOrderStatusAsync(long orderId, string status)
        {
            var payload = new { status };
            var json = JsonSerializer.Serialize(payload);

            using var request = new HttpRequestMessage(HttpMethod.Patch, $"{_ordersEndpoint}?id=eq.{orderId}")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            request.Headers.Add("apikey", _serviceKey);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _serviceKey);
            request.Headers.Add("Prefer", "return=representation");

            var response = await _http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to update order {OrderId} status to {Status}. StatusCode: {Code}. Body: {Body}",
                    orderId, status, response.StatusCode, body);
            }
            else
            {
                _logger.LogInformation("Order {OrderId} status updated to {Status}.", orderId, status);
            }
        }

        private async Task ReleaseReservedStockAsync(IEnumerable<(Guid bookId, int quantity)> reservations)
        {
            foreach (var (bookId, quantity) in reservations)
            {
                try
                {
                    var payload = JsonSerializer.Serialize(new { quantity });
                    var content = new StringContent(payload, Encoding.UTF8, "application/json");
                    var releaseResp = await _catalogClient.PostAsync($"/api/books/{bookId}/release", content);
                    var releaseBody = await releaseResp.Content.ReadAsStringAsync();
                    if (!releaseResp.IsSuccessStatusCode)
                    {
                        _logger.LogError("Compensation (release stock) failed for book {BookId}. Status: {Status}. Body: {Body}",
                            bookId, releaseResp.StatusCode, releaseBody);
                    }
                    else
                    {
                        _logger.LogInformation("Compensation successful: released {Quantity} unit(s) for book {BookId}.",
                            quantity, bookId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception during stock release compensation for book {BookId}.", bookId);
                }
            }
        }

        private static string? ExtractBearerToken(string? header)
        {
            if (string.IsNullOrWhiteSpace(header))
                return null;

            return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? header.Substring(7)
                : header;
        }

        private async Task<(bool Success, HttpStatusCode StatusCode, string? Message)> ReserveBookStockAsync(Guid bookId, int quantity)
        {
            if (quantity <= 0)
                return (false, HttpStatusCode.BadRequest, "Quantity must be greater than zero.");

            var payload = JsonSerializer.Serialize(new { quantity });
            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            var response = await _catalogClient.PostAsync($"/api/books/{bookId}/reserve", content);
            var body = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
                return (true, HttpStatusCode.OK, null);

            return (false, response.StatusCode, body);
        }

        private static List<OrderItemRequest> NormalizeItems(CreateOrderRequest request)
        {
            var items = new List<OrderItemRequest>();

            if (request.items != null && request.items.Count > 0)
            {
                foreach (var item in request.items)
                {
                    if (item == null)
                        continue;

                    items.Add(new OrderItemRequest
                    {
                        book_id = item.book_id,
                        quantity = item.quantity <= 0 ? 1 : item.quantity
                    });
                }
            }
            else if (request.book_id.HasValue && request.book_id != Guid.Empty)
            {
                items.Add(new OrderItemRequest
                {
                    book_id = request.book_id.Value,
                    quantity = 1
                });
            }

            return items;
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAll()
        {
            var req = new HttpRequestMessage(HttpMethod.Get, _ordersEndpoint);
            req.Headers.Add("apikey", _anonKey);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _anonKey);

            var resp = await _http.SendAsync(req);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            var orders = JsonSerializer.Deserialize<List<Order>>(json, _jsonOptions);

            return Ok(orders);
        }

        [HttpGet("{id:long}")]
        [Authorize]
        public async Task<IActionResult> GetById(long id)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, $"{_ordersEndpoint}?id=eq.{id}");
            req.Headers.Add("apikey", _anonKey);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _anonKey);

            var resp = await _http.SendAsync(req);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();

            var items = JsonSerializer.Deserialize<List<Order>>(json, _jsonOptions);

            var order = (items != null && items.Count > 0) ? items[0] : null;
            return order == null ? NotFound() : Ok(order);
        }

        // ---------- SAGA ORCHESTRATION: RESERVE STOCK + CREATE ORDER ----------

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create([FromBody] CreateOrderRequest request)
        {
            var normalizedItems = NormalizeItems(request);
            if (normalizedItems.Count == 0)
                return BadRequest("Provide at least one book with a quantity.");

            if (normalizedItems.Any(i => i.book_id == Guid.Empty))
                return BadRequest("Each item must include a valid book_id.");

            // Get user id from JWT (Supabase "sub")
            var userIdClaim = User.FindFirst("sub")?.Value;
            if (string.IsNullOrWhiteSpace(userIdClaim))
                return Unauthorized("User id (sub) not found in token.");

            if (!Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized("User id (sub) is not a valid GUID.");

            // Forward token to CatalogService
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            var bearerToken = ExtractBearerToken(authHeader);
            if (!string.IsNullOrWhiteSpace(bearerToken))
            {
                _catalogClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", bearerToken);
            }

            // Saga Step 1: Reserve stock for each item
            var reservations = new List<(Guid bookId, int quantity)>();
            foreach (var item in normalizedItems)
            {
                _logger.LogInformation("Reserving {Quantity} unit(s) for book {BookId}", item.quantity, item.book_id);

                var reserveResult = await ReserveBookStockAsync(item.book_id, item.quantity);
                if (!reserveResult.Success)
                {
                    _logger.LogWarning("Failed to reserve stock for book {BookId}: {Message}", item.book_id, reserveResult.Message);
                    await ReleaseReservedStockAsync(reservations);
                    return StatusCode((int)reserveResult.StatusCode,
                        reserveResult.Message ?? "Unable to reserve stock.");
                }

                reservations.Add((item.book_id, item.quantity));
            }

            _logger.LogInformation("Stock reserved for {Count} item(s). Proceeding to create order.", normalizedItems.Count);

            var totalQuantity = normalizedItems.Sum(i => i.quantity);
            var totalAmount = Math.Round(totalQuantity * 5.00m, 2, MidpointRounding.AwayFromZero);

            // Saga Step 2: Insert order into Orders DB (Supabase)
            var insertDto = new InsertOrderDto
            {
                user_id = userId,
                book_id = normalizedItems.First().book_id,
                amount = totalAmount,
                status = "pending",
                created_at = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(insertDto);
            var orderReq = new HttpRequestMessage(HttpMethod.Post, _ordersEndpoint)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            orderReq.Headers.Add("apikey", _serviceKey);
            orderReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _serviceKey);
            orderReq.Headers.Add("Prefer", "return=representation");

            var orderResp = await _http.SendAsync(orderReq);
            var orderBody = await orderResp.Content.ReadAsStringAsync();

            if (!orderResp.IsSuccessStatusCode)
            {
                _logger.LogError("Order insert failed. Status: {Status}. Body: {Body}. Triggering compensation.",
                    orderResp.StatusCode, orderBody);

                await ReleaseReservedStockAsync(reservations);

                return StatusCode((int)orderResp.StatusCode, orderBody);
            }

            var createdList = JsonSerializer.Deserialize<List<Order>>(orderBody, _jsonOptions);
            var created = createdList!.First();

            _logger.LogInformation("Order {OrderId} created. Initiating payment of {Amount:C}.", created.id, totalAmount);

            var paymentResult = await ProcessPaymentAsync(created, bearerToken);
            if (!paymentResult.Success)
            {
                _logger.LogWarning("Payment for order {OrderId} failed: {Message}", created.id, paymentResult.Message);
                await UpdateOrderStatusAsync(created.id, "failed");
                await ReleaseReservedStockAsync(reservations);
                return StatusCode((int)paymentResult.StatusCode, paymentResult.Message ?? "Payment failed.");
            }

            created.status = "successful";
            created.amount = totalAmount;
            await UpdateOrderStatusAsync(created.id, created.status);

            var response = new OrderResponse
            {
                order = created,
                items = normalizedItems.Select(i => new OrderItemSummary
                {
                    book_id = i.book_id,
                    quantity = i.quantity
                }).ToList(),
                total_amount = totalAmount
            };

            _logger.LogInformation("Order {OrderId} marked as successful after payment.", created.id);

            return CreatedAtAction(nameof(GetById), new { id = created.id }, response);
        }
    }
}
