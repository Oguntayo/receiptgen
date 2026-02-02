using System;

namespace ReceiptGen.Models
{
    public class Product
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Store association
        public Guid? StoreId { get; set; }
        public Store? Store { get; set; }
    }
}
