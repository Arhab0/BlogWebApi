using AgeCalculator;
using BlogWebApi.Helpers;
using BlogWebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
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
                x.Id,
                x.FirstName,
                x.LastName,
                x.PhoneNo,
                x.Email,
                x.Country,
                x.State,
                x.City,
                x.ProfilePic,
                x.Age,
                x.Dob,
                x.Gender,
            }).FirstOrDefaultAsync();

            return Json(data);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateUser(User updatedUser, IFormFile file)
        {
            try
            {

                var claims = GetClaimsFromToken(Request?.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last() ?? "");
                int userId = int.Parse(claims[0].Value);

                var user = await _context.Users.Where(x => x.Id == userId).FirstOrDefaultAsync();
                if (user == null)
                {
                    return NotFound("User not found");
                }


                user.FirstName = updatedUser.FirstName != "" ? updatedUser.FirstName : user.FirstName;
                user.LastName = updatedUser.LastName != "" ? updatedUser.LastName : user.LastName;
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
                    user.Dob = dobDateTime;
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
                else
                {
                    user.ProfilePic = user.ProfilePic;
                }

                if (updatedUser.Password != null)
                {
                    user.Password = Security.Encrypt(updatedUser.Password);
                }
                else user.Password = user.Password;

                await _context.SaveChangesAsync();
                return Json(true);
            }
            catch (Exception e)
            {
                return Json(e.Message);
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAllPostOfUser()
        {
            var claims = GetClaimsFromToken(Request?.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last() ?? "");
            int userId = int.Parse(claims[0].Value);

            var data = await _context.Posts.Where(x => x.UserId == userId).Select(x => new
            {
                x.Id,
                x.Img,
                x.Title,
                x.CreatedAt,
                isActive = x.IsActive,
                x.IsApproved,
                Description = x.Description.Substring(0,50)
            }).ToListAsync();

            if (data.Count == 0)
            {
                return Json(new {message= "This User didn't post anything"});
            }
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> GetHistoryPost()
        {
            var claims = GetClaimsFromToken(Request?.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last() ?? "");
            int userId = int.Parse(claims[0].Value);

            var data = await _context.Posts
                        .Join(_context.RecentlyViewedPosts.Where(x => x.UserId == userId),
                        post => post.Id,
                        recent => recent.PostId,
                        (post, recent) => new
                        {
                            post,
                            recent
                        })
                        .Join(_context.Users,
                         postDetail => postDetail.post.Id,
                         user => user.Id,
                         (postDetail, user) => new
                         {
                             postDetail.post.Id,
                             postDetail.post.Img,
                             postDetail.post.Title,
                             postImg = postDetail.post.Img,
                             postDetail.recent.LastViewed,
                             AuthorName = user.FirstName + " " + user.LastName ?? "",
                             userImg = user.ProfilePic
                         }).ToListAsync();

            if (data.Count == 0)
            {
                return Json(new {post=false});
            }
            return Json(new {post = data});
        }
        [HttpGet]
        public async Task<IActionResult> GetWatahLaterPosts()
        {
            var claims = GetClaimsFromToken(Request?.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last() ?? "");
            int userId = int.Parse(claims[0].Value);

            var data = await _context.WatchLaters.Where(x => x.UserId == userId).ToListAsync();
            return Json(data);
        }

        [HttpPost]
        public async Task<IActionResult> ChangePostActiveStatus(int id, bool status,int userid)
        {
            var claims = GetClaimsFromToken(Request?.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last() ?? "");
            int userId = int.Parse(claims[0].Value);

            var data = await _context.Posts.Where(x => x.Id == id).FirstOrDefaultAsync();
            if (data != null && data.IsApproved == true)
            {

                if (data.UserId == userid)
                {
                    if (status == true)
                    {
                        data.IsActive = true;
                        await _context.SaveChangesAsync();
                        return Json(true);
                    }
                    else
                    {
                        data.IsActive = false;
                        await _context.SaveChangesAsync();
                        return Json(false);
                    }
                }
                else
                {
                    return Json(new { message="This post doesn't belong to you" });
                }
            }
            else
            {
                return Json(new { message = "Can't change the status of this post. It has rejected by Admin!" });
            }
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
    }
}
