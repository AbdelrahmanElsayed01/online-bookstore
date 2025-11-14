using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderService.Models;
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
        private readonly string _ordersEndpoint;
        private readonly string _anonKey;
        private readonly string _serviceKey;
        private readonly ILogger<OrdersController> _logger;

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

            var orders = JsonSerializer.Deserialize<List<Order>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

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

            var items = JsonSerializer.Deserialize<List<Order>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var order = (items != null && items.Count > 0) ? items[0] : null;
            return order == null ? NotFound() : Ok(order);
        }

        // ---------- SAGA ORCHESTRATION: RESERVE STOCK + CREATE ORDER ----------

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create([FromBody] CreateOrderRequest request)
        {
            if (request.book_id == Guid.Empty)
                return BadRequest("book_id is required.");

            // 1️⃣ Get user id from JWT (Supabase "sub")
            var userIdClaim = User.FindFirst("sub")?.Value;
            if (string.IsNullOrWhiteSpace(userIdClaim))
                return Unauthorized("User id (sub) not found in token.");

            if (!Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized("User id (sub) is not a valid GUID.");

            // 2️⃣ Forward token to CatalogService
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(authHeader))
            {
                _catalogClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer",
                        authHeader.StartsWith("Bearer ") ? authHeader.Substring(7) : authHeader);
            }

            // 3️⃣ Saga Step 1: Reserve stock in CatalogService
            _logger.LogInformation("Starting Saga: reserve stock for book {BookId}", request.book_id);

            var reserveResp = await _catalogClient.PostAsync($"/api/books/{request.book_id}/reserve", null);

            if (reserveResp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Book {BookId} not found in catalog.", request.book_id);
                return BadRequest($"Book {request.book_id} not found in Catalog.");
            }

            if (reserveResp.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                _logger.LogWarning("Out of stock for book {BookId}.", request.book_id);
                // out of stock, Saga stops here
                var reason = await reserveResp.Content.ReadAsStringAsync();
                return BadRequest($"Out of stock: {reason}");
            }

            if (!reserveResp.IsSuccessStatusCode)
            {
                var body = await reserveResp.Content.ReadAsStringAsync();
                _logger.LogError("Unexpected error reserving stock. Status: {Status}, Body: {Body}",
                    reserveResp.StatusCode, body);
                return StatusCode((int)reserveResp.StatusCode, body);
            }

            _logger.LogInformation("Stock reserved for book {BookId}. Proceeding to create order.", request.book_id);

            // 4️⃣ Saga Step 2: Insert order into Orders DB (Supabase)
            var insertDto = new InsertOrderDto
            {
                user_id = userId,
                book_id = request.book_id,
                amount = 0.01m,     // 1 cent for school project
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

                // 5️⃣ Saga compensation: release stock in CatalogService
                try
                {
                    var releaseResp = await _catalogClient.PostAsync($"/api/books/{request.book_id}/release", null);
                    var releaseBody = await releaseResp.Content.ReadAsStringAsync();
                    if (!releaseResp.IsSuccessStatusCode)
                    {
                        _logger.LogError("Compensation (release stock) failed. Status: {Status}. Body: {Body}",
                            releaseResp.StatusCode, releaseBody);
                    }
                    else
                    {
                        _logger.LogInformation("Compensation successful: stock released for book {BookId}.", request.book_id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception during stock release compensation for book {BookId}.", request.book_id);
                }

                return StatusCode((int)orderResp.StatusCode, orderBody);
            }

            // 6️⃣ Success: deserialize created order from Supabase
            var createdList = JsonSerializer.Deserialize<List<Order>>(orderBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            var created = createdList!.First();

            _logger.LogInformation("Saga completed successfully. Order {OrderId} created for book {BookId}.",
                created.id, created.book_id);

            return CreatedAtAction(nameof(GetById), new { id = created.id }, created);
        }
    }
}
