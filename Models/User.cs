using Microsoft.AspNetCore.Identity;

namespace MyApi.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Bio { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation property for tasks
        public virtual ICollection<Task> Tasks { get; set; } = new List<Task>();
    }
}
