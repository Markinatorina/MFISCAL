using MFISCAL_DAL.Models;
using MFISCAL_BLL.Utils;
using MFISCAL_DAL.Repositories;
using MFISCAL_INF.Environments;
using MFISCAL_BLL.Loggers;
using System;
using System.Linq;

namespace MFISCAL_BLL.Services
{
    public class SeedingService
    {
        private readonly IIdentifiableRepository<UserDB> _userRepo;
        private readonly IIdentifiableRepository<InviteCodeDB> _inviteCodeRepo;
        private readonly ILocalEnvironment _env;
        private readonly ServiceLogger _serviceLogger;

        public SeedingService(
            IIdentifiableRepository<UserDB> userRepo,
            IIdentifiableRepository<InviteCodeDB> inviteCodeRepo,
            ILocalEnvironment env,
            ServiceLogger serviceLogger)
        {
            _userRepo = userRepo ?? throw new ArgumentNullException(nameof(userRepo));
            _inviteCodeRepo = inviteCodeRepo ?? throw new ArgumentNullException(nameof(inviteCodeRepo));
            _env = env ?? throw new ArgumentNullException(nameof(env));
            _serviceLogger = serviceLogger ?? throw new ArgumentNullException(nameof(serviceLogger));
        }

        public void Seed()
        {
            SeedAdminUser();
        }

        private Guid SeedAdminUser()
        {
            string adminUsername = _env.Values.AdminUsername;
            string adminPassword = _env.Values.AdminPassword;

            UserDB? adminUser = _userRepo.GetAllAsReadOnly().FirstOrDefault(u => u.Username == adminUsername);
            if (adminUser == null)
            {
                byte[] salt = PasswordUtils.GenerateSalt();
                byte[] hash = PasswordUtils.HashPassword(adminPassword, salt);
                adminUser = new UserDB
                {
                    Id = Guid.NewGuid(),
                    Username = adminUsername,
                    PasswordHash = hash,
                    PasswordSalt = salt,
                    IsAdmin = true,
                    CreatedAt = DateTime.UtcNow
                };
                _userRepo.Insert(adminUser);
                _userRepo.Commit();
                _serviceLogger.WriteLog($"Admin user '{adminUsername}' created during seeding.", adminUser.Id);
            }
            return adminUser.Id;
        }
    }
}
