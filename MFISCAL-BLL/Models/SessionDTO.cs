using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MFISCAL_BLL.Models
{
    public class SessionDTO
    {
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
        public bool IsBanned { get; set; }
        public bool IsWhitelisted { get; set; }
        public List<string> Roles { get; set; } = new();
        public Dictionary<string, List<string>> Claims { get; set; } = new();
        public DateTime IssuedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool LoggedIn { get; set; } = false;
    }
}
