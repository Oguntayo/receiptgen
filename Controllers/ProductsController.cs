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
    public class ProductsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ProductsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResponse<ProductResponseDto>>> GetProducts([FromQuery] Guid? storeId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 50) pageSize = 50;

            var query = _context.Products.AsQueryable();
            
            if (storeId.HasValue)
            {
                query = query.Where(p => p.StoreId == storeId.Value);
            }

            var totalItems = await query.CountAsync();
            var products = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProductResponseDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    Price = p.Price,
                    Stock = p.Stock,
                    StoreId = p.StoreId,
                    CreatedAt = p.CreatedAt
                })
                .ToListAsync();

            return Ok(new PagedResponse<ProductResponseDto>(products, totalItems, pageNumber, pageSize));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ProductResponseDto>> GetProduct(Guid id)
        {
            var product = await _context.Products.FindAsync(id);

            if (product == null)
            {
                return NotFound();
            }

            return new ProductResponseDto
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                Stock = product.Stock,
                StoreId = product.StoreId,
                CreatedAt = product.CreatedAt
            };
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult<ProductResponseDto>> CreateProduct(ProductCreateDto productDto)
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            
            // Verify store exists and belongs to the user
            var store = await _context.Stores.FirstOrDefaultAsync(s => s.Id == productDto.StoreId && s.OwnerId == userId);
            if (store == null)
            {
                return BadRequest("Invalid StoreId or you do not own this store.");
            }

            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = productDto.Name,
                Description = productDto.Description,
                Price = productDto.Price,
                Stock = productDto.Stock,
                StoreId = productDto.StoreId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            var response = new ProductResponseDto
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                Stock = product.Stock,
                StoreId = product.StoreId,
                CreatedAt = product.CreatedAt
            };

            return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, response);
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> UpdateProduct(Guid id, ProductCreateDto productDto)
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            
            var product = await _context.Products
                .Include(p => p.Store)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
            {
                return NotFound();
            }

            // Verify store ownership
            if (product.Store.OwnerId != userId)
            {
                return Forbid("You do not own the store this product belongs to.");
            }

            product.Name = productDto.Name;
            product.Description = productDto.Description;
            product.Price = productDto.Price;
            product.Stock = productDto.Stock;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteProduct(Guid id)
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            
            var product = await _context.Products
                .Include(p => p.Store)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
            {
                return NotFound();
            }

            // Verify store ownership
            if (product.Store.OwnerId != userId)
            {
                return Forbid("You do not own the store this product belongs to.");
            }

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpGet("all")]
        [AllowAnonymous]
        public async Task<ActionResult<PagedResponse<PublicProductResponseDto>>> GetProductsAll([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 50) pageSize = 50;

            var query = _context.Products.AsQueryable();
            var totalItems = await query.CountAsync();

            var products = await query
                .Include(p => p.Store)
                .OrderByDescending(p => p.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new PublicProductResponseDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    Price = p.Price,
                    Stock = p.Stock,
                    CreatedAt = p.CreatedAt,
                    Store = p.Store == null ? null : new StoreResponseDto
                    {
                        Id = p.Store.Id,
                        Name = p.Store.Name,
                        Description = p.Store.Description,
                        Address = p.Store.Address,
                        PhoneNumber = p.Store.PhoneNumber,
                        OwnerId = p.Store.OwnerId,
                        CreatedAt = p.Store.CreatedAt
                    }
                })
                .ToListAsync();

            return Ok(new PagedResponse<PublicProductResponseDto>(products, totalItems, pageNumber, pageSize));
        }
    }
}
