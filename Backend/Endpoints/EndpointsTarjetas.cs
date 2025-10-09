using Backend.Modelos.Tarjetas_Digitales;
using Microsoft.EntityFrameworkCore;

namespace Backend.Endpoints
{
    public static class EndpointsTarjetas
    {
        public static void MapearEndpointsTarjetas(this WebApplication app)
        {
            // ------------------- LISTAR TARJETAS -------------------
            app.MapGet("/tarjetas/lista", async (ApiDbContext db) =>
            {
                var tarjetas = await db.tarjetas_digitales.ToListAsync();
                return Results.Ok(tarjetas);
            })
            .WithName("ListaTarjetas");
        }
    }
}
