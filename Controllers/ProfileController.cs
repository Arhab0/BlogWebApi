using AgeCalculator;
using BlogWebApi.Helpers;
using BlogWebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace BlogWebApi.Controllers
{
    public class ProfileController(BlogWebDBContext context) : Controller
    {
        private readonly BlogWebDBContext _context = context;
        public override JsonResult Json(object? data)
        {
            return new JsonResult(data, new JsonSerializerOptions { PropertyNamingPolicy = null });
        }
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetUserInfo()
        {
            var claims = GetClaimsFromToken(Request?.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last() ?? "");
            int userId = int.Parse(claims[0].Value);

            var data = await _context.Users.Where(x => x.Id == userId).Select(x=> new
            {
                x.FirstName,
                x.LastName,
                x.PhoneNo,
                x.Email,
                x.Country,
                x.State,
                x.City,
                x.ProfilePic,
                x.Age,
                x.Dob
            }).FirstAsync();

            return Json(data);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateUser(User updatedUser, IFormFile file)
        {
            var claims = GetClaimsFromToken(Request?.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last() ?? "");
            int userId = int.Parse(claims[0].Value);

            var user = await _context.Users.Where(x => x.Id == userId).FirstAsync();
            if (user == null)
            {
                return NotFound("User not found");
            }


            user.FirstName = updatedUser.FirstName != "" ? updatedUser.FirstName : user.FirstName;
            user.LastName = updatedUser.LastName != "" ? updatedUser.LastName : user.LastName;
            user.ProfilePic = updatedUser.ProfilePic != "" ? updatedUser.ProfilePic : user.ProfilePic;
            user.Email = updatedUser.Email != "" ? updatedUser.Email : user.Email;
            user.Gender = updatedUser.Gender != "" ? updatedUser.Gender : user.Gender;
            user.Country = updatedUser.Country != "" ? updatedUser.Country : user.Country;
            user.State = updatedUser.State != "" ? updatedUser.State : user.State;
            user.City = updatedUser.City != "" ? updatedUser.City : user.City;
            user.PhoneNo = updatedUser.PhoneNo != "" ? updatedUser.PhoneNo : user.PhoneNo;

            if (updatedUser.Dob.HasValue)
            {
                var dob = updatedUser.Dob.Value;
                var dobDateTime = new DateTime(dob.Year, dob.Month, dob.Day);
                var today = DateTime.Today;

                var age = today.Year - dobDateTime.Year;
                if (dobDateTime.Date > today.AddYears(-age)) age--;

                user.Age = age;
                user.Dob = updatedUser.Dob;
            }
            else
            {
                user.Dob = user.Dob;
                user.Age = updatedUser.Age;
            }


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

        [HttpGet]
        public async Task<IActionResult> GetWatahLaterPosts()
        {
            var claims = GetClaimsFromToken(Request?.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last() ?? "");
            int userId = int.Parse(claims[0].Value);

            var data = await _context.WatchLaters.Where(x => x.UserId == userId).ToListAsync();
            return Json(data);
        }

        private async Task<int> GetUserAge(DateTime dob)
        {
            //DateTime DOB = dob.Value.ToDateTime(new TimeOnly(0, 0));
            var userAge = new Age(dob, DateTime.Now, true);
            int age = userAge.Years;
            return age;
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
    }
}
