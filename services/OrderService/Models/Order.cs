using System.Text.Json.Serialization;

namespace OrderService.Models
{
    public class Order
    {
        public long id { get; set; }
        public Guid user_id { get; set; }
        public Guid book_id { get; set; }
        public decimal amount { get; set; }
        public string status { get; set; } = "pending";
        public DateTime created_at { get; set; }
    }

    // Request body from client
    public class CreateOrderRequest
    {
        public Guid book_id { get; set; }
    }

    // DTO sent to Supabase INSERT (no id!)
    public class InsertOrderDto
    {
        public Guid user_id { get; set; }
        public Guid book_id { get; set; }
        public decimal amount { get; set; }
        public string status { get; set; } = "pending";
        public DateTime created_at { get; set; }
    }

    // Minimal shape of book we care about (optional, but nice to have)
    public class CatalogBook
    {
        public Guid id { get; set; }
        public int? stock { get; set; }
    }
}

