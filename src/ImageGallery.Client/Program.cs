


using System;
using System.IdentityModel.Tokens.Jwt;
using IdentityModel;
using ImageGallery.Client.HttpHandlers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;

JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews()
     .AddJsonOptions(opts => opts.JsonSerializerOptions.PropertyNamingPolicy = null);

builder.Services.AddAuthorization(authorizationOptions =>
    {
        authorizationOptions.AddPolicy(
                    "CanOrderFrame",
                    policyBuilder =>
                    {
                        policyBuilder.RequireAuthenticatedUser();
                        policyBuilder.RequireClaim("country", "be");
                        policyBuilder.RequireClaim("subscriptionlevel", "PayingUser");
                    });
    });

builder.Services.AddHttpContextAccessor();

builder.Services.AddTransient<BearerTokenHandler>();

// create an HttpClient used for accessing the API
builder.Services.AddHttpClient("APIClient", client =>
{
    client.BaseAddress = new Uri("https://localhost:44366/");
    client.DefaultRequestHeaders.Clear();
    client.DefaultRequestHeaders.Add(HeaderNames.Accept, "application/json");
}).AddHttpMessageHandler<BearerTokenHandler>();
// create an HttpClient used for accessing the IDP

builder.Services.AddHttpClient("IDPClient", client =>
{
    client.BaseAddress = new Uri("https://localhost:44318/");
    client.DefaultRequestHeaders.Clear();
    client.DefaultRequestHeaders.Add(HeaderNames.Accept, "application/json");
});


builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.AccessDeniedPath = "/Authorization/AccessDenied";
})
.AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.Authority = "https://localhost:44318/";
    options.ClientId = "imagegalleryclient";
    options.ResponseType = "code";
    options.Scope.Add("address");
    options.Scope.Add("roles");
    options.Scope.Add("imagegalleryapiscope");
    options.Scope.Add("subscriptionlevel");
    options.Scope.Add("country");
    options.Scope.Add("offline_access");
    options.ClaimActions.DeleteClaim("sid");
    options.ClaimActions.DeleteClaim("idp");
    options.ClaimActions.DeleteClaim("s_hash");
    options.ClaimActions.DeleteClaim("auth_time");
    options.ClaimActions.MapUniqueJsonKey("role", "role");
    options.ClaimActions.MapUniqueJsonKey("subscriptionlevel", "subscriptionlevel");
    options.ClaimActions.MapUniqueJsonKey("country", "country");
    options.SaveTokens = true;
    options.ClientSecret = "secret";
    options.GetClaimsFromUserInfoEndpoint = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        NameClaimType = JwtClaimTypes.GivenName,
        RoleClaimType = JwtClaimTypes.Role
    };
});

var app = builder.Build();

app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Shared/Error");
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
    endpoints.MapControllerRoute(
        name: "default",
        pattern: "{controller=Gallery}/{action=Index}/{id?}");
});

app.Run();