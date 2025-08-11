using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MFISCAL_DAL.Models
{
    public class UserDB : IIdentifiableDB
    {
        public Guid Id { get; set; }
        public required string Username { get; set; }
        public required byte[] PasswordHash { get; set; }
        public required byte[] PasswordSalt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public required bool IsAdmin { get; set; }
        public Guid? InviteCodeId { get; set; }
    }
}
