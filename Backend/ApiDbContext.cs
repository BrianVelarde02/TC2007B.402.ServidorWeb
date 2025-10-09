using Microsoft.EntityFrameworkCore;
using Backend.Modelos.Usuario;
using Backend.Modelos.Tarjetas_Digitales;

public class ApiDbContext : DbContext
{
    public ApiDbContext(DbContextOptions<ApiDbContext> options) : base(options) { }

    // Tabla de usuarios
    public DbSet<usuarios> usuarios { get; set; }

    // Tabla de tarjetas digitales
    public DbSet<tarjetas_digitales> tarjetas_digitales { get; set; }
}
