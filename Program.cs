using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using AppOwnsDataMultiTenant.Models;
using AppOwnsDataMultiTenant.Services;

var builder = WebApplication.CreateBuilder(args);

string connectString = builder.Configuration["AppOwnsDataMultiTenantDB:ConnectString"];
builder.Services.AddDbContext<AppOwnsDataMultiTenantDB>(opt => opt.UseSqlServer(connectString));

builder.Services
       .AddMicrosoftIdentityWebAppAuthentication(builder.Configuration)
       .EnableTokenAcquisitionToCallDownstreamApi()
       .AddInMemoryTokenCaches();

builder.Services.AddScoped(typeof(AppOwnsDataMultiTenantDB));
builder.Services.AddScoped(typeof(AppOwnsDataMultiTenantDbService));
builder.Services.AddScoped(typeof(PowerBiServiceApi));

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
