using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReceiptGen.Data;
using ReceiptGen.Models;
using ReceiptGen.Services;
using System.Security.Claims;

namespace ReceiptGen.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ReceiptsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IS3Service _s3Service;

        public ReceiptsController(AppDbContext context, IS3Service s3Service)
        {
            _context = context;
            _s3Service = s3Service;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResponse<ReceiptResponseDto>>> GetReceipts([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var userRole = User.FindFirstValue(ClaimTypes.Role);

            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 50) pageSize = 50;

            IQueryable<Receipt> query = _context.Receipts
                .Include(r => r.Order)
                    .ThenInclude(o => o.OrderItems)
                        .ThenInclude(oi => oi.Product)
                .AsNoTracking();

            if (userRole == UserRole.Business.ToString())
            {
                // Business owners see receipts for any of their stores
                query = query.Where(r => r.Order.OrderItems.Any(oi => oi.Product.Store!.OwnerId == userId));
            }
            else
            {
                // Customers see only their own receipts
                query = query.Where(r => r.Order.UserId == userId);
            }

            var totalItems = await query.CountAsync();
            var receipts = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var receiptDtos = receipts.Select(r => new ReceiptResponseDto
            {
                Id = r.Id,
                OrderId = r.OrderId,
                S3Url = _s3Service.GetPreSignedUrl(r.S3Url),
                CreatedAt = r.CreatedAt,
                Order = new OrderResponseDto
                {
                    Id = r.Order.Id,
                    Subtotal = r.Order.Subtotal,
                    DiscountAmount = r.Order.DiscountAmount,
                    VatAmount = r.Order.VatAmount,
                    TotalAmount = r.Order.TotalAmount,
                    PaymentMethod = r.Order.PaymentMethod,
                    Status = r.Order.Status.ToString(),
                    CreatedAt = r.Order.CreatedAt,
                    Items = r.Order.OrderItems.Select(oi => new OrderItemResponseDto
                    {
                        ProductId = oi.ProductId,
                        ProductName = oi.Product?.Name ?? "Unknown",
                        Quantity = oi.Quantity,
                        UnitPrice = oi.UnitPrice
                    }).ToList()
                }
            }).ToList();

            return Ok(new PagedResponse<ReceiptResponseDto>(receiptDtos, totalItems, pageNumber, pageSize));
        }
    }
}
