using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using CatalogService.Models;

namespace CatalogService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BooksController : ControllerBase
    {
        private static readonly List<Book> Books = new()
        {
            new Book { Id = 1, Title = "Clean Code", Author = "Robert C. Martin", Price = 29.99M },
            new Book { Id = 2, Title = "The Pragmatic Programmer", Author = "Andy Hunt", Price = 34.99M },
            new Book { Id = 3, Title = "Design Patterns", Author = "Erich Gamma", Price = 39.99M }
        };

        // Public: no authentication required
        [HttpGet("public")]
        public IActionResult PublicEndpoint() =>
            Ok("✅ Public endpoint, no authentication needed.");

        // Protected: requires Supabase JWT
        [HttpGet]
        [Authorize]
        public IActionResult GetAll()
        {
            var userId = User.FindFirstValue("sub"); // Supabase user UUID
            return Ok(new { userId, books = Books });
        }

        [HttpGet("{id}")]
        [Authorize]
        public IActionResult GetBook(int id)
        {
            var book = Books.FirstOrDefault(b => b.Id == id);
            if (book == null) return NotFound();
            return Ok(book);
        }

        [HttpPost]
        [Authorize]
        public IActionResult CreateBook(Book newBook)
        {
            if (Books.Any(b => b.Id == newBook.Id))
                return Conflict("Book with this ID already exists.");

            Books.Add(newBook);
            return CreatedAtAction(nameof(GetBook), new { id = newBook.Id }, newBook);
        }

        [HttpPut("{id}")]
        [Authorize]
        public IActionResult UpdateBook(int id, Book updatedBook)
        {
            var book = Books.FirstOrDefault(b => b.Id == id);
            if (book == null) return NotFound();

            book.Title = updatedBook.Title;
            book.Author = updatedBook.Author;
            book.Price = updatedBook.Price;

            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize]
        public IActionResult DeleteBook(int id)
        {
            var book = Books.FirstOrDefault(b => b.Id == id);
            if (book == null) return NotFound();

            Books.Remove(book);
            return NoContent();
        }
    }
}
