using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MyApi.Data;
using MyApi.Models.Entities;
using System.Text;
using Microsoft.OpenApi.Models;
using System.Security.Cryptography;
using Duende.IdentityServer.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Add DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity Configuration
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => {
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// IdentityServer Configuration
var identityServerBuilder = builder.Services.AddIdentityServer(options =>
{
    options.Events.RaiseErrorEvents = true;
    options.Events.RaiseInformationEvents = true;
    options.Events.RaiseFailureEvents = true;
    options.Events.RaiseSuccessEvents = true;
    
    options.EmitStaticAudienceClaim = true;
})
.AddAspNetIdentity<ApplicationUser>()
.AddInMemoryIdentityResources(new List<IdentityResource>
{
    new IdentityResources.OpenId(),
    new IdentityResources.Profile(),
    new IdentityResources.Email()
})
.AddInMemoryApiScopes(new List<ApiScope>
{
    new ApiScope("api1", "My API")
})
.AddInMemoryApiResources(new List<ApiResource>
{
    new ApiResource("api1", "My API")
    {
        Scopes = { "api1" }
    }
})
.AddInMemoryClients(new List<Client>
{
    // Resource Owner Password Client
    new Client
    {
        ClientId = "ro.client",
        ClientName = "Resource Owner Client",
        AllowedGrantTypes = GrantTypes.ResourceOwnerPassword,
        ClientSecrets = { new Secret("secret".Sha256()) },
        AllowedScopes = { "api1", "openid", "profile", "email" },
        AccessTokenLifetime = 3600
    },
    // JavaScript Client
    new Client
    {
        ClientId = "js",
        ClientName = "JavaScript Client",
        AllowedGrantTypes = GrantTypes.Code,
        RequirePkce = true,
        RequireClientSecret = false,
        RedirectUris = { "http://localhost:4200/callback" },
        PostLogoutRedirectUris = { "http://localhost:4200" },
        AllowedCorsOrigins = { "http://localhost:4200" },
        AllowedScopes = { "api1", "openid", "profile", "email" }
    }
});

if (builder.Environment.IsDevelopment())
{
    identityServerBuilder.AddDeveloperSigningCredential();
}
else
{
    // TODO: Add production signing credential
    // builder.AddSigningCredential(...)
}

// Configure Authentication
builder.Services.AddAuthentication()
    .AddJwtBearer(options =>
    {
        options.Authority = "https://localhost:5001"; // Your IdentityServer URL
        options.RequireHttpsMetadata = false; // Set to true in production
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"] ?? 
                throw new InvalidOperationException("Jwt:Secret not configured")))
        };
    })
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? 
            throw new InvalidOperationException("Google ClientId not configured");
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? 
            throw new InvalidOperationException("Google ClientSecret not configured");
    })
    .AddGitHub(options =>
    {
        options.ClientId = builder.Configuration["Authentication:GitHub:ClientId"] ?? 
            throw new InvalidOperationException("GitHub ClientId not configured");
        options.ClientSecret = builder.Configuration["Authentication:GitHub:ClientSecret"] ?? 
            throw new InvalidOperationException("GitHub ClientSecret not configured");
    })
    .AddDiscord(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Discord:ClientId"] ?? 
            throw new InvalidOperationException("Discord ClientId not configured");
        options.ClientSecret = builder.Configuration["Authentication:Discord:ClientSecret"] ?? 
            throw new InvalidOperationException("Discord ClientSecret not configured");
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API v1"));
}

app.UseHttpsRedirection();

// The order is important!
app.UseIdentityServer();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/", () => "Hello World!");
