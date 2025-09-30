using Microsoft.AspNetCore.Mvc;
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

        [HttpGet]
        public ActionResult<IEnumerable<Book>> GetAll()
        {
            return Ok(Books);
        }

        [HttpGet("{id}")]
        public ActionResult<Book> GetBook(int id)
        {
            var book = Books.FirstOrDefault(b => b.Id == id);
            if (book == null) return NotFound();
            return Ok(book);
        }

        // POST: api/books
        [HttpPost]
        public ActionResult<Book> CreateBook(Book newBook)
        {
            if (Books.Any(b => b.Id == newBook.Id))
                return Conflict("Book with this ID already exists.");

            Books.Add(newBook);
            return CreatedAtAction(nameof(GetBook), new { id = newBook.Id }, newBook);
        }

        // PUT: api/books/1
        [HttpPut("{id}")]
        public ActionResult UpdateBook(int id, Book updatedBook)
        {
            var book = Books.FirstOrDefault(b => b.Id == id);
            if (book == null) return NotFound();

            book.Title = updatedBook.Title;
            book.Author = updatedBook.Author;
            book.Price = updatedBook.Price;

            return NoContent();
        }

        // DELETE: api/books/1
        [HttpDelete("{id}")]
        public ActionResult DeleteBook(int id)
        {
            var book = Books.FirstOrDefault(b => b.Id == id);
            if (book == null) return NotFound();

            Books.Remove(book);
            return NoContent();
        }
    }
}
