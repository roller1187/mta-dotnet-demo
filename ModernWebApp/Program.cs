using ModernWebApp.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllersWithViews();

// Add session support (replaces Web.config sessionState)
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(20);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Add memory cache (replaces System.Web.Caching)
builder.Services.AddMemoryCache();

// Register application services
builder.Services.AddScoped<CustomerService>();

// Add HttpContextAccessor (replaces HttpContext.Current)
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.UseSession();

// Log application startup (replaces Global.asax Application_Start)
app.Logger.LogInformation("Modern Web Application Started at {Time}", DateTime.Now);

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
