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
        private static readonly object _booksLock = new();
        private static int _nextId = 4; // Start from 4 since we have books 1, 2, 3
        private static readonly List<Book> Books = new()
        {
            new Book { Id = 1, Title = "Clean Code", Author = "Robert C. Martin", Price = 29.99M, Year = 2008 },
            new Book { Id = 2, Title = "The Pragmatic Programmer", Author = "Andy Hunt", Price = 34.99M, Year = 1999 },
            new Book { Id = 3, Title = "Design Patterns", Author = "Erich Gamma", Price = 39.99M, Year = 1994 }
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
            lock (_booksLock)
            {
                var userId = User.FindFirstValue("sub"); // Supabase user UUID
                var validBooks = Books.Where(b => b != null).ToList();
                return Ok(new { userId, books = validBooks });
            }
        }

        [HttpGet("{id}")]
        [Authorize]
        public IActionResult GetBook(int id)
        {
            lock (_booksLock)
            {
                var book = Books.FirstOrDefault(b => b != null && b.Id == id);
                if (book == null) return NotFound();
                return Ok(book);
            }
        }

        [HttpPost]
        [Authorize]
        public IActionResult CreateBook(Book newBook)
        {
            lock (_booksLock)
            {
                // Use atomic counter to avoid ID conflicts
                newBook.Id = _nextId++;
                Books.Add(newBook);

                return CreatedAtAction(nameof(GetBook), new { id = newBook.Id }, newBook);
            }
        }

        [HttpPut("{id}")]
        [Authorize]
        public IActionResult UpdateBook(int id, Book updatedBook)
        {
            lock (_booksLock)
            {
                var book = Books.FirstOrDefault(b => b != null && b.Id == id);
                if (book == null) return NotFound();

                book.Title = updatedBook.Title;
                book.Author = updatedBook.Author;
                book.Price = updatedBook.Price;
                book.Year = updatedBook.Year;

                return NoContent();
            }
        }

        [HttpDelete("{id}")]
        [Authorize]
        public IActionResult DeleteBook(int id)
        {
            lock (_booksLock)
            {
                var book = Books.FirstOrDefault(b => b != null && b.Id == id);
                if (book == null) return NotFound();

                Books.Remove(book);
                return NoContent();
            }
        }
    }
}
