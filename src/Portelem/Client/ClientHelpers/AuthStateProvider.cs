using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;

namespace UsersManager.Client
{
    
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

        
        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var token = await _localStorage.GetItemAsync(GetTokenName());

            if (string.IsNullOrWhiteSpace(token))
            {
                return _anonymous;
            }


            if (IsTokenExpired(token))
            {
                var refreshResponse = await _httpClient.GetAsync("api/auth/refresh");
                if (refreshResponse.IsSuccessStatusCode)
                {
                    var newToken = await refreshResponse.Content.ReadAsStringAsync();
                    await _localStorage.SetItemAsync(GetTokenName(), newToken);
                    token = newToken;
                }
                else
                {
                    return _anonymous; 
                }
            }
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(JwtParser.ParseClaimsFromJwt(token), "jwtAuthType")));
        }

        public async Task NotifyAuthenticationStateChanged(string token)
        {
            await _localStorage.SetItemAsync(GetTokenName(), token);
            
            var authState = Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(JwtParser.ParseClaimsFromJwt(token), "jwtAuthType"))));
            NotifyAuthenticationStateChanged(authState);
        }

        public async Task NotifyUserLogout()
        {
            await _localStorage.RemoveItemAsync(GetTokenName());
            _httpClient.DefaultRequestHeaders.Authorization = null;

            var authState = Task.FromResult(_anonymous);
            NotifyAuthenticationStateChanged(authState);
        }

       
        private bool IsTokenExpired(string token)
        {
            var claims = JwtParser.ParseClaimsFromJwt(token);
            var expiry = claims.FirstOrDefault(c => c.Type == "exp")?.Value;

            if (expiry != null && long.TryParse(expiry, out long exp))
            {
                var expDate = DateTimeOffset.FromUnixTimeSeconds(exp).DateTime;
                return expDate < DateTime.UtcNow.AddHours(1);
            }

            return true; // If there's no expiration claim, consider the token expired
        }
        
        private string GetTokenName()
        {
            return $"auth_{_configuration["portelem:serviceId"]}_{_configuration["portelem:SystemTitle"]}_Token";
        }

    }
}
