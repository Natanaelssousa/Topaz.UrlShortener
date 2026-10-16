using Topaz.UrlShortener.Application.Interfaces;
using Topaz.UrlShortener.Infrastructure.Storage;
using Topaz.UrlShortener.Infrastructure.Generation;
using Topaz.UrlShortener.Application.Services;
using Topaz.UrlShortener.Infrastructure.Queue;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddSingleton<ILinkStore, InMemoryLinkStore>();
builder.Services.AddSingleton<IShortCodeGenerator, RandomShortCodeGenerator>();
builder.Services.AddSingleton<LinkShorteningService>();

builder.Services.AddSingleton<LinkCreationQueue>();
builder.Services.AddHostedService<LinkCreationWorker>();

builder.Services.AddProblemDetails();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
