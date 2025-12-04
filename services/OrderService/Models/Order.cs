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
        public Guid? book_id { get; set; }
        public List<OrderItemRequest>? items { get; set; }
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

    public class OrderItemRequest
    {
        public Guid book_id { get; set; }
        public int quantity { get; set; } = 1;
    }

    public class OrderItemSummary
    {
        public Guid book_id { get; set; }
        public int quantity { get; set; }
    }

    public class OrderResponse
    {
        public Order order { get; set; } = default!;
        public List<OrderItemSummary> items { get; set; } = new();
        public decimal total_amount { get; set; }
    }
}
