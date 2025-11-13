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

    // For POST body (only needs the book id)
    public class CreateOrderRequest
    {
        public Guid book_id { get; set; }
    }
}
