using System.Collections.Generic;

namespace GearsHouse.Models
{
    public class UserRoleViewModel
    {
        public string UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public IList<string> CurrentRoles { get; set; } = new List<string>();
        public string SelectedRole { get; set; }
    }
}