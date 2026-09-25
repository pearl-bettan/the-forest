using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Net.Http.Json;
using System.Net.Http;
using System.Security.Claims;
using System.Web;
using Microsoft.AspNetCore.Components;
using UsersManager.Shared;


namespace UsersManager.Client
{
    // ============================================================
    // מימוש ההתחברות בצד הלקוח, מול פורטל"מ ומול השרת.
    //
    // תהליך הכניסה המלא:
    //   1. המשתמש מגיע למחולל בלי טוקן בכתובת
    //   2. מפנים אותו לעמוד ההתחברות של פורטל"מ
    //   3. פורטל"מ מחזיר אותו לכאן עם ?token=... בכתובת
    //   4. הטוקן נשלח לשרת, שמוודא אותו מול פורטל"מ
    //   5. השרת מחזיר טוקן משלו, שנשמר ב-localStorage
    //   6. הכתובת מנוקה מהפרמטר, כדי שהטוקן לא יישאר בהיסטוריה
    // ============================================================
    public class AuthenticationService : IAuthenticationService
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _options;
        private readonly AuthenticationStateProvider _authStateProvider;
        private readonly AuthenticationState _anonymous;
        private readonly NavigationManager _navigationManager;
        private readonly IConfiguration _configuration;

        public event Action OnAuthenticationStateChanged;

        public AuthenticationService(HttpClient client, AuthenticationStateProvider authStateProvider,
            NavigationManager navigationManager, IConfiguration configuration)
        {
            _httpClient = client;
            _options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            _authStateProvider = authStateProvider;
            _navigationManager = navigationManager;
            _configuration = configuration;
        }
        
        // ============================================================
        // התנתקות מלאה, בשלושה שלבים:
        //   1. פסילת הטוקן בשרת, כדי שלא יהיה שמיש גם אם הועתק
        //   2. מחיקתו מ-localStorage ועדכון מצב ההזדהות
        //   3. חזרה לעמוד הראשי של פורטל"מ
        //
        // רק הראשון מנטרל את הטוקן באמת. השניים האחרים דואגים
        // לכך שהממשק יתעדכן ושהמשתמש לא יישאר במסך ריק
        // ============================================================
        public async Task Logout()
        {
            var get = await _httpClient.GetAsync("api/auth/logout");
            await ((AuthStateProvider)_authStateProvider).NotifyUserLogout();
            OnAuthenticationStateChanged?.Invoke();
            _navigationManager.NavigateTo($"{_configuration["portelem:mainUrl"]}");
        }
        
        // ============================================================
        // ההתחברות עצמה.
        //
        // אין טוקן בכתובת - מפנים לפורטל"מ ומחזירים false. כלומר
        // false כאן אינו בהכרח כישלון, אלא גם "עוד לא סיימנו".
        //
        // כתובת ההפניה שונה בין סביבת פיתוח לסביבת ייצור: בפיתוח
        // מעבירים לפורטל"מ גם את הכתובת לחזור אליה, כי היא משתנה
        // בין מפתחים.
        //
        // בסוף הכתובת מנוקה מהפרמטר, בלי טעינה מחדש. אחרת הטוקן
        // של פורטל"מ היה נשאר בשורת הכתובת ובהיסטוריה
        // ============================================================
        public async Task<bool> LoginWithPortelem()
        {
            if (string.IsNullOrEmpty(GetQueryParm("token")))
            {
                if (_configuration["portelem:type"] == "inDevelop")
                {
                    var baseUri = _navigationManager.Uri;
                    _navigationManager.NavigateTo(
                        $"{_configuration["portelem:loginUrl"]}?id={_configuration["portelem:serviceId"]}&link={baseUri}");
                    return false;
                }
                _navigationManager.NavigateTo(
                    $"{_configuration["portelem:loginUrl"]}{_configuration["portelem:serviceId"]}");
                return false;
            }

            var response = await _httpClient.PostAsJsonAsync($"api/auth/portelemLogin/", GetQueryParm("token"));
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine(await response.Content.ReadAsStringAsync());
                return false;
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
            if (tokenResponse?.Token == null)
            {
                Console.WriteLine("Failed to get token from response");
                return false;
            }
            
            await ((AuthStateProvider)_authStateProvider).NotifyAuthenticationStateChanged(tokenResponse.Token);
            OnAuthenticationStateChanged?.Invoke();
            // Extract the base path (without the query string)
            var uriWithoutQuery = _navigationManager.Uri.Split('?')[0];
            // Navigate to the same path without the query string
            _navigationManager.NavigateTo(uriWithoutQuery, forceLoad: false);
            return true;
        }
        
        // מבנה תשובת השרת ל-portelemLogin. השרת מחזיר עטיפה
        // { token: "..." } ולא מחרוזת חשופה
        public class TokenResponse
        {
            public string Token { get; set; }
        }

        // שולף פרמטר משורת הכתובת. מחזיר מחרוזת ריקה ולא null
        // כשהפרמטר חסר, כדי שהקורא לא יצטרך לבדוק null
        string GetQueryParm(string parmName)
        {
            var uriBuilder = new UriBuilder(_navigationManager.Uri);
            var q = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);
            return q[parmName] ?? "";
        }

        // מרכיב אובייקט משתמש מתוך תביעות הטוקן השמור.
        // המידע מגיע מהטוקן ולא מפנייה לשרת, ולכן זה מיידי
        public async Task<User> GetUserFromClaimAsync()
        {
            var authenticationState = await _authStateProvider.GetAuthenticationStateAsync();
            var user = authenticationState.User;

            if (user.Identity.IsAuthenticated)
            {
                var userDto = new User
                {
                    FirstName = user.FindFirst(c => c.Type == "given_name")?.Value,
                    LastName = user.FindFirst(c => c.Type == "family_name")?.Value,
                    Email = user.FindFirst(c => c.Type == "email")?.Value,
                    Id = Convert.ToInt16(user.FindFirst(c => c.Type == "nameid")?.Value),
                };

                return userDto;
            }
            else
            {
                return null;
            }
        }
    }
}