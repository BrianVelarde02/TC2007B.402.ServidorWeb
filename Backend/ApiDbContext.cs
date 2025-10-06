using Microsoft.EntityFrameworkCore;
using Backend.Modelos.Usuario;

public class ApiDbContext : DbContext
{
    public ApiDbContext(DbContextOptions<ApiDbContext> options) : base(options) { }

    public DbSet<usuarios> usuarios { get; set; }
}
