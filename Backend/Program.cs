using Backend.Servicios;
using Backend.Modelos.Usuario;
using Backend.Endpoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting; 
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ==================== CONFIGURACIÓN CORS ====================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// ==================== CONFIGURACIÓN JWT ====================
var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(jwtSettings["Key"]!);

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
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };
});

// ==================== POLÍTICAS DE AUTORIZACIÓN ====================
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SoloAdmins", policy =>
        policy.RequireClaim("tipo_usuario", "ADMIN"));
    options.AddPolicy("ParaNegocios", policy =>
        policy.RequireClaim("tipo_usuario", "NEGOCIO"));
});

// ==================== SWAGGER ====================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ==================== DB CONTEXT ====================
builder.Services.AddDbContext<ApiDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        new MySqlServerVersion(new Version(8, 0, 36))
    ));

// ==================== DATA PROTECTION ====================
builder.Services.AddDataProtection();

// ==================== SERVICIOS ====================
builder.Services.AddScoped<IServicioCifrado, ServicioCifrado>();
builder.Services.AddScoped<IServicioAutenticacion, ServicioAutenticacion>();

// ==================== RATE LIMITER ====================

builder.Services.AddRateLimiter(options =>
{
    // Esta es la política "por IP"
    options.AddPolicy("login-limiter", context => RateLimitPartition.GetFixedWindowLimiter(
        
        partitionKey: context.Connection.RemoteIpAddress?.ToString(), 
        
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5, // 5 intentos
            Window = TimeSpan.FromMinutes(1), // por 1 minuto
            QueueLimit = 0
        }));

    // Esto es lo que pasa cuando alguien es rechazado
    options.OnRejected = (context, _) =>
    {
        context.HttpContext.Response.StatusCode = 429; // Too Many Requests
        return new ValueTask(context.HttpContext.Response.WriteAsync("Demasiados intentos. Por favor, espera un minuto."));
    };
});

// ==================== CONSTRUCCIÓN DEL APP ====================
var app = builder.Build();

// ==================== MIDDLEWARES ====================

// Habilitar CORS antes de cualquier endpoint
app.UseCors("AllowAll");

app.UseRateLimiter();

// Swagger solo en desarrollo
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// JWT
app.UseAuthentication();
app.UseAuthorization();

// ==================== MAPEO DE ENDPOINTS ====================
app.MapearEndpointsAutenticacion();
app.MapearEndpointsTarjetas();

// ==================== RUN ====================
app.Run();
