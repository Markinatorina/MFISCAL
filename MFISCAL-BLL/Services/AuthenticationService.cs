using MFISCAL_BLL.Loggers;
using MFISCAL_BLL.Models;
using MFISCAL_BLL.Utils;
using MFISCAL_DAL.Models;
using MFISCAL_DAL.Repositories;
using MFISCAL_INF.Environments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace MFISCAL_BLL.Services
{
    public class AuthenticationService
    {
        private readonly IIdentifiableRepository<UserDB> _userRepository;
        private readonly IIdentifiableRepository<LoginTokenDB> _jwtRepository;
        private readonly InviteCodeService _inviteCodeService;
        private readonly string _jwtIssuer;
        private readonly string _jwtAudience;
        private readonly byte[] _jwtKeyBytes;
        private readonly ServiceLogger _serviceLogger;

        public AuthenticationService(
            IIdentifiableRepository<UserDB> userRepository,
            IIdentifiableRepository<LoginTokenDB> jwtRepository,
            InviteCodeService inviteCodeService,
            IConfiguration configuration,
            ILocalEnvironment env,
            ServiceLogger serviceLogger)
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _jwtRepository = jwtRepository ?? throw new ArgumentNullException(nameof(jwtRepository));
            _jwtIssuer = env.Values.JwtIssuerName;
            _jwtAudience = env.Values.JwtIssuerAudience;
            _jwtKeyBytes = env.GetSigningKeyBytes();
            _inviteCodeService = inviteCodeService ?? throw new ArgumentNullException(nameof(inviteCodeService));
            _serviceLogger = serviceLogger ?? throw new ArgumentNullException(nameof(serviceLogger));
        }

        public async Task<LoginResponseDTO> LoginAsync(LoginRequestDTO request, CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            UserDB? user = await _userRepository.GetAllAsReadOnly()
                .FirstOrDefaultAsync(u => u.Username == request.Username, cancellationToken);
            if (user == null || !PasswordUtils.VerifyPassword(request.Password, user.PasswordHash, user.PasswordSalt))
            {
                throw new InvalidOperationException("Username doesn't exist or password is incorrect.");
            }

            LoginTokenDB token = await GenerateJwtTokenAsync(user);

            _serviceLogger.WriteLog($"User {user.Username} logged in successfully.", user.Id);

            return new LoginResponseDTO { Token = token.TokenString };
        }

        public async Task<LoginTokenDB> GenerateJwtTokenAsync(UserDB user)
        {
            Guid jwtId = Guid.NewGuid();
            DateTime issuedAt = DateTime.UtcNow;
            DateTime expiresAt = issuedAt.AddHours(2);

            List<Claim> claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Username)
            };

            if (user.IsAdmin)
                claims.Add(new Claim(ClaimTypes.Role, "Admin"));
            claims.Add(new Claim("inviteCode", user.InviteCodeId?.ToString() ?? string.Empty));

            SymmetricSecurityKey key = new SymmetricSecurityKey(_jwtKeyBytes);
            SigningCredentials creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            JwtSecurityToken token = new JwtSecurityToken(
                issuer: _jwtIssuer,
                audience: _jwtAudience,
                claims: claims,
                notBefore: issuedAt,
                expires: expiresAt,
                signingCredentials: creds);

            string tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            LoginTokenDB LoginTokenDB = new LoginTokenDB
            {
                Id = jwtId,
                UserId = user.Id,
                Username = user.Username,
                IsAdmin = user.IsAdmin,
                TokenString = tokenString,
                IssuedAt = issuedAt,
                ExpiresAt = expiresAt
            };
            _jwtRepository.Insert(LoginTokenDB);
            await _jwtRepository.CommitAsync();

            return LoginTokenDB;
        }

        public async Task<Guid> RegisterAsync(RegisterRequestDTO request, CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            bool isValidInviteCode = _inviteCodeService.IsValidInviteCode(request.InviteCode);
            if (!isValidInviteCode)
            {
                throw new InvalidOperationException("Invalid or expired invite code.");
            }

            bool exists = await _userRepository.GetAllAsReadOnly()
                .AnyAsync(u => u.Username == request.Username, cancellationToken);
            if (exists)
            {
                throw new InvalidOperationException("Username already exists.");
            }

            if (request.InviteCode == Guid.Empty)
            {
                throw new InvalidOperationException("Invite code is required.");
            }

            byte[] salt = PasswordUtils.GenerateSalt();
            byte[] hash = PasswordUtils.HashPassword(request.Password, salt);

            UserDB user = new UserDB
            {
                Id = Guid.NewGuid(),
                Username = request.Username,
                PasswordHash = hash,
                PasswordSalt = salt,
                IsAdmin = false,
                InviteCodeId = request.InviteCode,
            };

            _userRepository.Insert(user);
            await _userRepository.CommitAsync();

            _serviceLogger.WriteLog($"User {user.Username} registered successfully.", user.Id);

            return user.Id;
        }
    }
}
