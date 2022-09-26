using System;
using System.IdentityModel.Tokens.Jwt;
using AutoMapper;
using IdentityServer4.AccessTokenValidation;
using ImageGallery.API.Authorization;
using ImageGallery.API.Entities;
using ImageGallery.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;


var builder = WebApplication.CreateBuilder(args);

JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

builder.Services.AddControllers()
         .AddJsonOptions(opts => opts.JsonSerializerOptions.PropertyNamingPolicy = null);

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<IAuthorizationHandler, MustOwnImageHandler>();

builder.Services.AddAuthorization(authorizationOptions =>
{
    authorizationOptions.AddPolicy(
        "MustOwnImage",
        policyBuilder =>
        {
            policyBuilder.RequireAuthenticatedUser();
            policyBuilder.AddRequirements(
                  new MustOwnImageRequirement());
        });
});

builder.Services.AddAuthentication(IdentityServerAuthenticationDefaults.AuthenticationScheme)
    .AddJwtBearer(IdentityServerAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.Authority = "https://localhost:44318";
        options.Audience = "imagegalleryapi";
        options.TokenValidationParameters = new TokenValidationParameters()
        {
            ValidateAudience = false
        };
    });

// register the DbContext on the container, getting the connection string from
// appSettings (note: use this during development; in a production environment,
// it's better to store the connection string in an environment variable)
builder.Services.AddDbContext<GalleryContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration["ConnectionStrings:ImageGalleryDBConnectionString"]);
});

// register the repository
builder.Services.AddScoped<IGalleryRepository, GalleryRepository>();

// register AutoMapper-related services
builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

var app = builder.Build();




if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();

    using (var scope = app.Services.CreateScope())
    {
        try
        {
            var context = scope.ServiceProvider.GetService<GalleryContext>();
            // migrate & seed
            context.Database.Migrate();
        }
        catch (Exception ex)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "An error occurred while migrating or seeding the database.");
        }
    }
}
else
{
    app.UseExceptionHandler(appBuilder =>
    {
        appBuilder.Run(async context =>
        {
            // ensure generic 500 status code on fault.
            context.Response.StatusCode = StatusCodes.Status500InternalServerError; ;
            await context.Response.WriteAsync("An unexpected fault happened. Try again later.");
        });
    });
    // The default HSTS value is 30 days. You may want to change this for 
    // production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();

app.UseAuthorization();

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
});

app.Run();