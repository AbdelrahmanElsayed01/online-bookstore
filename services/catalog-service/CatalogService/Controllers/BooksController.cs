using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CatalogService.Models;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

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
    }
}
