using Backend.Servicios;
using Backend.Modelos.Usuario;
using Backend.Endpoints;

using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);


// Swagger para documentación de API
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// DbContext
builder.Services.AddDbContext<ApiDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        new MySqlServerVersion(new Version(8, 0, 36))
    ));

// Agregar Data Protection
builder.Services.AddDataProtection();

// Registrar servicios
builder.Services.AddScoped<IServicioCifrado, ServicioCifrado>();
builder.Services.AddScoped<IServicioAutenticacion, ServicioAutenticacion>();

var app = builder.Build();

// cosa del swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Mapear endpoints
app.MapearEndpointsAutenticacion();
app.MapearEndpointsTarjetas();

// Escuchar en todas las IPs externas y puerto 8080
app.Urls.Add("http://0.0.0.0:8080");

app.Run();


// 54.144.192.111:8080/inicio