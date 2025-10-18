using Backend.Servicios;
using Backend.Modelos.Usuario;
using Backend.Endpoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

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

// ==================== CONSTRUCCIÓN DEL APP ====================
var app = builder.Build();

// ==================== MIDDLEWARES ====================

// Habilitar CORS antes de cualquier endpoint
app.UseCors("AllowAll");

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

// ==================== CONFIGURACIÓN DE KESTREL ====================
app.Urls.Clear();
app.Urls.Add("http://localhost:5000"); 

// ==================== RUN ====================
app.Run();


// 54.144.192.111:8080/inicio