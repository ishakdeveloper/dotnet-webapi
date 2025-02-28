using Microsoft.AspNetCore.Identity;

namespace MyApi.Models.Entities
{
    public class ApplicationUser : IdentityUser
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Bio { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation property for tasks
        public virtual ICollection<TodoTask> Tasks { get; set; } = new List<TodoTask>();
    }
} 