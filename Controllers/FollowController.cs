using BlogWebApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace BlogWebApi.Controllers
{
    public class FollowController(BlogWebDBContext context) : Controller
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

        [HttpPost]
        public async Task<IActionResult> AddFollow(int id,bool check)
        {
            var claims = GetClaimsFromToken(Request?.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last() ?? "");
            int userId = int.Parse(claims[0].Value);

            var data = await _context.Followers.Where(x => x.FollowedTo == id && x.FollowedBy == userId).FirstOrDefaultAsync();

            if (data == null)
            {
                Follower _ = new()
                {
                    FollowedBy = userId,
                    FollowedTo = id,
                    FollowedAt = DateTime.Now,
                    IsFollowingActive = true
                };
                await _context.Followers.AddAsync(_);
            }
            else
            {
                if (data.IsFollowingActive != check)
                {
                    data.IsFollowingActive = check;
                    data.FollowedAt = check == true ? DateTime.Now : null;
                }
            }
            await _context.SaveChangesAsync();
            return Json(check);
        }

        [HttpGet]
        public async Task<IActionResult> GetFollowerById(int id)
        {
            var claims = GetClaimsFromToken(Request?.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last() ?? "");
            int userId = int.Parse(claims[0].Value);

            var data = await _context.Followers.Where(x => x.FollowedTo == id && x.FollowedBy == userId).FirstOrDefaultAsync();

            if (data == null)
            {
                var result = false;
                return Json(result);
            }
            var finalResult = data.IsFollowingActive;
            return Json(finalResult);
        }

        [HttpGet]
        public async Task<IActionResult> GetFollowers(int id)
        {
            var userData = _context.Users.Where(x => x.Id == id).FirstOrDefault();

            var data = await _context.Followers.Where(x=>x.FollowedTo == id)
                        .Join(_context.Users,
                        _ => _.FollowedBy,
                        user => user.Id,
                        (_, user) => new 
                        {
                            UserName = (user.FirstName + " " + user.LastName).Trim(),
                            UserId = user.Id,
                            UserProfilePic = user.ProfilePic,
                            userData.CanSeeMyFollowers
                        }).ToListAsync();
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> GetFollowing(int id)
        {
            var data = await _context.Followers.Where(x => x.FollowedBy == id)
                        .Join(_context.Users,
                        _ => _.FollowedTo,
                        user => user.Id,
                        (_, user) => new
                        {
                            UserName = (user.FirstName + " " + user.LastName).Trim(),
                            UserId = user.Id,
                            UserProfilePic = user.ProfilePic,
                        }).ToListAsync();
            return Json(data);
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
