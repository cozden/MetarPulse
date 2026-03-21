using MetarPulse.Admin.Components;
using MetarPulse.Core.Models;
using MetarPulse.Infrastructure.Configuration;
using MetarPulse.Infrastructure.Persistence.PostgreSQL;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

// ─── Blazor Server ─────────────────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ─── Veritabanı (PostgreSQL + Repository'ler) ──────────────────────────────
builder.Services.AddPostgreSqlDatabase(builder.Configuration);

// ─── ASP.NET Identity ─────────────────────────────────────────────────────
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = false;
    })
    .AddEntityFrameworkStores<AppDbContext>();

// ─── Cookie Auth (Blazor Server için JWT yerine Cookie) ───────────────────
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath  = "/login";
    options.LogoutPath = "/logout";
    options.AccessDeniedPath = "/access-denied";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(
        builder.Configuration.GetValue<int>("Admin:CookieExpiryMinutes", 480));
    options.SlidingExpiration = true;
});

// ─── API HttpClient (Provider yönetimi için) ───────────────────────────────
var apiBaseUrl    = builder.Configuration.GetValue<string>("Admin:ApiBaseUrl",    "http://localhost:5000")!;
var internalApiKey = builder.Configuration.GetValue<string>("Admin:InternalApiKey", "")!;
builder.Services.AddHttpClient("MetarPulseApi", c =>
{
    c.BaseAddress = new Uri(apiBaseUrl);
    if (!string.IsNullOrWhiteSpace(internalApiKey))
        c.DefaultRequestHeaders.Add("X-Admin-Key", internalApiKey);
});

// ─── Authorization ─────────────────────────────────────────────────────────
builder.Services.AddAuthorization();

// ─── Cascade Auth State (Blazor Server) ───────────────────────────────────
builder.Services.AddCascadingAuthenticationState();

var app = builder.Build();

// ─── Pipeline ──────────────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// ─── Login endpoint (HTTP POST — cookie Blazor SignalR üzerinden yazılamaz) ──
app.MapPost("/account/login", async (
    HttpContext ctx,
    SignInManager<ApplicationUser> signIn) =>
{
    var form = await ctx.Request.ReadFormAsync();
    var email     = form["email"].ToString().Trim();
    var password  = form["password"].ToString();
    var returnUrl = form["returnUrl"].ToString();
    if (string.IsNullOrEmpty(returnUrl) || !returnUrl.StartsWith("/")) returnUrl = "/";

    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        return Results.LocalRedirect($"/login?error=empty&returnUrl={Uri.EscapeDataString(returnUrl)}");

    var result = await signIn.PasswordSignInAsync(email, password, isPersistent: true, lockoutOnFailure: true);

    if (result.Succeeded)
        return Results.LocalRedirect(returnUrl);
    if (result.IsLockedOut)
        return Results.LocalRedirect($"/login?error=locked&returnUrl={Uri.EscapeDataString(returnUrl)}");

    return Results.LocalRedirect($"/login?error=invalid&returnUrl={Uri.EscapeDataString(returnUrl)}");
}).DisableAntiforgery();

// ─── Logout endpoint ───────────────────────────────────────────────────────
app.MapPost("/logout", async (SignInManager<ApplicationUser> signIn) =>
{
    await signIn.SignOutAsync();
    return Results.LocalRedirect("/login");
}).DisableAntiforgery();

app.Run();
