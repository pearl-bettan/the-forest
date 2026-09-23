using UsersManager.Shared;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Data;
using Data;
using Microsoft.Extensions.Logging;

namespace UsersManager.Server
{
    //ניהול תהליכי התחברות
    public class AuthRepository
    {
        private readonly TokenService _tokenService;
        private readonly DbRepository _db;
        private readonly PasswordService _passwordService;
        private readonly IHttpContextAccessor _context;
        private readonly ITokenBlacklistService _tokenBlacklistService;
        private readonly ILogger<AuthRepository> _logger;

        public AuthRepository(TokenService tokenService, DbRepository db, PasswordService passwordService,
            IHttpContextAccessor contextAccessor, ITokenBlacklistService tokenBlacklistService, IConfiguration config, ILogger<AuthRepository> logger)
        {
            _db = db;
            _tokenService = tokenService;
            _passwordService = passwordService;
            _context = contextAccessor;
            _tokenBlacklistService = tokenBlacklistService;
            _logger = logger;
        }


        //התחברות
        public async Task<string> Login(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                _logger.LogWarning("Login: Empty email provided");
                return AuthConstants.NoUser;
            }

            try
            {
                object user = new
                {
                    Email = email
                };
                //שליפת פרטי המשתמש
                //COLLATE NOCASE כדי ששינוי אותיות גדולות/קטנות במייל
                //שמגיע מהפורטל"מ לא ייצור משתמש חדש
                string query = "SELECT Id, FirstName, LastName, Email FROM Users WHERE Email = @Email COLLATE NOCASE";
                UserFromDB userFromDB = (await _db.GetRecordsAsync<UserFromDB>(query, user)).FirstOrDefault();

                //אם המשתמש לא קיים
                if (userFromDB == null)
                {
                    _logger.LogInformation("Login: User not found with email {Email}", email);
                    return AuthConstants.NoUser;
                }

                var token = CreateToken(userFromDB); //יצירת TOKEN
                _logger.LogInformation("Login: User {UserId} logged in successfully", userFromDB.Id);
                return token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login: Error during login for email {Email}", email);
                return AuthConstants.NoUser;
            }
        }

        //הרשמה
        public async Task<string> Signup(PortelemUser newUser)
        {
            if (newUser == null || string.IsNullOrWhiteSpace(newUser.Email))
            {
                _logger.LogWarning("Signup: Invalid user data provided");
                return AuthConstants.ErrorInsertToDb;
            }

            try
            {
                //הזהות שמגיעה מהפורטל"מ היא PortelemId, לא המייל. לטבלה יש
                //אילוץ ייחודיות על שני השדות, ולכן אם המייל בפורטל"מ השתנה
                //ההכנסה הייתה נכשלת על PortelemId והמשתמש היה ננעל בחוץ:
                //ההתחברות לא מוצאת אותו לפי המייל החדש, וההרשמה לא יכולה
                //ליצור אותו כי המזהה כבר תפוס.
                //לכן קודם מחפשים לפי המזהה, ואם הוא קיים מרעננים את הפרטים
                UserFromDB byPortelemId = (await _db.GetRecordsAsync<UserFromDB>(
                        "SELECT Id, FirstName, LastName, Email FROM Users WHERE PortelemId = @PortelemId",
                        newUser))
                    .FirstOrDefault();

                if (byPortelemId != null)
                {
                    await _db.SaveDataAsync(
                        "UPDATE Users SET Email = @Email, FirstName = @FirstName, LastName = @LastName " +
                        "WHERE PortelemId = @PortelemId", newUser);

                    byPortelemId.Email = newUser.Email;
                    byPortelemId.FirstName = newUser.FirstName;
                    byPortelemId.LastName = newUser.LastName;

                    _logger.LogInformation(
                        "Signup: existing user {UserId} matched by PortelemId, details refreshed", byPortelemId.Id);

                    return CreateToken(byPortelemId);
                }

                //שורה עם אותו מייל אבל בלי המזהה - משלימים לה אותו.
                //אין סכנת התנגשות, כי כבר ווידאנו שאף שורה לא מחזיקה במזהה
                UserFromDB byEmail = (await _db.GetRecordsAsync<UserFromDB>(
                        "SELECT Id, FirstName, LastName, Email FROM Users WHERE Email = @Email COLLATE NOCASE",
                        newUser))
                    .FirstOrDefault();

                if (byEmail != null)
                {
                    object fix = new
                    {
                        ID = byEmail.Id,
                        PortelemId = newUser.PortelemId,
                        FirstName = newUser.FirstName,
                        LastName = newUser.LastName
                    };

                    await _db.SaveDataAsync(
                        "UPDATE Users SET PortelemId = @PortelemId, FirstName = @FirstName, " +
                        "LastName = @LastName WHERE Id = @ID", fix);

                    byEmail.FirstName = newUser.FirstName;
                    byEmail.LastName = newUser.LastName;

                    _logger.LogInformation(
                        "Signup: existing user {UserId} matched by email, PortelemId filled in", byEmail.Id);

                    return CreateToken(byEmail);
                }

                //משתמש חדש לגמרי - הכנסה לDB
                string query =
                    "INSERT INTO Users (Email,FirstName,LastName,PortelemId) VALUES (@Email,@FirstName,@LastName,@PortelemId)";

                int userID = await _db.InsertReturnIdAsync(query, newUser);
                if (userID == 0)
                {
                    _logger.LogError("Signup: Failed to insert user to database for email {Email}", newUser.Email);
                    return AuthConstants.ErrorInsertToDb;
                }

                UserFromDB userFromDb = new UserFromDB()
                {
                    Email = newUser.Email,
                    FirstName = newUser.FirstName,
                    LastName = newUser.LastName,
                    Id = userID,
                };

                var token = CreateToken(userFromDb); //יצירת TOKEN
                _logger.LogInformation("Signup: User {UserId} signed up successfully", userID);
                return token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Signup: Error during signup for email {Email}", newUser.Email);
                return AuthConstants.ErrorInsertToDb;
            }
        }


