using System.ComponentModel.DataAnnotations;

namespace MyApi.Models.DTOs
{
    public class CreateTaskDto
    {
        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        public string Description { get; set; }

        public DateTime? DueDate { get; set; }
    }

    public class UpdateTaskDto
    {
        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        public string Description { get; set; }

        public TaskStatus Status { get; set; }

        public DateTime? DueDate { get; set; }
    }

    public class TaskDto
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public TaskStatus Status { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
    }
} 