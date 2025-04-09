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
using AgeCalculator;
using AgeCalculator.Extensions;
using System.Security.Cryptography;

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

            if (user.Dob.HasValue)
            {
                var dob = user.Dob.Value;
                var dobDateTime = new DateTime(dob.Year, dob.Month, dob.Day);
                var today = DateTime.Today;

                var age = today.Year - dobDateTime.Year;
                if (dobDateTime.Date > today.AddYears(-age)) age--;

                user.Age = age;
                user.Dob = dobDateTime;
            }

            if (file != null)
            {
                user.ProfilePic = await UploadFile(file);
            }
            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();
            return Json(true);
        }

        private async Task<string> UploadFile(IFormFile? ufile)
        {
            try
            {
                if (ufile != null && ufile.Length > 0)
                {
                    var fileName = Path.GetFileName(ufile.FileName);
                    var filePath = Path.Combine(Directory.GetCurrentDirectory(), @"wwwroot\images", fileName);

                    var directory = Path.GetDirectoryName(filePath);
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await ufile.CopyToAsync(fileStream);
                    }

                    return Path.Combine(@"\images", fileName);
                }
                return "";
            }
            catch
            {
                return "x";
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPassword(string emailAddress)
        {
            var user = _context.Users.FirstOrDefault(x => x.Email == emailAddress);
            if (user == null)
            {
                return Json(new { code = "Email Not Found" });
            }
            if (user != null && user.IsActive == true)
            {
                var rand = new Random();
                var uid = rand.Next(1000, 10000);

                Email.SendMessage(
                    $"Hi {user.FirstName} {user.LastName}\n" +
                    $"Your One Time Password(OTP) is :\r\n" +
                    $"<b>{uid}</b>\r\n" +
                    "Enter this code to login to your account.\n" +
                    "Note: This code expires in 10 minutes.\n" +
                    "Thank You!",
                    $"OTP Verification Code - {user.FirstName} {user.LastName}",
                    new() { emailAddress }
                );


                return Json(new { code = uid, userid = user.Id });
            }
            return Json(new { code = "This User is de-activated by admin" });
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> CreateNewPassword(string password, int userId)
        {
            var user = await _context.Users.Where(x => x.Id == userId).FirstOrDefaultAsync();
            password = BitConverter.ToString(MD5.HashData(Encoding.ASCII.GetBytes(password))).Replace("-", "").ToLower();
            user.Password = password;
            await _context.SaveChangesAsync();
            return Json(new { user });
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
                    new Claim("FullName", fullName.ToString()),
                    new Claim("RoleId", user.RoleId.ToString())
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
