using FeedCraft.Domain.Services;
using FeedCraft.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// The feed optimizer lives in the Domain layer; inject it rather than new-ing it up.
builder.Services.AddScoped<IFeedOptimizationService, FeedOptimizationService>();

// Comparing two formulations is pure arithmetic over two view models, so it lives in Domain
// alongside the optimizer.
builder.Services.AddScoped<IFormulationComparer, FormulationComparer>();

// Reading the ingredient library, the pickers and the saved snapshots — and copying a knowledge
// base entry's targets onto a formulation. Shared by /Feed and /Experiment so both pages start
// from the same data and a template means the same thing on each.
builder.Services.AddScoped<IFormulationLibraryReader, FormulationLibraryReader>();
builder.Services.AddScoped<IKnowledgeTemplateApplier, KnowledgeTemplateApplier>();

// Persistence lives in the Infrastructure layer (SQLite file alongside the app).
builder.Services.AddDbContext<FeedCraftDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity shares the one FeedCraftDbContext, so there is a single database and a single
// migration chain. AddIdentity (rather than AddDefaultIdentity) because the login/register
// pages are our own MVC views — AddDefaultIdentity would drag in the Razor Pages UI package.
// Password rules are left at Identity's defaults rather than weakened for demo convenience.
builder.Services
    .AddIdentity<IdentityUser, IdentityRole>(options =>
    {
        // No email sender exists in this app, so requiring a confirmed account would lock every
        // newly registered dealer out of their own listings.
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<FeedCraftDbContext>()
    .AddDefaultTokenProviders();

// Point the cookie middleware at our own pages instead of the /Account/Login default it
// assumes, and give a role failure somewhere meaningful to land.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

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
    app.UseExceptionHandler("/Error");
}
app.UseRouting();

// Order is load-bearing: authentication must establish who the caller is before
// authorization decides what they may do. No global authorization filter is registered —
// the whole app stays anonymous by default and only /Listings opts in.
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Feed}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
