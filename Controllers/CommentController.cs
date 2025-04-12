using BlogWebApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.Design;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace BlogWebApi.Controllers
{
    public class CommentController(BlogWebDBContext context) : Controller
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
        public async Task<IActionResult> GetComments(int id)
        {
            var comments = await _context.Comments.Where(x=>x.PostId == id).OrderByDescending(x=>x.Id)
                        .Join(_context.Users,
                            comments => comments.UserId,
                            user => user.Id,
                            (comments, users) => new
                            {
                                commentId = comments.Id,
                                comment = comments.Comment1,
                                comments.CommentAt,
                                comments.IsEdited,
                                users.ProfilePic,
                                userId = users.Id,
                                postId = comments.PostId,
                                userName = users.FirstName + " " + users.LastName
                            }).ToListAsync();
            if (comments == null)
            {
                return BadRequest();
            }
            return Json(comments);
        }

        [HttpPost]
        public async Task<IActionResult> AddComment(Comment comment)
        {
            var claims = GetClaimsFromToken(Request?.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last() ?? "");
            int userId = int.Parse(claims[0].Value);

            if (comment == null)
            {
                return BadRequest();
            }

            comment.CommentAt = DateTime.Now;
            comment.UserId = userId;

            await _context.Comments.AddAsync(comment);
            await _context.SaveChangesAsync();
            return Json(true);
        }

        [HttpPost]
        public async Task<IActionResult> AddReplyComment(ReplyComment comment)
        {
            var claims = GetClaimsFromToken(Request?.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last() ?? "");
            int userId = int.Parse(claims[0].Value);

            if (comment == null)
            {
                return BadRequest();
            }

            var replyTo = await _context.Comments.Where(x => x.Id == comment.CommentId).Select(x => x.UserId ).FirstOrDefaultAsync();

            comment.RepliedAt = DateTime.Now;
            comment.RepliedBy = userId;
            comment.RepliedTo = replyTo;

            await _context.ReplyComments.AddAsync(comment);
            await _context.SaveChangesAsync();
            return Json(true);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateComment(Comment updateComment)
        {
            var claims = GetClaimsFromToken(Request?.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last() ?? "");
            int userId = int.Parse(claims[0].Value);

            var existingComment = await _context.Comments.FindAsync(updateComment.Id);

            if (updateComment == null)
            {
                return BadRequest();
            }

            existingComment.PostId = existingComment.PostId;
            existingComment.CommentAt = existingComment.CommentAt;
            existingComment.UserId = userId;
            existingComment.IsEdited = true;
            existingComment.Comment1 = updateComment.Comment1;

            await _context.SaveChangesAsync();
            return Json(true);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteComment(int id)
        {
            var data = await _context.Comments.Where(x => x.Id == id).FirstOrDefaultAsync();
            if (data == null)
            {
                return BadRequest();
            }

            _context.Comments.Remove(data);
            await _context.SaveChangesAsync();
            return Json(true);
        }


        [HttpGet]
        public async Task<IActionResult> GetRepliedComments(int id)
        {
            var replyComments = await _context.ReplyComments
                            .Join(_context.Comments.Where(x => x.PostId == id),
                            replyComment => replyComment.CommentId,
                            comment => comment.Id,
                            (replyComment, comment) => new { replyComment, comment })
                            .Join(_context.Users,
                            rbc => rbc.replyComment.RepliedBy,
                            repliedBy => repliedBy.Id,
                            (rbc, repliedBy) => new {rbc,repliedBy})
                            .Join(_context.Users,
                            rtc=>rtc.rbc.replyComment.RepliedTo,
                            repliedTo => repliedTo.Id,
                            (rtc, repliedTo) => new
                            {
                                ReplyCommentId = rtc.rbc.replyComment.Id,
                                rtc.rbc.replyComment.Reply,
                                rtc.rbc.replyComment.IsEdited,
                                RepliedCommentId = rtc.rbc.replyComment.CommentId,
                                RepliedToId = rtc.rbc.replyComment.RepliedTo,
                                RepliedById = rtc.rbc.replyComment.RepliedBy,
                                rtc.rbc.replyComment.RepliedAt,
                                RepliedByUsername = rtc.repliedBy.FirstName +" " + rtc.repliedBy.LastName,
                                RepliedToUsername = repliedTo.FirstName + " " + repliedTo.LastName,
                                RepliedByProfilePic = rtc.repliedBy.ProfilePic,
                                RepliedToProfilePic = repliedTo.ProfilePic,
                                rtc.rbc.replyComment.PostId
                            }).ToListAsync();
            if (replyComments == null)
            {
                return BadRequest();
            }
            return Json(replyComments);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteReplyComment(int id)
        {
            var data = await _context.ReplyComments.Where(x => x.Id == id).FirstOrDefaultAsync();
            if (data == null)
            {
                return BadRequest();
            }

            _context.ReplyComments.Remove(data);
            await _context.SaveChangesAsync();
            return Json(true);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateReplyComment(ReplyComment comment)
        {
            var claims = GetClaimsFromToken(Request?.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last() ?? "");
            int userId = int.Parse(claims[0].Value);

            var existingComment = await _context.ReplyComments.Where(x=>x.Id == comment.Id).FirstOrDefaultAsync();

            existingComment.RepliedTo = existingComment.RepliedTo;
            existingComment.RepliedBy = userId;
            existingComment.RepliedAt = existingComment.RepliedAt;
            existingComment.PostId = existingComment.PostId;
            existingComment.CommentId = existingComment.CommentId;
            existingComment.Reply = comment.Reply;
            existingComment.IsEdited = true;

            //_context.ReplyComments.Update(existingComment);
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
    }
}
