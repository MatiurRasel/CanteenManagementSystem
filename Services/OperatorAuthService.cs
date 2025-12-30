//using CanteenManagementSystem.Models;
//using Microsoft.Extensions.Options;
//using System.Text.Json;

//namespace CanteenManagementSystem.Services
//{
//    public class OperatorAuthService
//    {
//        private readonly IHttpContextAccessor _httpContextAccessor;
//        private readonly ILogger<OperatorAuthService> _logger;
//        private readonly OperatorSettings _settings;
//        private const string SessionKey = "OperatorSession";

//        public OperatorAuthService(
//            IHttpContextAccessor httpContextAccessor,
//            IOptions<OperatorSettings> settings,
//            ILogger<OperatorAuthService> logger)
//        {
//            _httpContextAccessor = httpContextAccessor;
//            _logger = logger;
//            _settings = settings.Value;
//        }

//        public bool Authenticate(string userId, string password)
//        {
//            try
//            {
//                var operatorUser = _settings.Operators
//                    .FirstOrDefault(o =>
//                        o.UserId.Equals(userId, StringComparison.OrdinalIgnoreCase) &&
//                        o.IsActive);

//                if (operatorUser == null)
//                    return false;

//                // Verify password using BCrypt
//                var isValid = BCrypt.Net.BCrypt.Verify(password, operatorUser.PasswordHash);

//                if (isValid)
//                {
//                    var session = new OperatorSession
//                    {
//                        UserId = operatorUser.UserId,
//                        Name = operatorUser.Name,
//                        Role = operatorUser.Role,
//                        LoginTime = DateTime.UtcNow,
//                        LastActivity = DateTime.UtcNow
//                    };

//                    SetSession(session);
//                    _logger.LogInformation("Operator {UserId} logged in successfully", userId);
//                    return true;
//                }

//                return false;
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error authenticating operator {UserId}", userId);
//                return false;
//            }
//        }

//        public bool IsAuthenticated()
//        {
//            var session = GetSession();
//            if (session == null)
//                return false;

//            // Check session timeout
//            if (session.LastActivity.HasValue &&
//                (DateTime.UtcNow - session.LastActivity.Value).TotalMinutes > _settings.SessionTimeout)
//            {
//                Logout();
//                return false;
//            }

//            // Update last activity
//            session.LastActivity = DateTime.UtcNow;
//            SetSession(session);

//            return true;
//        }

//        public OperatorSession GetCurrentUser()
//        {
//            return GetSession();
//        }

//        public void Logout()
//        {
//            var context = _httpContextAccessor.HttpContext;
//            context?.Session.Remove(SessionKey);
//        }

//        private void SetSession(OperatorSession session)
//        {
//            var context = _httpContextAccessor.HttpContext;
//            if (context != null)
//            {
//                var sessionJson = JsonSerializer.Serialize(session);
//                context.Session.SetString(SessionKey, sessionJson);
//            }
//        }

//        private OperatorSession GetSession()
//        {
//            var context = _httpContextAccessor.HttpContext;
//            if (context != null)
//            {
//                var sessionJson = context.Session.GetString(SessionKey);
//                if (!string.IsNullOrEmpty(sessionJson))
//                {
//                    return JsonSerializer.Deserialize<OperatorSession>(sessionJson);
//                }
//            }
//            return null;
//        }

//        public bool HasPermission(string permission)
//        {
//            var session = GetSession();
//            if (session == null)
//                return false;

//            var operatorUser = _settings.Operators
//                .FirstOrDefault(o => o.UserId.Equals(session.UserId, StringComparison.OrdinalIgnoreCase));

//            return operatorUser?.Permissions?.Contains(permission) ?? false;
//        }
//    }

//    public class OperatorSettings
//    {
//        public List<OperatorUser> Operators { get; set; }
//        public int SessionTimeout { get; set; }
//        public int MaxLoginAttempts { get; set; }
//        public int LockoutDuration { get; set; }
//    }

//    public class OperatorUser
//    {
//        public string UserId { get; set; }
//        public string Name { get; set; }
//        public string PasswordHash { get; set; }
//        public string Role { get; set; }
//        public bool IsActive { get; set; }
//        public List<string> Permissions { get; set; }
//    }
//}