        //עדכון TOKEN שעומד לפוג
        public async Task<string> RefreshToken()
        {
            try
            {
                var authorizationHeader = _context.HttpContext.Request.Headers["Authorization"].ToString();
                var token = authorizationHeader.StartsWith(AuthConstants.BearerPrefix)
                    ? authorizationHeader.Substring(AuthConstants.BearerPrefix.Length).Trim()
                    : authorizationHeader;

                if (string.IsNullOrWhiteSpace(token))
                {
                    _logger.LogWarning("RefreshToken: Empty token provided");
                    return AuthConstants.TokenInvalid;
                }

                //האם הטוקן תקין
                var principal = _tokenService.ValidateToken(token);
                if (principal == null)
                {
                    _logger.LogWarning("RefreshToken: Invalid token provided");
                    await Logout();
                    return AuthConstants.TokenInvalid;
                }

                string newToken = _tokenService.RefreshToken(token);
                if (string.IsNullOrWhiteSpace(newToken))
                {
                    _logger.LogWarning("RefreshToken: Token refresh returned null or empty");
                    return AuthConstants.TokenInvalid;
                }

                _logger.LogInformation("RefreshToken: Token refreshed successfully");
                return newToken;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RefreshToken: Error during token refresh");
                return AuthConstants.TokenInvalid;
            }
        }

        //התנתקות
        public async Task Logout()
        {
            try
            {
                //קבלת מזהה משתמש
                var authorizationHeader = _context.HttpContext.Request.Headers["Authorization"].ToString();
                var token = authorizationHeader.StartsWith(AuthConstants.BearerPrefix)
                    ? authorizationHeader.Substring(AuthConstants.BearerPrefix.Length).Trim()
                    : authorizationHeader;
                
                //הוספה לבלאקליסט
                if (!string.IsNullOrWhiteSpace(token))
                {
                    _tokenBlacklistService.AddToBlacklist(token);
                    _logger.LogInformation("Logout: Token added to blacklist");
                }
                else
                {
                    _logger.LogWarning("Logout: No token found in authorization header");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Logout: Error during logout");
            }
        }

        //קבלת פרטי משתמש מהיוזר
        public async Task<User> GetUser()
        {
            try
            {
                var authorizationHeader = _context.HttpContext.Request.Headers["Authorization"].ToString();
                var token = authorizationHeader.StartsWith(AuthConstants.BearerPrefix)
                    ? authorizationHeader.Substring(AuthConstants.BearerPrefix.Length).Trim()
                    : authorizationHeader;

                //אין טוקן
                if (string.IsNullOrWhiteSpace(token))
                {
                    _logger.LogWarning("GetUser: No token provided");
                    return null;
                }

                var principal = _tokenService.ValidateToken(token);

                //אם טוקן לא תקין
                if (principal == null)
                {
                    _logger.LogWarning("GetUser: Invalid token");
                    await Logout();
                    return null;
                }

                //אם טוקן בבלאקליסט
                if (_tokenBlacklistService.IsBlacklisted(token))
                {
                    _logger.LogWarning("GetUser: Token is blacklisted");
                    return null;
                }

                //פירוק לClaims
                var userIdClaim = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrWhiteSpace(userIdClaim) || !short.TryParse(userIdClaim, out short userId))
                {
                    _logger.LogWarning("GetUser: Invalid user ID in token");
                    return null;
                }

                User user = new User()
                {
                    Id = userId,
                    Email = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value ?? string.Empty,
                    FirstName = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.GivenName)?.Value ?? string.Empty,
                    LastName = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Surname)?.Value ?? string.Empty
                };

                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetUser: Error retrieving user from token");
                return null;
            }
        }

        //יצירת Claims 
        private string CreateToken(UserFromDB user)
        {
            if (user == null)
            {
                _logger.LogError("CreateToken: User is null");
                throw new ArgumentNullException(nameof(user));
            }

            var claims = new List<Claim> // יצירת מזהה משתמש
            {
                new Claim(JwtRegisteredClaimNames.NameId, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.GivenName, user.FirstName ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.FamilyName, user.LastName ?? string.Empty),
            };

            var token = _tokenService.GenerateToken(claims); //יצירת TOKEN
            return token;
        }
    }
}