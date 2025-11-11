namespace CatalogService.Models
{
    public class Book
    {
        public Guid id { get; set; }
        public string? isbn { get; set; }
        public string? book_title { get; set; }
        public string? book_author { get; set; }
        public short? year_of_publication { get; set; }
        public string? publisher { get; set; }
        public string? image_url_s { get; set; }
        public string? image_url_m { get; set; }
        public string? image_url_l { get; set; }
    }
}
