namespace OrderService.Models
{
    public class Order
    {
        public int Id { get; set; }
        public int BookId { get; set; }
        public int Quantity { get; set; }
        public string? UserId { get; set; }    // Supabase sub (UUID)
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
