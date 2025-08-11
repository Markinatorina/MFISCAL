﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MFISCAL_BLL.Models
{
    public class RegisterRequestDTO
    {
        public required string Username { get; set; }
        public required string Password { get; set; }
        public required Guid InviteCode { get; set; }
    }
}
