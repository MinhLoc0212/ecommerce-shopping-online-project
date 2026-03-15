using GearsHouse.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GearsHouse.Controllers
{
    [Authorize]
    public class ChatController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        public ChatController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> MyThread()
        {
            var user = await _userManager.GetUserAsync(User);
            var thread = await _context.ChatThreads.FirstOrDefaultAsync(t => t.CustomerId == user.Id && !t.IsClosed);
            if (thread == null)
            {
                thread = new ChatThread { CustomerId = user.Id };
                _context.ChatThreads.Add(thread);
                await _context.SaveChangesAsync();
            }
            return Json(new { threadId = thread.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Messages(int threadId, int? afterId)
        {
            var userId = _userManager.GetUserId(User);
            var thread = await _context.ChatThreads.FirstOrDefaultAsync(t => t.Id == threadId);
            if (thread == null) return NotFound();
            var isStaff = User.IsInRole(Roles.Role_Admin) || User.IsInRole(Roles.Role_Employee);
            if (thread.CustomerId != userId && !isStaff) return Forbid();

            if (isStaff)
            {
                var unreadMessages = await _context.ChatMessages
                    .Where(m => m.ThreadId == threadId && !m.IsStaff && !m.IsRead)
                    .ToListAsync();
                if (unreadMessages.Any())
                {
                    foreach (var msg in unreadMessages) msg.IsRead = true;
                    await _context.SaveChangesAsync();
                }
            }

            var query = _context.ChatMessages.Where(m => m.ThreadId == threadId).OrderBy(m => m.Id);
            if (afterId.HasValue) query = query.Where(m => m.Id > afterId.Value).OrderBy(m => m.Id);
            var list = await query.Take(200).ToListAsync();
            var res = list.Select(m => new { id = m.Id, content = m.Content, isStaff = m.IsStaff, at = m.CreatedAt.ToString("HH:mm dd/MM/yyyy") });
            return Json(res);
        }

        [HttpGet]
        public async Task<IActionResult> GetUnreadCount()
        {
            if (User.IsInRole(Roles.Role_Admin) || User.IsInRole(Roles.Role_Employee))
            {
                var count = await _context.ChatMessages.CountAsync(m => !m.IsStaff && !m.IsRead);
                return Json(new { count });
            }
            return Json(new { count = 0 });
        }

        [HttpPost]
        public async Task<IActionResult> Send(int threadId, string content)
        {
            content = (content ?? "").Trim();
            if (string.IsNullOrEmpty(content)) return BadRequest();
            var user = await _userManager.GetUserAsync(User);
            var thread = await _context.ChatThreads.FirstOrDefaultAsync(t => t.Id == threadId);
            if (thread == null) return NotFound();
            var isStaff = User.IsInRole(Roles.Role_Admin) || User.IsInRole(Roles.Role_Employee);
            if (!isStaff && thread.CustomerId != user.Id) return Forbid();
            if (isStaff && thread.StaffId == null)
            {
                thread.StaffId = user.Id;
                _context.ChatThreads.Update(thread);
            }
            var message = new ChatMessage { ThreadId = thread.Id, SenderId = user.Id, IsStaff = isStaff, Content = content };
            _context.ChatMessages.Add(message);
            await _context.SaveChangesAsync();
            return Json(new { id = message.Id });
        }

        [Authorize(Roles = Roles.Role_Admin + "," + Roles.Role_Employee)]
        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var threads = await _context.ChatThreads
                .OrderByDescending(t => t.Id)
                .Include(t => t.Messages)
                .ToListAsync();

            var customerIds = threads.Select(t => t.CustomerId).Distinct().ToList();
            var customers = await _userManager.Users
                .Where(u => customerIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id);

            var model = threads.Select(t => new ChatDashboardViewModel
            {
                Thread = t,
                Customer = customers.ContainsKey(t.CustomerId) ? customers[t.CustomerId] : null,
                UnreadCount = t.Messages.Count(m => !m.IsStaff && !m.IsRead)
            }).ToList();

            return View(model);
        }
    }
}
