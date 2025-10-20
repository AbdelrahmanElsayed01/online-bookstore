using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderService.Models;
using System.Net.Http.Json;

namespace OrderService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private static List<Order> _orders = new List<Order>();
        private static int _nextId = 1;
        private readonly HttpClient _httpClient;

        public OrdersController(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("CatalogService");
        }

        [HttpGet]
        [Authorize]
        public IActionResult GetAllOrders() => Ok(_orders);

        [HttpGet("{id}")]
        [Authorize]
        public IActionResult GetOrder(int id)
        {
            var order = _orders.FirstOrDefault(o => o.Id == id);
            return order == null ? NotFound() : Ok(order);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateOrder([FromBody] Order order)
        {
            // Forward the JWT token to the CatalogService
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (authHeader != null)
            {
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", 
                        authHeader.Substring("Bearer ".Length));
            }

            // Call catalog-service
            var response = await _httpClient.GetAsync($"api/books/{order.BookId}");

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return BadRequest($"Book with id {order.BookId} not found in CatalogService.");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return Unauthorized("Unauthorized to access CatalogService.");
            }

            response.EnsureSuccessStatusCode();

            var book = await response.Content.ReadFromJsonAsync<Book>();

            order.Id = _nextId++;
            _orders.Add(order);

            return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
        }
    }
}
