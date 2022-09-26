
using System;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using IdentityServer4;
using IdentityServer4.EntityFramework.DbContexts;
using IdentityServer4.EntityFramework.Mappers;
using IdentityServerHost.Quickstart.UI;
using Marvin.IDP;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

var builder = WebApplication.CreateBuilder(args);

var marvinIDPDataDBConnectionString =
    "Data Source=(localdb)\\ProjectModels;Initial Catalog=GalleryIDS;Integrated Security=True;Connect Timeout=30;Encrypt=False;TrustServerCertificate=False;ApplicationIntent=ReadWrite;MultiSubnetFailover=False";

var migrationsAssembly = typeof(Program)
    .GetTypeInfo().Assembly.GetName().Name;

// uncomment, if you want to add an MVC-based UI
builder.Services.AddControllersWithViews();

builder.Services.AddAuthentication().AddFacebook(facebookOptions =>
{
    facebookOptions.SignInScheme = IdentityServerConstants.ExternalCookieAuthenticationScheme;
    facebookOptions.AppId = builder.Configuration["Authentication:Facebook:AppId"]; //from secret manager
    facebookOptions.AppSecret = builder.Configuration["Authentication:Facebook:AppSecret"]; //from secret manager
});

builder.Services.AddIdentityServer(options =>
    {
        options.EmitStaticAudienceClaim = true;
    })
    .AddTestUsers(TestUsers.Users)
    .AddConfigurationStore(options =>
    {
        options.ConfigureDbContext = b =>
            b.UseSqlServer(marvinIDPDataDBConnectionString,
                options => options.MigrationsAssembly(migrationsAssembly));
    })

    .AddOperationalStore(options =>
    {
        options.ConfigureDbContext = b =>
            b.UseSqlServer(marvinIDPDataDBConnectionString,
                options => options.MigrationsAssembly(migrationsAssembly));
    })
    // not recommended for production - you need to store your key material somewhere secure
    //builder.AddDeveloperSigningCredential();
    .AddDeveloperSigningCredential();

var app = builder.Build();

var logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore.Authentication", LogEventLevel.Information)
    .Enrich.FromLogContext()
    // uncomment to write to Azure diagnostics stream
    //.WriteTo.File(
    //    @"D:\home\LogFiles\Application\identityserver.txt",
    //    fileSizeLimitBytes: 1_000_000,
    //    rollOnFileSizeLimit: true,
    //    shared: true,
    //    flushToDiskInterval: TimeSpan.FromSeconds(1))
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}", theme: AnsiConsoleTheme.Literate)
    .CreateLogger();




if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

InitializeDatabase(app);

// uncomment if you want to add MVC
app.UseStaticFiles();
app.UseRouting();

app.UseIdentityServer();

// uncomment, if you want to add MVC
app.UseAuthorization();
app.UseEndpoints(endpoints =>
{
    endpoints.MapDefaultControllerRoute();
});

;


try
{
    Log.Information("Starting host...");
    app.Run();
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly.");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

void InitializeDatabase(IApplicationBuilder app)
{
    using (var serviceScope = app.ApplicationServices
        .GetService<IServiceScopeFactory>().CreateScope())
    {
        serviceScope.ServiceProvider
            .GetRequiredService<PersistedGrantDbContext>().Database.Migrate();

        var context = serviceScope.ServiceProvider
            .GetRequiredService<ConfigurationDbContext>();

        context.Database.Migrate();

        if (!context.Clients.Any())
        {
            foreach (var client in Config.Clients)
            {
                context.Clients.Add(client.ToEntity());
            }
            context.SaveChanges();
        }

        if (!context.IdentityResources.Any())
        {
            foreach (var resource in Config.Ids)
            {
                context.IdentityResources.Add(resource.ToEntity());
            }
            context.SaveChanges();
        }

        if (!context.ApiResources.Any())
        {
            foreach (var resource in Config.Apis)
            {
                context.ApiResources.Add(resource.ToEntity());
            }
            context.SaveChanges();
        }

        if (!context.ApiScopes.Any())
        {
            foreach (var resource in Config.ApiScopes)
            {
                context.ApiScopes.Add(resource.ToEntity());
            }
            context.SaveChanges();
        }
    }
}
