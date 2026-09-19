var builder = WebApplication.CreateBuilder(args);

// Explicitly set the WebRootPath to the wwwroot folder in the publish directory
var publishDir = AppContext.BaseDirectory;
builder.Environment.WebRootPath = Path.Combine(publishDir, "wwwroot");

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.Run();
