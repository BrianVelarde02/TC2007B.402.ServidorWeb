using Microsoft.EntityFrameworkCore;
using Backend.Modelos.Usuario;
using Backend.Modelos.Tarjetas_Digitales;
using Backend.Modelos.Redenciones;
using Backend.Modelos.Descuentos;
using Backend.Modelos.Negocios;
using Backend.Modelos.Categorias;
using Backend.Modelos.Productos;

public class ApiDbContext : DbContext
{
    public ApiDbContext(DbContextOptions<ApiDbContext> options) : base(options) { }

    // Tabla de usuarios
    public DbSet<usuarios> usuarios { get; set; }

    // Tabla de tarjetas digitales
    public DbSet<tarjetas_digitales> tarjetas_digitales { get; set; }
    
    // Tabla de Redenciones
    public DbSet<redenciones> redenciones { get; set; }
    
    // Tabla de Descuentos 
    public DbSet<descuentos> descuentos { get; set; }
    
    // Tabla de negocios
    public DbSet<negocios> negocios { get; set; }
    
    // Tabla de categorias
    public DbSet<categorias> categorias { get; set; }
    
    // Tabla de Productos
    public DbSet<productos> productos { get; set; }

}
