using MFISCAL_BLL.Loggers;
using MFISCAL_BLL.Models;
using MFISCAL_DAL.Models;
using MFISCAL_DAL.Repositories;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace MFISCAL_BLL.Services
{
    public class UserService
    {

        private readonly IIdentifiableRepository<UserDB> _userRepository;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ServiceLogger _serviceLogger;

        public UserService(IIdentifiableRepository<UserDB> userRepository, IHttpContextAccessor httpContextAccessor, ServiceLogger serviceLogger)
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            _serviceLogger = serviceLogger ?? throw new ArgumentNullException(nameof(serviceLogger));
        }

        public Guid GetSessionUserId()
        {
            HttpContext httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null && httpContext.Items["SessionDTO"] is SessionDTO sessionDto)
            {
                return sessionDto.UserId;
            }
            else
            {
                throw new UnauthorizedAccessException("SessionDTO not found in HttpContext.");
            }
        }
        public SessionDTO GetSessionClaims()
        {
            HttpContext httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null && httpContext.Items["SessionDTO"] is SessionDTO sessionDto)
            {
                return sessionDto;
            }
            else
            {
                throw new UnauthorizedAccessException("Session claims not found in HttpContext.");
            }
        }

        public bool IsSessionUserAdmin()
        {
            HttpContext httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null && httpContext.Items["SessionDTO"] is SessionDTO sessionDto)
            {
                return sessionDto.IsAdmin;
            }
            else
            {
                throw new UnauthorizedAccessException("SessionDTO not found in HttpContext.");
            }
        }

        public UserDTO GetSessionUser()
        {
            return MapUserDTOWithRelationships(GetSessionUserDB());
        }

        public static UserDTO MapUserDTOWithRelationships(UserDB db)
        {
            if (db == null)
                throw new ArgumentNullException(nameof(db));

            UserDTO userDTO = MapUserDTO(db);

            return userDTO;
        }
        public static UserDTO MapUserDTO(UserDB db)
        {
            if (db == null)
                throw new ArgumentNullException(nameof(db));
            return new UserDTO
            {
                Id = db.Id,
                Username = db.Username,
                IsAdmin = db.IsAdmin,
                CreatedAt = db.CreatedAt
            };
        }

        public UserDB GetSessionUserDB()
        {
            HttpContext httpContext = _httpContextAccessor.HttpContext;
            Guid sessionUserId = new();
            if (httpContext != null && httpContext.Items["SessionDTO"] is SessionDTO sessionDto)
            {
                sessionUserId = sessionDto.UserId;
            }
            else
            {
                throw new UnauthorizedAccessException("SessionDTO not found in HttpContext.");
            }
            var userDb = _userRepository.GetById(sessionUserId);
            return userDb;
        }

        public async Task<UserDTO> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var userDb = await _userRepository.GetByIdAsync(id);
            return MapUserDTOWithRelationships(userDb);
        }

        public List<UserDTO> GetAllUsers()
        {
            var userDbs = _userRepository.GetAllAsReadOnly().ToList();
            return userDbs.Select(MapUserDTOWithRelationships).ToList();
        }

        public void DeleteUser(Guid id)
        {
            _userRepository.DeleteById(id);
            _userRepository.Commit();
            _serviceLogger.WriteLog($"User deleted: {id}", id);
        }

        public async Task DeleteUserAsync(Guid id, CancellationToken cancellationToken = default)
        {
            await _userRepository.DeleteByIdAsync(id);
            await _userRepository.CommitAsync();
            _serviceLogger.WriteLog($"User deleted: {id}", id);
        }

    }
}
