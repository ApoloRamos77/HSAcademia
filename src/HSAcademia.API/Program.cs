using HSAcademia.API.Authorization;
using HSAcademia.Application.Interfaces;
using HSAcademia.Infrastructure.Persistence;
using HSAcademia.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ─────────────────────────────────────────────────────────────
// 0. Configuración obligatoria (secretos SOLO por variables de entorno / user-secrets)
//    Producción (Easypanel):
//      ConnectionStrings__DefaultConnection = Host=...;Database=...;Username=...;Password=...
//      Jwt__Key                             = cadena aleatoria de 64+ caracteres
//      Cors__AllowedOrigins__0              = https://tu-frontend
//    Desarrollo: dotnet user-secrets set "Jwt:Key" "..." (ver README de despliegue)
// ─────────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"];

if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException(
        "Falta la cadena de conexión. Define la variable de entorno ConnectionStrings__DefaultConnection.");
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
    throw new InvalidOperationException(
        "Falta Jwt:Key o es demasiado corta (mínimo 32 caracteres). Define la variable de entorno Jwt__Key.");

// ─────────────────────────────────────────────────────────────
// 1. Database — PostgreSQL / EF Core
// ─────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(3)));

// ─────────────────────────────────────────────────────────────
// 2. Application Services (implemented in Infrastructure)
// ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<SuperAdminService>();
builder.Services.AddScoped<AcademyConfigurationService>();
builder.Services.AddScoped<StudentService>();
builder.Services.AddScoped<StoreService>();
builder.Services.AddScoped<FinancesService>();
builder.Services.AddScoped<ReceiptRecorder>();
builder.Services.AddScoped<ReceiptService>();
builder.Services.AddScoped<DiscountService>();
builder.Services.AddScoped<CashService>();
builder.Services.AddScoped<AccessControlService>();
builder.Services.AddScoped<FinancialPeriodGuard>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<PaymentProofService>();
builder.Services.AddScoped<SportsService>();
builder.Services.AddScoped<EvaluationService>();
builder.Services.AddScoped<ReportsService>();
builder.Services.AddScoped<FamilyNoticeService>();
builder.Services.AddScoped<StaffAttendanceService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<ICalendarService, CalendarService>();
builder.Services.AddScoped<IAnnouncementService, AnnouncementService>();
builder.Services.AddScoped<IFinancesPremiumService, FinancesPremiumService>();

// ─────────────────────────────────────────────────────────────
// 3. Infrastructure Services
// ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddSingleton<UploadStorage>();

// ─────────────────────────────────────────────────────────────
// 4. JWT Authentication
// ─────────────────────────────────────────────────────────────
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        // HTTPS lo termina el proxy (Traefik/Easypanel); la API escucha HTTP interno.
        opts.RequireHttpsMetadata = false;
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
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
// 5. Proxy inverso: IP real del cliente (necesaria para el rate limiting por IP)
// ─────────────────────────────────────────────────────────────
builder.Services.Configure<ForwardedHeadersOptions>(opts =>
{
    opts.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // El contenedor solo es accesible a través del proxy de Easypanel.
    opts.KnownNetworks.Clear();
    opts.KnownProxies.Clear();
    opts.ForwardLimit = 1; // se toma la IP agregada por el proxy, no la que envíe el cliente
});

