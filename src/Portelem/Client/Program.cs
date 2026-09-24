using AuthTemplate.Client;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.Authorization;
using UsersManager.Client;

// ============================================================
// נקודת הכניסה של אפליקציית הלקוח.
//
// כל הקוד כאן רץ בדפדפן, בתוך WebAssembly - לא בשרת. לכן אין
// כאן גישה לבסיס הנתונים ולא לקבצים, וכל נתון מגיע דרך ה-API.
//
// התפקיד של הקובץ: לקשור את רכיב השורש ל-HTML, ולרשום את
// השירותים שהמסכים מקבלים בהזרקה
// ============================================================
var builder = WebAssemblyHostBuilder.CreateDefault(args);

// App נטען לתוך האלמנט עם id="app" שב-index.html
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// כתובת הבסיס היא כתובת האתר עצמו, ולכן כל הקריאות ל-API
// יחסיות. כך אותו קוד עובד מקומית ועל השרת בלי שינוי
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// ---------- ניהול משתמשים ----------
// AuthStateProvider נרשם כ-AuthenticationStateProvider, וזה מה
// שמחבר אותו ל-<AuthorizeView> ול-CascadingAuthenticationState.
// AddAuthorizationCore הוא מה שמפעיל את מנגנון ההרשאות בכלל
//User management
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<AuthenticationStateProvider, AuthStateProvider>();
builder.Services.AddScoped<ILocalStorageService, LocalStorageService>();
builder.Services.AddAuthorizationCore();

await builder.Build().RunAsync();
