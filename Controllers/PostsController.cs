using BlogWebApi.Models;
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
    public class PostsController(BlogWebDBContext context) : Controller
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
        public async Task<IActionResult> GetCategories()
        {
            var category = await _context.Categories.ToListAsync();
            if (category.Count == 0)
            {
                return NotFound("No category found");
            }
            return Json(new { category });
        }

        [HttpGet]
        public async Task<IActionResult> GetPosts()
        {
            var data = await _context.Posts.Where(x=>x.IsApproved == true).AsNoTracking()
                        .Join(_context.Categories,
                            post => post.CatId,
                            cat => cat.Id,
                            (posts, cats) => new
                            {
                                postId = posts.Id,
                                posts.Title,
                                posts.Description,
                                posts.Img,
                                cats.Category1,
                                categoryId = cats.Id
                            }).ToListAsync();
            if (data == null)
            {
                return BadRequest("No posts are avaliable");
            }
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> GetPostById(int id)
        {
            try
            {

                var claims = GetClaimsFromToken(Request?.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last() ?? "");
                int userId = int.Parse(claims[0].Value);

                var data = await _context.Posts.Where(x => x.Id == id)
                            .Join(_context.Categories,
                                post => post.CatId,
                                cat => cat.Id,
                                (posts, cats) => new { posts, cats }
                              )
                            .Join(_context.Users,
                            sc => sc.posts.UserId,
                            user => user.Id,
                            (sc, user) => new
                            {
                                postId = sc.posts.Id,
                                sc.posts.Title,
                                sc.posts.Description,
                                sc.posts.IsActive,
                                sc.posts.IsApproved,
                                postImg = sc.posts.Img,
                                sc.posts.CreatedAt,
                                sc.cats.Category1,
                                categoryId = sc.cats.Id,
                                userId = user.Id,
                                userPhoto = user.ProfilePic,
                                AuthorName = user.FirstName + " " + user.LastName
                            }).FirstOrDefaultAsync();

                var watchLater = await _context.WatchLaters.FirstOrDefaultAsync(w => w.PostId == id && w.UserId == userId);
                var finalResult = new
                {
                    data.postId,
                    data.Title,
                    data.Description,
                    data.IsActive,
                    data.IsApproved,
                    data.postImg,
                    data.CreatedAt,
                    data.categoryId,
                    data.userId,
                    data.userPhoto,
                    data.AuthorName,
                    IsWatchLater = watchLater == null ? false : watchLater.IsActive
                };

                var checkOrPost = _context.RecentlyViewedPosts.Where(x => x.PostId == id && x.UserId == userId).FirstOrDefault();
                if (checkOrPost == null)
                {
                    RecentlyViewedPost postDetails = new()
                    {
                        PostId = id,
                        UserId = userId,
                        LastViewed = DateTime.Now
                    };
                    await _context.RecentlyViewedPosts.AddAsync(postDetails);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    checkOrPost.LastViewed = DateTime.Now;
                    await _context.SaveChangesAsync();
                }

                if (finalResult == null)
                {
                    return BadRequest("No posts are avaliable");
                }
                return Json(finalResult);
            }
            catch (Exception e)
            {
                return Json(e.Message);
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetPostByCategory(int id,int postId)
        {
            var data = await _context.Posts.Where(x => x.CatId == id && x.IsActive == true && x.Id != postId).Take(5).Select(x => new
            {
                x.CatId,
                CategoryName = x.Cat.Category1,
                x.Title,
                x.Img,
                PostId = x.Id
            }).ToListAsync();

            if (data.Count == 0)
            {
                var otherCategory = await _context.Posts.Where(x => x.IsActive == true && x.Id != postId).Take(5).Select(x => new
                {
                    x.CatId,
                    CategoryName = x.Cat.Category1,
                    x.Title,
                    x.Img,
                    PostId = x.Id
                }).ToListAsync();
                return Json(otherCategory);
            }
            else
            {
                return Json(data);
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeletePost(int id)
        {
            var data = await _context.Posts.Where(x => x.Id == id).FirstOrDefaultAsync();
            if (data == null)
            {
                return BadRequest("No posts are avaliable");
            }

            data.IsActive = false;
            await _context.SaveChangesAsync();
            return Json(true);
        }

        [HttpPost]
        public async Task<IActionResult> AddPost(Post post, IFormFile? file)
        {
            var claims = GetClaimsFromToken(Request?.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last() ?? "");
            int userId = int.Parse(claims[0].Value);

            if (post == null)
            {
                return BadRequest();
            }

            post.CreatedAt = DateTime.Now;
            post.UserId = userId;
            post.IsApproved = null;
            post.IsActive = null;
            if (file != null)
            {
                post.Img = await UploadFile(file);
            }

            await _context.Posts.AddAsync(post);
            await _context.SaveChangesAsync();
            return Json(true);
        }

        [HttpPost]
        public async Task<IActionResult> UpdatePost(Post updatePost, IFormFile? file)
        {
            var claims = GetClaimsFromToken(Request?.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last() ?? "");
            int userId = int.Parse(claims[0].Value);

            if (updatePost == null)
            {
                return BadRequest();
            }

            var existingPost = await _context.Posts.FindAsync(updatePost.Id);

            existingPost.Title = updatePost.Title;
            existingPost.Description = updatePost.Description;
            existingPost.UserId = userId;
            existingPost.CreatedAt = existingPost.CreatedAt;
            existingPost.CatId = existingPost.CatId;

            if (file != null)
            {
                existingPost.Img = await UploadFile(file);
            }
            else existingPost.Img = existingPost.Img;
            await _context.SaveChangesAsync();

            return Json(true);
        }

        [HttpGet]
        public async Task<IActionResult> GetRecentlyViewedPost()
        {
            var claims = GetClaimsFromToken(Request?.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last() ?? "");
            int userId = int.Parse(claims[0].Value);

            var data = await _context.RecentlyViewedPosts.Where(x=>x.UserId == userId)
                        .Join(_context.Posts,
                        recent => recent.PostId,
                        post => post.Id,
                        (recent, post) => new
                        {
                            post.Id,
                            post.Title,
                            postDescription = post.Description.Substring(0, 20),
                            post.Img
                        }).ToListAsync();
            return Json(data);
        }

        [HttpPost]
        public async Task<IActionResult> AddToWatchLater(int id,bool check)
        {
            var claims = GetClaimsFromToken(Request?.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last() ?? "");
            int userId = int.Parse(claims[0].Value);

            var data = await _context.WatchLaters.Where(x => x.PostId == id && x.UserId== userId).FirstOrDefaultAsync();
            if (data == null)
            {
                WatchLater _ = new()
                {
                    PostId = id,
                    UserId = userId,
                    IsActive = true
                };
                await _context.WatchLaters.AddAsync(_);
            }
            else
            {
                if (check == true && data.IsActive == false)
                {
                
                    data.IsActive = true;
                }
                else data.IsActive = false;
            }
            await _context.SaveChangesAsync();
            return Json(true);
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
