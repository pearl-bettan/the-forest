var builder = WebApplication.CreateBuilder(args);

// Force ContentRoot and WebRoot to match the actual executable folder location
var baseDir = AppContext.BaseDirectory;
builder.Environment.ContentRootPath = baseDir;
builder.Environment.WebRootPath = Path.Combine(baseDir, "wwwroot");

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.Run();
