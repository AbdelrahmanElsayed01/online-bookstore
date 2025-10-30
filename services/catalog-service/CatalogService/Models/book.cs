namespace CatalogService.Models
{
public class Book
{
    public Guid id { get; set; }
    public string title { get; set; }
    public string author { get; set; }
    public decimal price { get; set; }
    public string? description { get; set; }
    public DateTime? published_date { get; set; }
}
}
