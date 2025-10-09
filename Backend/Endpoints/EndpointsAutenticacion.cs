using Backend.Modelos.Usuario;
using Backend.Ayudantes;
using Backend.Servicios;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Backend.Endpoints
{
    public static class EndpointsAutenticacion
    {
        public static void MapearEndpointsAutenticacion(this WebApplication app)
        {
            // ------------------- REGISTRO -------------------
            app.MapPost("/api/auth/registrar", async (IServicioAutenticacion servicio, UsuarioCrearDto dto, IDataProtectionProvider provider) =>
            {
                var protector = provider.CreateProtector("UserData");
                try
                {
                    var creado = await servicio.RegistrarAsync(dto, protector);
                    return Results.Ok(creado);
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { mensaje = ex.Message });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { mensaje = ex.Message });
                }
                catch (Exception ex)
                {
                    return Results.Problem(detail: ex.Message);
                }
            }).WithName("RegistrarUsuario");

            // ------------------- LOGIN -------------------
            app.MapPost("/api/auth/login", async (IServicioAutenticacion servicio, UsuarioLoginDto dto, IDataProtectionProvider provider) =>
            {
                var protector = provider.CreateProtector("UserData");
                try
                {
                    var usuario = await servicio.LoginAsync(dto, protector);
                    return Results.Ok(usuario);
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { mensaje = ex.Message });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.NotFound(new { mensaje = ex.Message });
                }
                catch (Exception ex)
                {
                    return Results.Problem(detail: ex.Message);
                }
            }).WithName("LoginUsuario");
            
            //POSIBLEMENTE MUEVO ESTO A UN ARCHIVO PROPIO PARA ALGUNO DE UTILIDADES
            // ------------------- ELIMINAR USUARIO -------------------
            app.MapDelete("/api/auth/usuario/{id:int}", async (ApiDbContext db, int id) =>
            {
                var usuario = await db.usuarios.FindAsync(id);
                if (usuario == null)
                    return Results.NotFound(new { mensaje = "Usuario no encontrado" });

                db.usuarios.Remove(usuario);
                await db.SaveChangesAsync();

                return Results.Ok(new { mensaje = $"Usuario eliminado correctamente" });
            }).WithName("EliminarUsuario");
            
            // Visualizar lista de usuarios 
            app.MapGet("/usuarios/lista", async (ApiDbContext db, IDataProtectionProvider provider) =>
            {
                var protector = provider.CreateProtector("UserData");
                
                var usuarios = await db.usuarios.ToListAsync();
                
                var listaUsuarios = usuarios.Select(u =>
                {
                    //Desencriptando la info
                    string nombre = ServicioAutenticacion.SafeUnprotect(protector, u.nombre);
                    string apellidos = ServicioAutenticacion.SafeUnprotect(protector, u.apellidos);
                    string telefono = ServicioAutenticacion.SafeUnprotect(protector, u.telefono ?? "");
                    string correo = ServicioAutenticacion.SafeUnprotect(protector, u.correo);
                    string curp = ServicioAutenticacion.SafeUnprotect(protector, u.curp ?? "");

                    // Funcion para extraer la fecha
                    DateTime? fechaNacimiento = CurpHelper.ExtraerFechaDeCurp(curp);
            
                    return new
                    {
                        Nombre = nombre,
                        Apellidos = apellidos,
                        Curp = curp,
                        FechaNacimiento = fechaNacimiento,
                        Telefono = telefono,
                        Correo = correo
                    };
                }).ToList();
            
                return Results.Ok(listaUsuarios);
            })
            .WithName("ListaUsuarios");
        }
    }
}
