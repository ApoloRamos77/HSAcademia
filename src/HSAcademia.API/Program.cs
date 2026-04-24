using HSAcademia.Application.Interfaces;
using HSAcademia.Infrastructure.Persistence;
using HSAcademia.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ─────────────────────────────────────────────────────────────
// 1. Database — PostgreSQL / EF Core
// ─────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql.EnableRetryOnFailure(3)));

// ─────────────────────────────────────────────────────────────
// 2. Application Services (implemented in Infrastructure)
// ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<HSAcademia.Infrastructure.Services.AuthService>();
builder.Services.AddScoped<HSAcademia.Infrastructure.Services.SuperAdminService>();
builder.Services.AddScoped<HSAcademia.Infrastructure.Services.AcademyConfigurationService>();
builder.Services.AddScoped<HSAcademia.Infrastructure.Services.StudentService>();
builder.Services.AddScoped<HSAcademia.Infrastructure.Services.StoreService>();
builder.Services.AddScoped<HSAcademia.Infrastructure.Services.FinancesService>();
builder.Services.AddScoped<HSAcademia.Application.Interfaces.IAttendanceService, HSAcademia.Infrastructure.Services.AttendanceService>();
builder.Services.AddScoped<HSAcademia.Application.Interfaces.ICalendarService, HSAcademia.Infrastructure.Services.CalendarService>();

// ─────────────────────────────────────────────────────────────
// 3. Infrastructure Services
// ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IEmailService, EmailService>();

// ─────────────────────────────────────────────────────────────
// 4. JWT Authentication
// ─────────────────────────────────────────────────────────────
var jwtSection = builder.Configuration.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(jwtSection["Key"]!);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.RequireHttpsMetadata = false;
        opts.SaveToken = true;
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// ─────────────────────────────────────────────────────────────
// 5. Controllers + CORS
// ─────────────────────────────────────────────────────────────
builder.Services.AddControllers();

builder.Services.AddCors(opts =>
{
    opts.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins(
            "http://localhost:3000",
            "http://localhost:5173",
            "http://localhost:5174"
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

// ─────────────────────────────────────────────────────────────
// 6. Swagger / OpenAPI
// ─────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ADHSOFT SPORT API",
        Version = "v1",
        Description = "Plataforma de Gestión Deportiva Multi-Academia"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Ingrese el token JWT: Bearer {token}"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// ─────────────────────────────────────────────────────────────
// Build App
// ─────────────────────────────────────────────────────────────
var app = builder.Build();

// Auto-migrate on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        db.Database.Migrate();
        Console.WriteLine("✅ Base de datos migrada correctamente.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️  Error en migración: {ex.Message}");
    }
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ADHSOFT SPORT API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
