using MFISCAL_DAL.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MFISCAL_DAL.Contexts
{
    public class BaseDataContext : DbContext
    {
        public BaseDataContext(DbContextOptions<BaseDataContext> options) : base(options)
        {
            //todo
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            //todo
        }

        public DbSet<UserDB> Users { get; set; }
        public DbSet<InviteCodeDB> InviteCodes { get; set; }
        public DbSet<LoginTokenDB> LoginTokens { get; set; }
        public DbSet<InvoiceDB> Invoices { get; set; }
    }
}
