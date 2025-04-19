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
                x.CanSeeMyFollowers
                
            }).FirstOrDefaultAsync();

            var _ = await _context.Followers.ToListAsync();
            var Followers = _.Where(x => x.FollowedTo == userId).Count();
            var Following = _.Where(x => x.FollowedBy == userId).Count();
            var PostCount = await _context.Posts.Where(x => x.UserId == userId).CountAsync();

            var finalResult = new
            {
                data.Id,
                FullName = (data.FirstName+" "+data.LastName).Trim(),
                data.FirstName,
                data.LastName,
                data.PhoneNo,
                data.Email,
                data.Country,
                data.State,
                data.City,
                data.ProfilePic,
                data.Age,
                data.Dob,
                data.Gender,
                data.CanSeeMyFollowers,
                Followers,
                Following,
                PostCount
            };

            return Json(finalResult);
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
                user.CanSeeMyFollowers = user.CanSeeMyFollowers != updatedUser.CanSeeMyFollowers ? updatedUser.CanSeeMyFollowers : user.CanSeeMyFollowers;

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
                x.Description
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

            var data = await _context.WatchLaters.Where(x => x.UserId == userId && x.IsActive == true)
                            .Join(_context.Posts.Where(x=>x.IsActive == true),
                            watchlater => watchlater.PostId,
                            post=>post.Id,
                            (watchlater, post) => new 
                            {
                                postId = post.Id,
                                postImg = post.Img,
                                post.Title
                            }).ToListAsync();

            if (data.Count == 0)
            {
                return Json(new { message = "No post for watch later" });
            }
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> GetWatahLaterPostById(int id)
        {
            var claims = GetClaimsFromToken(Request?.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last() ?? "");
            int userId = int.Parse(claims[0].Value);

            var data = await _context.WatchLaters.Where(x => x.UserId == userId && x.PostId == id).FirstOrDefaultAsync();
            if (data == null)
            {
                var finalResult = new
                {
                    isWatchLater = data == null ? false : data.IsActive,
                };
                return Json(finalResult);
            }
            var result = new
            {
                isWatchLater = data.IsActive,
                data.PostId,
                data.UserId
            };
            return Json(result);

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
                        return Json(new { message = true });
                    }
                    else
                    {
                        data.IsActive = false;
                        await _context.SaveChangesAsync();
                        return Json(new { message = false });
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

        [HttpGet]
        public async Task<IActionResult> GetRejectedPosts()
        {
            var data = await _context.Users
                       .Join(_context.Posts.Where(x => x.IsApproved == false),
                           user => user.Id,
                           post => post.UserId,
                           (user, post) => new { user, post })
                       .Join(_context.Categories,
                           post => post.post.CatId,
                           category => category.Id,
                           (userPost, category) => new
                           {
                               postId = userPost.post.Id,
                               postTitle = userPost.post.Title,
                               userPost.post.CreatedAt,
                               postImg = userPost.post.Img,
                               CategoryName = category.Category1,
                               userPost.post.ReasonForReject,
                               userPost.post.RejectCount
                           })
                       .ToListAsync();

            if (data == null)
            {
                return BadRequest("No posts are avaliable");
            }
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
