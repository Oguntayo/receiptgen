using System;

namespace ReceiptGen.Models
{
    public class ReceiptResponseDto
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public string S3Url { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public OrderResponseDto? Order { get; set; }
    }
}
