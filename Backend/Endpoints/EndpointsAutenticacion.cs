using Backend.Modelos.Usuario;
using Backend.Modelos.Tarjetas_Digitales;
using Backend.Modelos.Redenciones;
using Backend.Modelos.Descuentos;
using Backend.Modelos.Negocios;
using Backend.Modelos.Categorias;
using Backend.Modelos.Productos;
using Backend.Ayudantes;
using Backend.Servicios;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using QRCoder;


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
            
                // Borrar tarjetas asociadas
                var tarjetas = await db.tarjetas_digitales
                    .Where(t => t.id_usuario == id)
                    .ToListAsync();
                db.tarjetas_digitales.RemoveRange(tarjetas);
            
                // Borrar usuario
                db.usuarios.Remove(usuario);
                await db.SaveChangesAsync();
            
                return Results.Ok(new { mensaje = "Usuario y tarjetas eliminados correctamente" });
            }).WithName("EliminarUsuario");

            /**
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
            .WithName("ListaUsuarios");  */
            
            //Para generar el QR
            app.MapGet("/usuario/{id}/qr", async (ApiDbContext db, int id, IDataProtectionProvider provider) =>
            {
                // Buscar usuario
                var usuario = await db.usuarios.FindAsync(id);
                if (usuario == null)
                    return Results.NotFound(new { mensaje = "Usuario no encontrado" });
            
                var protector = provider.CreateProtector("UserData");
                string nombre = ServicioAutenticacion.SafeUnprotect(protector, usuario.nombre);
                string apellidos = ServicioAutenticacion.SafeUnprotect(protector, usuario.apellidos);
            
                // Buscar tarjetas del usuario
                var numero_tarjeta = await db.tarjetas_digitales
                    .Where(t => t.id_usuario == id)
                    .Select(t => t.numero_tarjeta)
                    .ToListAsync();
                
                // Buscar id de tarjeta
                var id_tarjeta = await db.tarjetas_digitales
                    .Where(t => t.id_usuario == id)
                    .Select(t => t.id)
                    .ToListAsync();
            
                // Datos que queremos en el QR
                var data = new
                {
                    nombre = nombre,
                    apellidos = apellidos,
                    numero_tarjeta = numero_tarjeta,
                    id_tarjeta =id_tarjeta
                };
            
                string jsonData = System.Text.Json.JsonSerializer.Serialize(data);
            
                // Generar QR en SVG
                using var qrGenerator = new QRCodeGenerator();
                var qrData = qrGenerator.CreateQrCode(jsonData, QRCodeGenerator.ECCLevel.Q);
                var qrCode = new SvgQRCode(qrData);
                string svgImage = qrCode.GetGraphic(5);
            
                return Results.Content(svgImage, "image/svg+xml");
            }).WithName("GenerarQRPorUsuario");
            
            //Agrega la redencion ESTE METODO ES QUE VIENEN DESPUES DEL QR EN LA APP
            app.MapPost("/api/redenciones/agregar", async (ApiDbContext db, redenciones nuevaRedencion) =>
            {
                // Validar que la tarjeta exista
                var tarjeta = await db.tarjetas_digitales.FindAsync(nuevaRedencion.id_tarjeta);
                if (tarjeta == null)
                    return Results.BadRequest(new { mensaje = "Tarjeta no encontrada" });
            
                // Validar que el descuento exista
                var descuento = await db.descuentos.FindAsync(nuevaRedencion.id_descuento);
                if (descuento == null)
                    return Results.BadRequest(new { mensaje = "Descuento no encontrado" });
            
                // Si no viene la fecha, se asigna la actual 
                if (nuevaRedencion.redimido_en == default)
                    nuevaRedencion.redimido_en = DateTime.UtcNow;
            
                db.redenciones.Add(nuevaRedencion);
                await db.SaveChangesAsync();
            
                return Results.Ok(new
                {
                    mensaje = "Redención registrada correctamente",
                    redencion = nuevaRedencion
                });
            })
            .WithName("AgregarRedencion");
            
            // Endpoint para agregar un negocio
            app.MapPost("/api/negocios/agregar", async (ApiDbContext db, negocios nuevoNegocio) =>
            {
                // Validar que el propietario exista
                var propietario = await db.usuarios.FindAsync(nuevoNegocio.id_propietario_usuario);
                if (propietario == null)
                    return Results.BadRequest(new { mensaje = "Usuario propietario no encontrado" });
            
                // Validar que la categoría exista (si tienes tabla de categorias)
                var categoria = await db.categorias.FindAsync(nuevoNegocio.id_categoria);
                if (categoria == null)
                    return Results.BadRequest(new { mensaje = "Categoría no encontrada" });
            
                // Asignar fecha actual si no se envía (ya tienes valor por defecto)
                if (nuevoNegocio.creado_en == default)
                    nuevoNegocio.creado_en = DateTime.UtcNow;
            
                db.negocios.Add(nuevoNegocio);
                await db.SaveChangesAsync();
            
                return Results.Ok(new
                {
                    mensaje = "Negocio agregado correctamente",
                    negocio = nuevoNegocio
                });
            }).WithName("AgregarNegocio");
            
            // Endpoint para listar negocios
            app.MapGet("/api/negocios/lista", async (ApiDbContext db) =>
            {
                var negociosLista = await db.negocios
                    .Select(n => new
                    {
                        n.id,
                        n.nombre,
                        n.direccion,
                        n.id_categoria,
                        n.id_propietario_usuario,
                        n.creado_en
                    })
                    .ToListAsync();
            
                return Results.Ok(negociosLista);
            }).WithName("ListarNegocios");
            
            // ------------------- AGREGAR CATEGORÍA -------------------
            app.MapPost("/categorias/agregar", async (ApiDbContext db, categorias nuevaCategoria) =>
            {
                if (string.IsNullOrWhiteSpace(nuevaCategoria.nombre))
                    return Results.BadRequest(new { mensaje = "El nombre de la categoría es obligatorio" });
            
                db.categorias.Add(nuevaCategoria);
                await db.SaveChangesAsync();
            
                return Results.Ok(new { mensaje = "Categoría agregada correctamente", categoria = nuevaCategoria });
            }).WithName("AgregarCategoria");
            
            // ------------------- LISTAR CATEGORÍAS -------------------
            app.MapGet("/categorias/lista", async (ApiDbContext db) =>
            {
                var listaCategorias = await db.categorias.ToListAsync();
                return Results.Ok(listaCategorias);
            }).WithName("ListarCategorias");
            
            // Agregar un nuevo descuento
            app.MapPost("/api/descuentos", async (descuentos nuevoDescuento, ApiDbContext db) =>
            {
                db.descuentos.Add(nuevoDescuento);
                await db.SaveChangesAsync();
                return Results.Created($"/api/descuentos/{nuevoDescuento.id}", nuevoDescuento);
            }).WithName("AgregarDescuento");
            
            // Obtener la lista completa de descuentos
            app.MapGet("/api/descuentos", async (ApiDbContext db) =>
            {
                var lista = await db.descuentos.ToListAsync();
                return Results.Ok(lista);
            }).WithName("ListaDescuentos");
            
            //Borrar descuentos
            app.MapDelete("/descuentos/{id}", async (int id, ApiDbContext db) =>
            {
                var descuento = await db.descuentos.FindAsync(id);
                if (descuento is null)
                {
                    return Results.NotFound(new { mensaje = "Descuento no encontrado." });
                }
            
                db.descuentos.Remove(descuento);
                await db.SaveChangesAsync();
            
                return Results.Ok(new { mensaje = "Descuento eliminado correctamente.", descuento });
            }).WithName("BorrarDescuento");

            
            // Agregar un nuevo producto
            app.MapPost("/api/productos", async (productos nuevoProducto, ApiDbContext db) =>
            {
                db.productos.Add(nuevoProducto);
                await db.SaveChangesAsync();
                return Results.Created($"/api/productos/{nuevoProducto.id}", nuevoProducto);
            }).WithName("AgregarProducto");
            
            // Obtener la lista completa de productos
            app.MapGet("/api/productos", async (ApiDbContext db) =>
            {
                var lista = await db.productos.ToListAsync();
                return Results.Ok(lista);
            }).WithName("ListaProductos");
            
            // Obtener productos por negocio
            app.MapGet("/api/productos/negocio/{id_negocio:int}", async (int id_negocio, ApiDbContext db) =>
            {
                var productosNegocio = await db.productos
                    .Where(p => p.id_negocio == id_negocio)
                    .ToListAsync();
            
                if (!productosNegocio.Any())
                    return Results.NotFound(new { mensaje = "No se encontraron productos para este negocio." });
            
                return Results.Ok(productosNegocio);
            }).WithName("ListaProductosNegocios");
            
            // Editar producto
            app.MapPut("/api/productos/{id}", async (int id, productos productoActualizado, ApiDbContext db) =>
            {
                var productoExistente = await db.productos.FindAsync(id);
                if (productoExistente is null)
                {
                    return Results.NotFound(new { mensaje = "Producto no encontrado." });
                }
            
                // Actualizar solo los campos relevantes
                productoExistente.id_negocio = productoActualizado.id_negocio;
                productoExistente.nombre = productoActualizado.nombre;
                productoExistente.precio_centavos = productoActualizado.precio_centavos;
                productoExistente.stock_cantidad = productoActualizado.stock_cantidad;
                productoExistente.esta_activo = productoActualizado.esta_activo;
            
                await db.SaveChangesAsync();
            
                return Results.Ok(new { mensaje = "Producto actualizado correctamente.", producto = productoExistente });
            }).WithName("EditarProducto");
            
            app.MapDelete("/api/productos/{id}", async (int id, ApiDbContext db) =>
            {
                var producto = await db.productos.FindAsync(id);
                if (producto is null)
                {
                    return Results.NotFound(new { mensaje = "Producto no encontrado." });
                }
            
                db.productos.Remove(producto);
                await db.SaveChangesAsync();
            
                return Results.Ok(new { mensaje = "Producto eliminado correctamente.", producto });
            })
            .RequireAuthorization("ParaNegocios")
            .WithName("BorrarProducto");
            
            
            // ------------------- LOGIN CON TOKEN -------------------
            app.MapPost("/api/auth/login-token", async (IServicioAutenticacion servicio, UsuarioLoginDto dto, 
                IDataProtectionProvider provider, IConfiguration config) =>
            {
                var protector = provider.CreateProtector("UserData");
                try
                {
                    var usuario = await servicio.LoginAsync(dto, protector);
                    var token = servicio.GenerarToken(usuario);
            
                    return Results.Ok(new
                    {
                        mensaje = "Inicio de sesión exitoso",
                        token,
                        usuario
                    });
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
            }).WithName("LoginUsuarioConToken");
            
            //probando token
            app.MapGet("/usuarios/lista", async (ApiDbContext db, IDataProtectionProvider provider) =>
            {
                var protector = provider.CreateProtector("UserData");
                var usuarios = await db.usuarios.ToListAsync();
            
                var listaUsuarios = usuarios.Select(u =>
                {
                    string nombre = ServicioAutenticacion.SafeUnprotect(protector, u.nombre);
                    string apellidos = ServicioAutenticacion.SafeUnprotect(protector, u.apellidos);
                    string telefono = ServicioAutenticacion.SafeUnprotect(protector, u.telefono ?? "");
                    string correo = ServicioAutenticacion.SafeUnprotect(protector, u.correo);
                    string curp = ServicioAutenticacion.SafeUnprotect(protector, u.curp ?? "");
            
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
            //.RequireAuthorization("SoloAdmins")
            .WithName("ListaUsuarios");
            
            
            app.MapPut("/api/usuarios/{id}", async (int id, UsuarioDto usuarioActualizado, ApiDbContext db, IDataProtectionProvider provider) =>
            {
                var protector = provider.CreateProtector("UserData");
            
                var usuarioExistente = await db.usuarios.FindAsync(id);
                if (usuarioExistente == null)
                    return Results.NotFound(new { mensaje = "Usuario no encontrado" });
            
                // Actualiza solo los campos que se envíen
                if (!string.IsNullOrWhiteSpace(usuarioActualizado.Nombre))
                    usuarioExistente.nombre = protector.Protect(usuarioActualizado.Nombre);
            
                if (!string.IsNullOrWhiteSpace(usuarioActualizado.Apellidos))
                    usuarioExistente.apellidos = protector.Protect(usuarioActualizado.Apellidos);
            
                if (!string.IsNullOrWhiteSpace(usuarioActualizado.Telefono))
                    usuarioExistente.telefono = protector.Protect(usuarioActualizado.Telefono);
            
                if (!string.IsNullOrWhiteSpace(usuarioActualizado.Direccion))
                    usuarioExistente.direccion = protector.Protect(usuarioActualizado.Direccion);
            
                if (!string.IsNullOrWhiteSpace(usuarioActualizado.Curp))
                    usuarioExistente.curp = protector.Protect(usuarioActualizado.Curp);
            
                await db.SaveChangesAsync();
            
                return Results.Ok(new
                {
                    mensaje = "Usuario actualizado correctamente.",
                    usuario = new
                    {
                        id = usuarioExistente.id,
                        nombre = usuarioActualizado.Nombre,
                        apellidos = usuarioActualizado.Apellidos,
                        telefono = usuarioActualizado.Telefono,
                        direccion = usuarioActualizado.Direccion,
                        curp = usuarioActualizado.Curp
                    }
                });
            });
        }
    }
}
