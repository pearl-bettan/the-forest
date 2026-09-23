using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using UsersManager.Server;
using System.Text;
using Data;
using Microsoft.AspNetCore.StaticFiles;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

//DB
builder.Services.AddScoped<DbRepository>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();

//User management
builder.Services.AddScoped<AuthCheck>();
builder.Services.AddScoped<AuthRepository>();
builder.Services.AddScoped<ITokenBlacklistService, DbTokenBlacklistService>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<PasswordService>();

//Files
builder.Services.AddScoped<FilesManage>();

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
app.UseStaticFiles();

//Special Files
var provider = new FileExtensionContentTypeProvider();
provider.Mappings[".data"] = "application/json";
provider.Mappings[".svg"] = "image/svg";
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider
});

//Brotli files
//בניית ה-WebGL של יוניטי מייצרת קבצי .br דחוסים מראש. השרת מגיש
//אותם כמו שהם ומצהיר על הדחיסה, והדפדפן מפענח
app.UseStaticFiles(new StaticFileOptions
{
    ServeUnknownFileTypes = true,
    OnPrepareResponse = ctx =>
    {
        string name = ctx.File.Name;

        if (name.EndsWith(".br", StringComparison.OrdinalIgnoreCase) == false) return;

        if (name.EndsWith(".js.br", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.ContentType = "application/javascript";
        }
        else if (name.EndsWith(".wasm.br", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.ContentType = "application/wasm";
        }
        else
        {
            ctx.Context.Response.ContentType = "application/octet-stream";
        }

        //השמה ולא Append. Append מוסיף ערך נוסף, וכותרת
        //"Content-Encoding: br, br" גורמת לדפדפן לנסות לפענח
        //ברוטלי פעמיים ולהיכשל ב-ERR_CONTENT_DECODING_FAILED
        ctx.Context.Response.Headers["Content-Encoding"] = "br";

        //תוכן דחוס מראש לא ניתן להגשה בחלקים: טווח בייטים מתוך
        //זרם ברוטלי אינו זרם ברוטלי תקין, והפענוח בדפדפן נכשל
        ctx.Context.Response.Headers["Accept-Ranges"] = "none";
    }
});

app.UseRouting();

//user management
app.UseMiddleware<TokenBlacklistMiddleware>();
app.UseAuthentication();
app.UseAuthorization();


app.MapRazorPages();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
