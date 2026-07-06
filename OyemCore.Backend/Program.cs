using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OyemCore.BusinessLayer.Interfaces;
using OyemCore.BusinessLayer.Services;
using OyemCore.DataLayer.Contexts;
using OyemCore.DataLayer.Interfaces;
using OyemCore.DataLayer.Contexts;

var builder = WebApplication.CreateBuilder(args);

// Kestrel'in sunucuda HTTP üzerinden 5000 ve 5140 portlarını dinlemesini zorunlu kılıyoruz
builder.WebHost.UseUrls("http://*:5000", "http://*:5140");

// Add services to the container.
builder.Services.AddControllers();

// Configure EF Core DbContexts
builder.Services.AddDbContext<MasterDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MasterDB")));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantService, TenantService>();

builder.Services.AddDbContext<YbsDbContext>((provider, options) =>
{
    var tenantService = provider.GetRequiredService<ITenantService>();
    var connectionString = tenantService.GetCurrentConnectionString();
    if (!string.IsNullOrEmpty(connectionString))
    {
        options.UseSqlServer(connectionString);
    }
    else
    {
        options.UseSqlServer("Server=fake;Database=fake;Uid=fake;Pwd=fake");
    }
});

builder.Services.AddScoped<IYbsDbContext>(provider => provider.GetRequiredService<YbsDbContext>());

// Register Custom Business Services
builder.Services.AddHttpClient<IPushNotificationService, PushNotificationService>();
builder.Services.AddScoped<ILdapService, LdapService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddScoped<IBakimService, BakimService>();
builder.Services.AddScoped<IIzinService, IzinService>();
builder.Services.AddScoped<ITalepService, TalepService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<ITakvimService, TakvimService>();
builder.Services.AddScoped<IHaberService, HaberService>();
builder.Services.AddScoped<IEgitimService, EgitimService>();

// Configure CORS
builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });

// Configure JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrEmpty(jwtKey))
{
    throw new InvalidOperationException("JWT Key is not configured in appsettings.json.");
}

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddEndpointsApiExplorer();

// Configure Swagger with JWT Support
builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "OyemCore API", Version = "v1" });

        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.\r\n\r\nExample: \"Bearer 12345abcdef\"",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
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
                    },
                    Scheme = "oauth2",
                    Name = "Bearer",
                    In = ParameterLocation.Header
                },
                new List<string>()
            }
        });
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || true) // Enable swagger in production/staging for testing
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "OyemCore API v1");
    });
}

// if (!app.Environment.IsDevelopment())
// {
//     app.UseHttpsRedirection();
// }

app.UseCors("AllowAll");

app.UseStaticFiles();

// Serve custom storage folder if configured
var localStorageFolder = builder.Configuration["Storage:Folder"];
if (!string.IsNullOrEmpty(localStorageFolder))
{
    var storagePath = Path.IsPathRooted(localStorageFolder)
        ? localStorageFolder
        : Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, localStorageFolder));

    if (Directory.Exists(storagePath))
    {
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(storagePath),
            RequestPath = "" // Served at root
        });
    }
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
