using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;

namespace UsersManager.Client
{
    // ============================================================
    // מקור האמת של Blazor לשאלה "מי מחובר עכשיו".
    //
    // כל <AuthorizeView> ו-[Authorize] במערכת שואלים את המחלקה
    // הזו, והיא עונה מתוך הטוקן ששמור ב-localStorage.
    //
    // כאן גם מתבצע חידוש הטוקן: כשהוא מתקרב לפקיעה, המחלקה
    // מבקשת טוקן חדש מהשרת לפני שהיא עונה. כך משתמש באמצע
    // עבודה אינו נזרק פתאום החוצה
    // ============================================================
    public class AuthStateProvider : AuthenticationStateProvider
    {
        private readonly HttpClient _httpClient;
        private readonly ILocalStorageService _localStorage;
        private readonly AuthenticationState _anonymous;
        private readonly IConfiguration _configuration;

        public AuthStateProvider(HttpClient httpClient, ILocalStorageService localStorage, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _localStorage = localStorage;
            _anonymous = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            _configuration = configuration;
        }

        // ============================================================
        // מחזיר את מצב ההזדהות הנוכחי. נקרא על ידי Blazor.
        //
        // הסדר: קריאת הטוקן מהאחסון, חידוש אם צריך, והרכבת
        // זהות מהתביעות שבו.
        //
        // בסוף מוצבת כותרת Authorization על ה-HttpClient, וזו
        // הסיבה שכל קריאת API בהמשך נושאת את הטוקן מאליה בלי
        // שאף מסך יצטרך לצרף אותו
        // ============================================================
        //בדיקה האם יש משתמש מחובר
        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var token = await _localStorage.GetItemAsync(GetTokenName());

            if (string.IsNullOrWhiteSpace(token))
            {
                return _anonymous;
            }


            if (IsTokenExpired(token))
            {
                //ניסיון יצירת טוקן חדש - אם עומד לפוג
                var refreshResponse = await _httpClient.GetAsync("api/auth/refresh");
                if (refreshResponse.IsSuccessStatusCode)
                {
                    var newToken = await refreshResponse.Content.ReadAsStringAsync();
                    await _localStorage.SetItemAsync(GetTokenName(), newToken);
                    token = newToken;
                }
                else
                {
                    return _anonymous; // אם לא הצליח לרענן - פג התוקף
                }
            }
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(JwtParser.ParseClaimsFromJwt(token), "jwtAuthType")));
        }

        // נקרא אחרי התחברות מוצלחת: שומר את הטוקן ומודיע
        // לכל הרכיבים שמצב ההזדהות השתנה, כדי שיצטיירו מחדש
        //הודעה שהשתנתה ההתחברות
        public async Task NotifyAuthenticationStateChanged(string token)
        {
            await _localStorage.SetItemAsync(GetTokenName(), token);
            
            var authState = Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(JwtParser.ParseClaimsFromJwt(token), "jwtAuthType"))));
            NotifyAuthenticationStateChanged(authState);
        }

        // מנקה את הטוקן מהאחסון ומהכותרת, ומחזיר את המערכת
        // למצב אנונימי
        //התנתקות
        public async Task NotifyUserLogout()
        {
            await _localStorage.RemoveItemAsync(GetTokenName());
            _httpClient.DefaultRequestHeaders.Authorization = null;

            var authState = Task.FromResult(_anonymous);
            NotifyAuthenticationStateChanged(authState);
        }

        // ============================================================
        // בודק אם הטוקן פג או עומד לפוג בשעה הקרובה.
        //
        // השעה המוקדמת מכוונת: אם היינו מחכים לפקיעה ממש, משתמש
        // היה נתקל בשגיאה באמצע שמירה. כך החידוש קורה מראש.
        //
        // טוקן בלי תביעת exp נחשב פג - ברירת מחדל מחמירה
        // ============================================================
        //האם הטוקן פג תוקף
        private bool IsTokenExpired(string token)
        {
            var claims = JwtParser.ParseClaimsFromJwt(token);
            var expiry = claims.FirstOrDefault(c => c.Type == "exp")?.Value;

            if (expiry != null && long.TryParse(expiry, out long exp))
            {
                var expDate = DateTimeOffset.FromUnixTimeSeconds(exp).DateTime;
                // האם עומד לפוג בקרוב
                return expDate < DateTime.UtcNow.AddHours(1);
            }

            return true; // If there's no expiration claim, consider the token expired
        }

        // שם המפתח ב-localStorage, מורכב ממזהה השירות ומשם
        // המערכת. כך שתי מערכות שונות שרצות על אותו דומיין
        // אינן דורסות זו את הטוקן של זו
        private string GetTokenName()
        {
            return $"auth_{_configuration["portelem:serviceId"]}_{_configuration["portelem:SystemTitle"]}_Token";
        }

    }
}
