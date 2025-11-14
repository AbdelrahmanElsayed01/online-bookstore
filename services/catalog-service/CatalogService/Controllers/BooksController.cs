using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CatalogService.Models;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CatalogService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BooksController : ControllerBase
    {
        private readonly HttpClient _http;
        private readonly string _baseUrl;
        private readonly string _anonKey;
        private readonly string _serviceKey;
        private readonly string _booksEndpoint;

        public BooksController(IConfiguration config)
        {
            _baseUrl = config["SUPABASE_URL"] ?? throw new Exception("SUPABASE_URL missing");
            _anonKey = config["SUPABASE_KEY"] ?? throw new Exception("SUPABASE_KEY missing");
            _serviceKey = config["SUPABASE_SERVICE_KEY"] ?? throw new Exception("SUPABASE_SERVICE_KEY missing");
            _booksEndpoint = _baseUrl.TrimEnd('/') + "/rest/v1/books";
            _http = new HttpClient();
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAll()
        {
            var req = new HttpRequestMessage(HttpMethod.Get, _booksEndpoint);
            req.Headers.Add("apikey", _anonKey);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _anonKey);

            var resp = await _http.SendAsync(req);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            var books = JsonSerializer.Deserialize<List<Book>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return Ok(books);
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetById(Guid id)
        {
            var reqUrl = $"{_booksEndpoint}?id=eq.{id}";
            var req = new HttpRequestMessage(HttpMethod.Get, reqUrl);
            req.Headers.Add("apikey", _anonKey);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _anonKey);

            var resp = await _http.SendAsync(req);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            var items = JsonSerializer.Deserialize<List<Book>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var book = items != null && items.Count > 0 ? items[0] : null;
            return book == null ? NotFound() : Ok(book);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create([FromBody] Book book)
        {
            book.id = Guid.NewGuid();

            var json = JsonSerializer.Serialize(book);
            var req = new HttpRequestMessage(HttpMethod.Post, _booksEndpoint)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            req.Headers.Add("apikey", _serviceKey);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _serviceKey);

            var resp = await _http.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                return StatusCode((int)resp.StatusCode, body);

            return CreatedAtAction(nameof(GetById), new { id = book.id }, book);
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> Update(Guid id, [FromBody] Book updated)
        {
            updated.id = id;

            var json = JsonSerializer.Serialize(updated);
            var req = new HttpRequestMessage(HttpMethod.Patch, $"{_booksEndpoint}?id=eq.{id}")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            req.Headers.Add("apikey", _serviceKey);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _serviceKey);

            var resp = await _http.SendAsync(req);
            resp.EnsureSuccessStatusCode();

            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> Delete(Guid id)
        {
            var req = new HttpRequestMessage(HttpMethod.Delete, $"{_booksEndpoint}?id=eq.{id}");
            req.Headers.Add("apikey", _serviceKey);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _serviceKey);

            var resp = await _http.SendAsync(req);
            resp.EnsureSuccessStatusCode();

            return NoContent();
        }

        // ---------- SAGA SUPPORT: RESERVE / RELEASE STOCK ----------

        // POST /api/books/{id}/reserve
        [HttpPost("{id}/reserve")]
        [Authorize]
        public async Task<IActionResult> ReserveStock(Guid id)
        {
            // 1) Get the book (using service key to see stock)
            var getReq = new HttpRequestMessage(HttpMethod.Get, $"{_booksEndpoint}?id=eq.{id}");
            getReq.Headers.Add("apikey", _serviceKey);
            getReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _serviceKey);

            var getResp = await _http.SendAsync(getReq);
            getResp.EnsureSuccessStatusCode();

            var json = await getResp.Content.ReadAsStringAsync();
            var items = JsonSerializer.Deserialize<List<Book>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var book = items != null && items.Count > 0 ? items[0] : null;
            if (book == null)
                return NotFound($"Book {id} not found.");

            var currentStock = book.stock ?? 0;
            if (currentStock <= 0)
                return Conflict(new { message = "Out of stock" });

            var newStock = currentStock - 1;

            // 2) Patch stock
            var patchBody = JsonSerializer.Serialize(new { stock = newStock });
            var patchReq = new HttpRequestMessage(HttpMethod.Patch, $"{_booksEndpoint}?id=eq.{id}")
            {
                Content = new StringContent(patchBody, Encoding.UTF8, "application/json")
            };
            patchReq.Headers.Add("apikey", _serviceKey);
            patchReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _serviceKey);
            patchReq.Headers.Add("Prefer", "return=representation");

            var patchResp = await _http.SendAsync(patchReq);
            var patchContent = await patchResp.Content.ReadAsStringAsync();

            if (!patchResp.IsSuccessStatusCode)
                return StatusCode((int)patchResp.StatusCode, patchContent);

            var updatedList = JsonSerializer.Deserialize<List<Book>>(patchContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            var updated = updatedList?.FirstOrDefault();

            return Ok(new
            {
                message = "Stock reserved",
                book_id = id,
                previous_stock = currentStock,
                new_stock = updated?.stock ?? newStock
            });
        }

        // POST /api/books/{id}/release
        [HttpPost("{id}/release")]
        [Authorize]
        public async Task<IActionResult> ReleaseStock(Guid id)
        {
            // 1) Get the book
            var getReq = new HttpRequestMessage(HttpMethod.Get, $"{_booksEndpoint}?id=eq.{id}");
            getReq.Headers.Add("apikey", _serviceKey);
            getReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _serviceKey);

            var getResp = await _http.SendAsync(getReq);
            getResp.EnsureSuccessStatusCode();

            var json = await getResp.Content.ReadAsStringAsync();
            var items = JsonSerializer.Deserialize<List<Book>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var book = items != null && items.Count > 0 ? items[0] : null;
            if (book == null)
                return NotFound($"Book {id} not found.");

            var currentStock = book.stock ?? 0;
            var newStock = currentStock + 1;

            // 2) Patch stock
            var patchBody = JsonSerializer.Serialize(new { stock = newStock });
            var patchReq = new HttpRequestMessage(HttpMethod.Patch, $"{_booksEndpoint}?id=eq.{id}")
            {
                Content = new StringContent(patchBody, Encoding.UTF8, "application/json")
            };
            patchReq.Headers.Add("apikey", _serviceKey);
            patchReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _serviceKey);
            patchReq.Headers.Add("Prefer", "return=representation");

            var patchResp = await _http.SendAsync(patchReq);
            var patchContent = await patchResp.Content.ReadAsStringAsync();

            if (!patchResp.IsSuccessStatusCode)
                return StatusCode((int)patchResp.StatusCode, patchContent);

            var updatedList = JsonSerializer.Deserialize<List<Book>>(patchContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            var updated = updatedList?.FirstOrDefault();

            return Ok(new
            {
                message = "Stock released",
                book_id = id,
                previous_stock = currentStock,
                new_stock = updated?.stock ?? newStock
            });
        }
    }
}
