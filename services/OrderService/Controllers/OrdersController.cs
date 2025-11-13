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

        public OrdersController(IConfiguration config, IHttpClientFactory httpClientFactory)
        {
            var baseUrl = config["ORDERS_SUPABASE_URL"] ?? throw new Exception("ORDERS_SUPABASE_URL missing");
            _anonKey = config["ORDERS_SUPABASE_KEY"] ?? throw new Exception("ORDERS_SUPABASE_KEY missing");
            _serviceKey = config["ORDERS_SUPABASE_SERVICE_KEY"] ?? throw new Exception("ORDERS_SUPABASE_SERVICE_KEY missing");
            _ordersEndpoint = baseUrl.TrimEnd('/') + "/rest/v1/orders";

            _http = new HttpClient();

            // ✅ Ensure CatalogService HttpClient always has BaseAddress
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

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create([FromBody] CreateOrderRequest request)
        {
            // 1️⃣ Validate book exists via Catalog Service (forward caller JWT)
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(authHeader))
            {
                _catalogClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer",
                        authHeader.StartsWith("Bearer ") ? authHeader.Substring(7) : authHeader);
            }

            // ✅ use relative path since BaseAddress is guaranteed above
            var bookResp = await _catalogClient.GetAsync($"/api/books/{request.book_id}");
            if (bookResp.StatusCode == System.Net.HttpStatusCode.NotFound)
                return BadRequest($"Book {request.book_id} not found in Catalog.");
            bookResp.EnsureSuccessStatusCode();

            // 2️⃣ Build order
            var userIdClaim = User.FindFirst("sub")?.Value;
            if (string.IsNullOrWhiteSpace(userIdClaim))
                return Unauthorized("User id (sub) not found in token.");

            var newOrder = new Order
            {
                user_id = Guid.Parse(userIdClaim),
                book_id = request.book_id,
                amount = 0.01m,
                status = "pending",
                created_at = DateTime.UtcNow
            };

            // 3️⃣ Insert into Supabase Orders
            var json = JsonSerializer.Serialize(newOrder);
            var req = new HttpRequestMessage(HttpMethod.Post, _ordersEndpoint)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            req.Headers.Add("apikey", _serviceKey);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _serviceKey);
            req.Headers.Add("Prefer", "return=representation");

            var resp = await _http.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                return StatusCode((int)resp.StatusCode, body);

            var createdList = JsonSerializer.Deserialize<List<Order>>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            var created = createdList!.First();

            return CreatedAtAction(nameof(GetById), new { id = created.id }, created);
        }
    }
}
