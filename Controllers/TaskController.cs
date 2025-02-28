using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyApi.Data;
using MyApi.Models;
using MyApi.Models.DTOs;
using System.Security.Claims;

namespace MyApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class TaskController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public TaskController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<TaskDto>>> GetTasks([FromQuery] Models.TaskStatus? status = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var query = _context.Tasks
                .Include(t => t.User)
                .Where(t => t.UserId == userId);

            if (status.HasValue)
            {
                query = query.Where(t => t.Status == status.Value);
            }

            var tasks = await query
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new TaskDto
                {
                    Id = t.Id,
                    Title = t.Title,
                    Description = t.Description,
                    Status = t.Status,
                    DueDate = t.DueDate,
                    CreatedAt = t.CreatedAt,
                    UpdatedAt = t.UpdatedAt,
                    UserId = t.UserId,
                    UserName = t.User.UserName
                })
                .ToListAsync();

            return Ok(tasks);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<TaskDto>> GetTask(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var task = await _context.Tasks
                .Include(t => t.User)
                .Where(t => t.UserId == userId && t.Id == id)
                .Select(t => new TaskDto
                {
                    Id = t.Id,
                    Title = t.Title,
                    Description = t.Description,
                    Status = t.Status,
                    DueDate = t.DueDate,
                    CreatedAt = t.CreatedAt,
                    UpdatedAt = t.UpdatedAt,
                    UserId = t.UserId,
                    UserName = t.User.UserName
                })
                .FirstOrDefaultAsync();

            if (task == null)
            {
                return NotFound();
            }

            return Ok(task);
        }

        [HttpPost]
        public async Task<ActionResult<TaskDto>> CreateTask(CreateTaskDto createTaskDto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var task = new Models.Task
            {
                Title = createTaskDto.Title,
                Description = createTaskDto.Description,
                Status = Models.TaskStatus.Todo,
                DueDate = createTaskDto.DueDate,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                UserId = userId
            };

            _context.Tasks.Add(task);
            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetTask),
                new { id = task.Id },
                new TaskDto
                {
                    Id = task.Id,
                    Title = task.Title,
                    Description = task.Description,
                    Status = task.Status,
                    DueDate = task.DueDate,
                    CreatedAt = task.CreatedAt,
                    UpdatedAt = task.UpdatedAt,
                    UserId = task.UserId,
                    UserName = User.Identity?.Name
                });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTask(int id, UpdateTaskDto updateTaskDto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var task = await _context.Tasks
                .Where(t => t.UserId == userId && t.Id == id)
                .FirstOrDefaultAsync();

            if (task == null)
            {
                return NotFound();
            }

            task.Title = updateTaskDto.Title;
            task.Description = updateTaskDto.Description;
            task.Status = updateTaskDto.Status;
            task.DueDate = updateTaskDto.DueDate;
            task.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TaskExists(id))
                {
                    return NotFound();
                }
                throw;
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTask(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var task = await _context.Tasks
                .Where(t => t.UserId == userId && t.Id == id)
                .FirstOrDefaultAsync();

            if (task == null)
            {
                return NotFound();
            }

            _context.Tasks.Remove(task);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool TaskExists(int id)
        {
            return _context.Tasks.Any(e => e.Id == id);
        }
    }
} 