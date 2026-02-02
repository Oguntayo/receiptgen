using System;
using System.Collections.Generic;

namespace ReceiptGen.Models
{
    public class Store
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        
        // Owner association
        public Guid OwnerId { get; set; }
        public User Owner { get; set; } = null!;

        // Products in this store
        public ICollection<Product> Products { get; set; } = new List<Product>();
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