// ─────────────────────────────────────────────────────────────
// 6. Rate limiting (fuerza bruta en login y spam en formularios públicos)
// ─────────────────────────────────────────────────────────────
builder.Services.AddRateLimiter(opts =>
{
    opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opts.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.ContentType = "application/json";
        await ctx.HttpContext.Response.WriteAsync(
            "{\"message\":\"Demasiados intentos. Espera un minuto e inténtalo nuevamente.\"}", ct);
    };

    opts.AddPolicy("login", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

    opts.AddPolicy("public", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

// ─────────────────────────────────────────────────────────────
// 7. Controllers + CORS
// ─────────────────────────────────────────────────────────────
builder.Services.AddControllers();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()?
    .Where(o => !string.IsNullOrWhiteSpace(o))
    .Select(o => o.Trim().TrimEnd('/'))
    .ToArray() ?? Array.Empty<string>();

builder.Services.AddCors(opts =>
{
    opts.AddPolicy("AllowFrontend", policy =>
    {
        policy.SetIsOriginAllowed(origin =>
        {
            if (allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase)) return true;
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;
            // Desarrollo local siempre permitido
            if (uri.Host is "localhost" or "127.0.0.1") return true;
            // Transitorio: si no se configuró Cors:AllowedOrigins, permitir subdominios de Easypanel.
            return allowedOrigins.Length == 0 && uri.Scheme == "https" &&
                   uri.Host.EndsWith(".easypanel.host", StringComparison.OrdinalIgnoreCase);
        })
        .AllowAnyHeader()
        .AllowAnyMethod();
        // Sin AllowCredentials: el JWT viaja en el header Authorization, no en cookies.
    });
});

// ─────────────────────────────────────────────────────────────
// 8. Swagger / OpenAPI (solo Desarrollo o si Swagger:Enabled = true)
// ─────────────────────────────────────────────────────────────
var swaggerEnabled = builder.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Swagger:Enabled");
if (swaggerEnabled)
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Clubio API",
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
}

// ─────────────────────────────────────────────────────────────
// Build App
// ─────────────────────────────────────────────────────────────
var app = builder.Build();

if (allowedOrigins.Length == 0 && !app.Environment.IsDevelopment())
    app.Logger.LogWarning("Cors:AllowedOrigins no está configurado: se permiten temporalmente los subdominios *.easypanel.host. Configura el dominio exacto del frontend.");

// Migraciones automáticas al iniciar (desactivable con Database:MigrateOnStartup=false).
// Si fallan, la API NO arranca: funcionar con un esquema desactualizado corrompe datos en silencio.
if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        db.Database.Migrate();
        app.Logger.LogInformation("Base de datos migrada correctamente.");
    }
    catch (Exception ex)
    {
        app.Logger.LogCritical(ex, "Error al aplicar migraciones de base de datos. La API se detiene.");
        throw;
    }
}

// Instalación nueva: el SuperAdmin de semilla trae un hash inutilizable. Si se define
// Bootstrap:SuperAdminPassword, se asigna UNA sola vez (solo mientras conserve el hash de semilla).
var bootstrapPassword = app.Configuration["Bootstrap:SuperAdminPassword"];
if (!string.IsNullOrWhiteSpace(bootstrapPassword))
{
    const string seedHash = "$2a$11$G7Z3P7d1G3kMhY1S1iR5M.O5X9W8G2m5T4c5lR2p5n8v1M3s1x2eO";
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var superAdmin = db.Users.IgnoreQueryFilters()
        .FirstOrDefault(u => u.Role == HSAcademia.Domain.Enums.UserRole.SuperAdmin && u.PasswordHash == seedHash);
    if (superAdmin != null)
    {
        superAdmin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(bootstrapPassword);
        superAdmin.MustChangePassword = true;
        db.SaveChanges();
        app.Logger.LogWarning("Se asignó la contraseña inicial del SuperAdmin ({Email}). Cámbiala al ingresar y elimina Bootstrap:SuperAdminPassword.", superAdmin.Email);
    }
}

app.UseForwardedHeaders();

// Errores no controlados: se registran en el log y el cliente recibe un mensaje genérico
// (sin trazas ni detalles internos de la base de datos).
// Convención: los servicios lanzan System.Exception (tipo exacto) para reglas de negocio con mensaje
// para el usuario → 400. KeyNotFoundException → 404. Cualquier otro tipo es un error técnico → 500 genérico.
app.UseExceptionHandler(errorApp => errorApp.Run(async ctx =>
{
    var error = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
    int status;
    string message;

    if (error != null && error.GetType() == typeof(Exception))
    {
        status = StatusCodes.Status400BadRequest;
        message = error.Message;
    }
    else if (error is KeyNotFoundException)
    {
        status = StatusCodes.Status404NotFound;
        message = error.Message;
    }
    else
    {
        status = StatusCodes.Status500InternalServerError;
        message = "Ocurrió un error inesperado. Inténtalo nuevamente o contacta al soporte.";
        if (error != null)
            app.Logger.LogError(error, "Error no controlado en {Method} {Path}", ctx.Request.Method, ctx.Request.Path);
    }

    ctx.Response.StatusCode = status;
    await ctx.Response.WriteAsJsonAsync(new { message });
}));

if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Clubio API v1");
        c.RoutePrefix = "swagger";
    });
}

// Archivos subidos (vouchers). URLs con GUID aleatorio, sin listado de directorio.
var uploads = app.Services.GetRequiredService<UploadStorage>();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploads.RootPath),
    RequestPath = "/uploads",
    OnPrepareResponse = ctx => ctx.Context.Response.Headers["X-Content-Type-Options"] = "nosniff"
});

app.UseCors("AllowFrontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Health Check Endpoints para Easypanel / Traefik
// En desarrollo (o con Swagger habilitado) la raíz lleva a la documentación; en producción responde texto.
if (swaggerEnabled)
    app.MapGet("/", () => Results.Redirect("/swagger"));
else
    app.MapGet("/", () => "API is running!");
app.MapGet("/health", () => "OK");

app.Run();
