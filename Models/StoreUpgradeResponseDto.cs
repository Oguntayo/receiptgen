namespace ReceiptGen.Models
{
    public class StoreUpgradeResponseDto
    {
        public StoreResponseDto Store { get; set; } = new();
        public string Message { get; set; } = string.Empty;
    }
}
