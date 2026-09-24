using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using UsersManager.Server;
using System.Text;
using Data;
using Microsoft.AspNetCore.StaticFiles;

// ============================================================
// נקודת הכניסה של השרת.
//
// הקובץ בנוי בשני חלקים שסדרם קריטי:
//   1. רישום שירותים ב-builder.Services - מה זמין להזרקה
//   2. בניית צינור הבקשות ב-app.Use... - מה רץ על כל בקשה,
//      לפי הסדר שבו הוא נכתב כאן
//
// שינוי סדר השורות בחלק השני משנה את התנהגות השרת
// ============================================================
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// גישה לבסיס הנתונים. Scoped - מופע אחד לכל בקשה, כדי
// שחיבור לא ישותף בין בקשות מקבילות
//DB
builder.Services.AddScoped<DbRepository>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();

// ---------- ניהול משתמשים ----------
// AuthCheck ו-AuthRepository תלויים בבקשה הנוכחית ולכן Scoped.
// TokenService ו-PasswordService חסרי מצב ולכן Singleton -
// מופע אחד לכל חיי השרת
//User management
builder.Services.AddScoped<AuthCheck>();
builder.Services.AddScoped<AuthRepository>();
builder.Services.AddScoped<ITokenBlacklistService, DbTokenBlacklistService>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<PasswordService>();

//Files
builder.Services.AddScoped<FilesManage>();

// ---------- הגדרות אימות הטוקן ----------
// ההגדרות כאן חלות על [Authorize] של המערכת. AuthCheck מבצע
// אימות משלו באותם פרמטרים, ולכן שינוי כאן מחייב בדיקה גם שם.
// הקהל אינו נבדק כי לשרת יש צרכן אחד בלבד
//JWT
var jwtSettings = builder.Configuration.GetSection("JWTSettings");
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["validIssuer"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["securityKey"]))
        };
    });

// שירות רקע לניקוי הרשימה השחורה, רץ כל עוד השרת פועל
builder.Services.AddHostedService<TokenCleanupBackgroundService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseBlazorFrameworkFiles();

//כל מה שתחת /Game הוא תוצר הבנייה של יוניטי. הפריסה דורסת אותו
//תחת אותם שמות קבצים בדיוק, ולכן דפדפן ששמר גרסה קודמת מרכיב
//ערבוב של ישן וחדש ונכשל בטעינה.
//no-cache אינו מבטל קאש אלא מחייב אימות מול השרת: קובץ שלא
//השתנה מוחזר כ-304 ולא יורד שוב, וקובץ שהתחלף יורד מחדש
Action<StaticFileResponseContext> gameFiles = ctx =>
{
    if (ctx.Context.Request.Path.StartsWithSegments("/Game"))
    {
        ctx.Context.Response.Headers["Cache-Control"] = "no-cache";
    }
};

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = gameFiles
});

//Special Files
var provider = new FileExtensionContentTypeProvider();
provider.Mappings[".data"] = "application/json";
provider.Mappings[".svg"] = "image/svg";
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider,
    OnPrepareResponse = gameFiles
});

//קבצי הבנייה של יוניטי
//הבנייה מוגדרת Brotli + Decompression Fallback, ולכן הקבצים הכבדים
//נשמרים כ-.unityweb ויוניטי מפענחת אותם בעצמה בדפדפן.
//חשוב לא להצהיר Content-Encoding עליהם: הדפדפן היה מפענח אותם
//בעצמו ויוניטי הייתה מקבלת תוכן שכבר פוענח. בנוסף, כרום מקבל
//Content-Encoding: br רק ב-https או ב-localhost, והשרת מוגש ב-http
app.UseStaticFiles(new StaticFileOptions
{
    ServeUnknownFileTypes = true,
    DefaultContentType = "application/octet-stream",
    OnPrepareResponse = gameFiles
});

app.UseRouting();

// ---------- שכבות האימות ----------
// הסדר מחייב: פסילת טוקנים לפני האימות, כדי שטוקן שנפסל
// בהתנתקות ייחסם עוד לפני שהמערכת מקבלת אותו כתקף
//user management
app.UseMiddleware<TokenBlacklistMiddleware>();
app.UseAuthentication();
app.UseAuthorization();


app.MapRazorPages();
app.MapControllers();

// כל כתובת שלא נתפסה על ידי בקר או קובץ מוחזרת אל index.html,
// כדי שהניווט הפנימי של Blazor יטפל בה. בלי זה רענון עמוד
// בכתובת פנימית היה מחזיר 404
app.MapFallbackToFile("index.html");

app.Run();
