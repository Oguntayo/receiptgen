using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReceiptGen.Data;
using ReceiptGen.Models;
using ReceiptGen.Services;
using System.Security.Claims;
using Hangfire;

namespace ReceiptGen.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrdersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IBackgroundJobClient _backgroundJobClient;

        public OrdersController(AppDbContext context, IBackgroundJobClient backgroundJobClient)
        {
            _context = context;
            _backgroundJobClient = backgroundJobClient;
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

                // Enqueue receipt email job
                _backgroundJobClient.Enqueue<IReceiptService>(s => s.SendReceiptJobAsync(order.Id));

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

        [HttpGet("history")]
        public async Task<ActionResult<PagedResponse<OrderResponseDto>>> GetOrderHistory([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var userRole = User.FindFirstValue(ClaimTypes.Role);

            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 50) pageSize = 50;

            IQueryable<Order> query = _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .AsNoTracking();

            if (userRole == UserRole.Business.ToString())
            {
                // Business owners see orders for any of their stores
                query = query.Where(o => o.OrderItems.Any(oi => oi.Product.Store!.OwnerId == userId));
            }
            else
            {
                // Customers see only their own orders
                query = query.Where(o => o.UserId == userId);
            }

            var totalItems = await query.CountAsync();
            var orders = await query
                .OrderByDescending(o => o.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var orderDtos = orders.Select(order => new OrderResponseDto
            {
                Id = order.Id,
                Subtotal = order.Subtotal,
                DiscountAmount = order.DiscountAmount,
                VatAmount = order.VatAmount,
                TotalAmount = order.TotalAmount,
                PaymentMethod = order.PaymentMethod,
                Status = order.Status.ToString(),
                CreatedAt = order.CreatedAt,
                Items = order.OrderItems.Select(oi => new OrderItemResponseDto
                {
                    ProductId = oi.ProductId,
                    ProductName = oi.Product?.Name ?? "Unknown",
                    Quantity = oi.Quantity,
                    UnitPrice = oi.UnitPrice
                }).ToList()
            }).ToList();

            return Ok(new PagedResponse<OrderResponseDto>(orderDtos, totalItems, pageNumber, pageSize));
        }
    }
}
