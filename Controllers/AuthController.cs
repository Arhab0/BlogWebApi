using BlogWebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using BlogWebApi.Helpers;
using Microsoft.EntityFrameworkCore;

namespace BlogWebApi.Controllers
{
    public class AuthController(BlogWebDBContext context) : Controller
    {
        public override JsonResult Json(object? data)
        {
            return new JsonResult(data, new JsonSerializerOptions { PropertyNamingPolicy = null });
        }
        private readonly BlogWebDBContext _context = context;

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Login(string email, string password)
        {
            password = Security.Encrypt(password);
            var user = await _context.Users.Where(x => x.Email == email && x.Password == password && x.IsActive == true).FirstOrDefaultAsync();
            if (user == null) { return Json("User not Found"); }
            var token = GenerateJwtToken(user);
            user.Password = null;
            return Json(new { token, user });
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> CreateUser(User user, IFormFile file)
        {
            var data = await _context.Users.Where(x => x.Email == user.Email).AnyAsync();

            if (data == true)
            {
                return BadRequest(new{ message="this email address already exist"});
            }
            user.Password = Security.Encrypt(user.Password);
            user.CreatedAt = DateTime.Now;
            user.RoleId = 2;
            user.IsActive = true;

            if (file != null)
            {
                user.ProfilePic = await UploadFile(file);
            }
            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();
            return Json(true);
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> UpdateUser(User updatedUser, IFormFile file)
        {
            var claims = GetClaimsFromToken(Request?.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last() ?? "");
            int userId = int.Parse(claims[0].Value);

            var user = await _context.Users.Where(x=>x.Id == userId).FirstAsync();
            if (user == null)
                return NotFound("User not found");


            user.FirstName = updatedUser.FirstName != "" ? updatedUser.FirstName : user.FirstName;
            user.LastName = updatedUser.LastName != "" ? updatedUser.LastName : user.LastName;
            user.ProfilePic = updatedUser.ProfilePic != "" ? updatedUser.ProfilePic : user.ProfilePic;
            user.Email = updatedUser.Email != "" ? updatedUser.Email : user.Email;
            user.Gender = updatedUser.Gender != "" ? updatedUser.Gender : user.Gender;
            user.Age = updatedUser.Age != null ? updatedUser.Age : user.Age;
            user.Country = updatedUser.Country != "" ? updatedUser.Country : user.Country;
            user.State = updatedUser.State != "" ? updatedUser.State : user.State;
            user.City = updatedUser.City != "" ? updatedUser.City : user.City;
            user.PhoneNo = updatedUser.PhoneNo != "" ? updatedUser.PhoneNo : user.PhoneNo;

            if (file != null)
            {
                user.ProfilePic = await UploadFile(file);
            }

            if (updatedUser.Password != "")
            {
                user.Password = Security.Encrypt(updatedUser.Password);
            }
            else user.Password = user.Password;

            await _context.SaveChangesAsync();
            return Ok("User updated successfully");
        }

        private async Task<string> UploadFile(IFormFile? ufile)
        {
            try
            {
                if (ufile != null && ufile.Length > 0)
                {
                    var fileName = Path.GetFileName(ufile.FileName);
                    var filePath = Path.Combine(Directory.GetCurrentDirectory(), @"wwwroot\images", fileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await ufile.CopyToAsync(fileStream);
                    }
                    return filePath;
                }
                return "";
            }
            catch
            {
                return "x";
            }
        }

        private string GenerateJwtToken(User user)
        {
            var fullName = user.FirstName + " " + user.LastName;
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes("ArhabUmer2004$BlogWebApiSuperSecretKey123");
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                    new Claim(ClaimTypes.Name, user.Id.ToString()),
                    new Claim("FullName", user.Id.ToString()),
                    new Claim("RoleId", user.Id.ToString())
                }),
                Issuer = "https://localhost:44385/",
                Audience = "https://localhost:44385/",
                Expires = DateTime.UtcNow.AddHours(2), // Set token expiration time
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        public List<Claim> GetClaimsFromToken(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidIssuer = "https://localhost:44385/",
                ValidAudience = "https://localhost:44385/",
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("ArhabUmer2004$BlogWebApiSuperSecretKey123")),
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = false,
                ValidateIssuerSigningKey = true
            }, out SecurityToken validatedToken);

            var jwtToken = (JwtSecurityToken)validatedToken;
            return jwtToken.Claims.ToList();
        }

    }
}
