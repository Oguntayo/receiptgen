using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReceiptGen.Data;
using ReceiptGen.Models;
using System.Security.Claims;

namespace ReceiptGen.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrdersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public OrdersController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("checkout")]
        public async Task<ActionResult<OrderResponseDto>> Checkout(CheckoutRequestDto request)
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (request.Items == null || !request.Items.Any())
            {
                return BadRequest("Order must contain at least one item.");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var paymentMethod = request.PaymentMethod ?? "Unknown";
                var discountAmount = 0m;
                decimal subtotal = 0;
                var orderItems = new List<OrderItem>();
                
                foreach (var item in request.Items)
                {
                    var product = await _context.Products.Include(p => p.Store).FirstOrDefaultAsync(p => p.Id == item.ProductId);
                    if (product == null)
                    {
                        return BadRequest($"Product with ID {item.ProductId} not found.");
                    }

                    if (product.Stock < item.Quantity)
                    {
                        return BadRequest($"Insufficient stock for product '{product.Name}'. Available: {product.Stock}, Requested: {item.Quantity}.");
                    }

                    product.Stock -= item.Quantity;

                    var orderItem = new OrderItem
                    {
                        ProductId = product.Id,
                        Quantity = item.Quantity,
                        UnitPrice = product.Price
                    };

                    orderItems.Add(orderItem);
                    
                    var itemSubtotal = product.Price * item.Quantity;
                    subtotal += itemSubtotal;

                    // Discount is now product-specific only
                    decimal applicableDiscountPercentage = product.DiscountPercentage;

                    discountAmount += itemSubtotal * (applicableDiscountPercentage / 100);
                }

                var order = new Order
                {
                    UserId = userId,
                    Subtotal = subtotal,
                    DiscountAmount = discountAmount,
                    VatAmount = 0, // Will update below
                    TotalAmount = 0, // Will update below
                    PaymentMethod = paymentMethod,
                    Status = OrderStatus.Completed,
                    OrderItems = orderItems
                };

                _context.Orders.Add(order);

                var netAmount = subtotal - discountAmount;
                
                // VAT calculation
                var vatRateStr = Environment.GetEnvironmentVariable("VAT_RATE");
                if (!decimal.TryParse(vatRateStr, out decimal vatRate))
                {
                    vatRate = 0.15m; // Default 15%
                }

                var vatAmount = netAmount * vatRate;
                
                // Total amount: (Subtotal - Discount) + VAT
                var totalAmount = netAmount + vatAmount;

                order.Subtotal = subtotal;
                order.VatAmount = vatAmount;
                order.TotalAmount = totalAmount;
                order.OrderItems = orderItems;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var response = new OrderResponseDto
                {
                    Id = order.Id,
                    Subtotal = order.Subtotal,
                    DiscountAmount = order.DiscountAmount,
                    VatAmount = order.VatAmount,
                    TotalAmount = order.TotalAmount,
                    PaymentMethod = order.PaymentMethod,
                    Status = order.Status.ToString(),
                    CreatedAt = order.CreatedAt,
                    Items = orderItems.Select(oi => new OrderItemResponseDto
                    {
                        ProductId = oi.ProductId,
                        ProductName = _context.Products.Find(oi.ProductId)?.Name ?? "Unknown",
                        Quantity = oi.Quantity,
                        UnitPrice = oi.UnitPrice
                    }).ToList()
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"An error occurred during checkout: {ex.Message}");
            }
        }
    }
}
