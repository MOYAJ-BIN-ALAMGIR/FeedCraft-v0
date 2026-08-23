using FeedCraft.Domain.Services;
using FeedCraft.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// The feed optimizer lives in the Domain layer; inject it rather than new-ing it up.
builder.Services.AddScoped<IFeedOptimizationService, FeedOptimizationService>();

// Persistence lives in the Infrastructure layer (SQLite file alongside the app).
builder.Services.AddDbContext<FeedCraftDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// Create the database and apply any pending migrations on startup, so a fresh clone
// gets a seeded feedcraft.db on first run with no manual step.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FeedCraftDbContext>();
    db.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Feed}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
