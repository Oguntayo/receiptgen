using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReceiptGen.Data;
using ReceiptGen.Models;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace ReceiptGen.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class StoreController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public StoreController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost("create")]
        public async Task<ActionResult<StoreUpgradeResponseDto>> CreateStore(StoreCreateDto request)
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await _context.Users.FindAsync(userId);

            if (user == null) return NotFound("User not found.");

            // Upgrade user role if not already business
            if (user.Role != UserRole.Business)
            {
                user.Role = UserRole.Business;
                _context.Users.Update(user);
            }

            var store = new Store
            {
                Name = request.Name,
                Description = request.Description,
                Address = request.Address,
                PhoneNumber = request.PhoneNumber,
                OwnerId = userId
            };

            _context.Stores.Add(store);
            await _context.SaveChangesAsync();

            // Generate new token with updated role
            // var token = CreateToken(user); // No longer needed

            return Ok(new StoreUpgradeResponseDto
            {
                Store = new StoreResponseDto
                {
                    Id = store.Id,
                    Name = store.Name,
                    Description = store.Description,
                    Address = store.Address,
                    PhoneNumber = store.PhoneNumber,
                    OwnerId = store.OwnerId,
                    CreatedAt = store.CreatedAt
                },
                Message = "Store created successfully."
            });
        }

        [HttpGet("my-stores")]
        [Authorize]
        public async Task<ActionResult<PagedResponse<StoreResponseDto>>> GetMyStores([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 50) pageSize = 50;

            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var query = _context.Stores.Where(s => s.OwnerId == userId);
            
            var totalItems = await query.CountAsync();
            var stores = await query
                .OrderByDescending(s => s.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new StoreResponseDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    Description = s.Description,
                    Address = s.Address,
                    PhoneNumber = s.PhoneNumber,
                    OwnerId = s.OwnerId,
                    CreatedAt = s.CreatedAt
                })
                .ToListAsync();

            return Ok(new PagedResponse<StoreResponseDto>(stores, totalItems, pageNumber, pageSize));
        }

        private string CreateToken(User user)
        {
            List<Claim> claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Email),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration.GetSection("Jwt:Key").Value!));

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature);

            var token = new JwtSecurityToken(
                issuer: _configuration.GetSection("Jwt:Issuer").Value,
                audience: _configuration.GetSection("Jwt:Audience").Value,
                claims: claims,
                expires: DateTime.Now.AddDays(1),
                signingCredentials: creds
            );

            var jwt = new JwtSecurityTokenHandler().WriteToken(token);

            return jwt;
        }
    }
}
