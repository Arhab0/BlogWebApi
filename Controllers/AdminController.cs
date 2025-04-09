using BlogWebApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace BlogWebApi.Controllers
{
    public class AdminController(BlogWebDBContext context) : Controller
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
        public async Task<IActionResult> GetUsers()
        {
            var data = await _context.Users
                        .GroupJoin(_context.Posts,
                            user => user.Id,
                            post => post.UserId,
                            (user, posts) => new
                            {
                                FullName = user.FirstName + " " + user.LastName,
                                user.Country,
                                user.State,
                                user.City,
                                user.PhoneNo,
                                user.Gender,
                                user.Age,
                                PostCount = posts.Count()
                            })
                        .ToListAsync();
            return Json(data);
        }

        [HttpPost]
        public async Task<IActionResult> ChangeUserActiveStatus(int id, bool status)
        {
            var user = await _context.Users.Where(x => x.Id == id).FirstOrDefaultAsync();

            if (status == true)
            {
                user.IsActive = true;
            }
            else user.IsActive = false;

            await _context.SaveChangesAsync();
            return Json("User Active status has been changed");
        }

        [HttpGet]
        public async Task<IActionResult> GetPosts()
        {
            var data = await _context.Users
                       .Join(_context.Posts,
                           user => user.Id,
                           post => post.UserId,
                           (user, post) => new { user, post })
                       .Join(_context.Categories,
                           post => post.post.CatId,
                           category => category.Id,
                           (userPost, category) => new
                           {
                               AuthorName = userPost.user.FirstName + " " + userPost.user.LastName,
                               postId = userPost.post.Id,
                               postTitle = userPost.post.Title,
                               postDescription = userPost.post.Description.Substring(0, 50),
                               postImg = userPost.post.Img,
                               isApproved = userPost.post.IsApproved,
                               ActiveStatus = userPost.post.IsActive,
                               CategoryName = category.Category1
                           })
                       .ToListAsync();

            if (data == null)
            {
                return BadRequest("No posts are avaliable");
            }
            return Json(data);
        }

        [HttpPost]
        public async Task<IActionResult> ApprovePost(int id, bool check)
        {
            var claims = GetClaimsFromToken(Request?.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last() ?? "");
            int userId = int.Parse(claims[0].Value);

            var post = await _context.Posts.Where(x => x.Id == id).FirstOrDefaultAsync();

            if (check == false)
            {
                post.IsApproved = false;
                post.IsActive = false;
                post.RejectedBy = userId;
                await _context.SaveChangesAsync();
                return Json(new { message = "Post has been rejected" });
            }
            post.IsApproved = true;
            post.IsActive = true;
            post.ApprovedBy = userId;
            await _context.SaveChangesAsync();
            return Json(new { message = "Post has been approved" });
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
