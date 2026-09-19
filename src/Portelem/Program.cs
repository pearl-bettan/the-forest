var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Enables serving static files (like index.html) from the wwwroot directory
app.UseStaticFiles();

// Sets index.html as the default fall-back file for root URLs ("/")
app.UseDefaultFiles();

app.Run();
