using Catan3.GameService.Controllers;
using Catan3.GameService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add CORS for local development
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalhost", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Register GameStateMachine as a singleton (renamed from GameController)
builder.Services.AddSingleton<GameStateMachine>(provider => 
{
    // For now, create with null services - will be updated when we integrate fully
    return new GameStateMachine(null, "temp_save.json");
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Enable CORS
app.UseCors("AllowLocalhost");

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

// Serve static files (including our companion.html)
app.UseStaticFiles();

// Map companion route to serve the HTML file
app.MapGet("/companion", async (HttpContext context) =>
{
    var filePath = Path.Combine(app.Environment.WebRootPath, "companion.html");
    if (File.Exists(filePath))
    {
        var content = await File.ReadAllTextAsync(filePath);
        context.Response.ContentType = "text/html";
        await context.Response.WriteAsync(content);
    }
    else
    {
        context.Response.StatusCode = 404;
        await context.Response.WriteAsync("Companion interface not found");
    }
});

app.MapStaticAssets();

// Map API controllers
app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

Console.WriteLine("Catan3 Game Service starting...");
Console.WriteLine("Web Companion interface available at: /companion");
Console.WriteLine("API endpoints available at: /api/*");
Console.WriteLine("Game State Machine ready for REST API calls");

app.Run();
