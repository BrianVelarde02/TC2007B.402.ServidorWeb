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
            // =================================================================
            // ENDPOINTS DE USUARIOS Y AUTENTICACIÓN
            // =================================================================

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

            // ------------------- LOGIN CON TOKEN -------------------
            app.MapPost("/api/auth/login-token", async (IServicioAutenticacion servicio, UsuarioLoginDto dto,
            IDataProtectionProvider provider, ApiDbContext db) =>
            {
                var protector = provider.CreateProtector("UserData");
                try
                {
                    // Login
                    var usuario = await servicio.LoginAsync(dto, protector);

                    // Inicializar id_negocio en null
                    int? idNegocio = null;

                    // Si el usuario es de tipo NEGOCIO, buscamos su negocio
                    if (usuario.TipoUsuario?.ToUpper() == "NEGOCIO")
                    {
                        var negocio = await db.negocios
                            .FirstOrDefaultAsync(n => n.id_propietario_usuario == usuario.Id);

                        if (negocio != null)
                            idNegocio = negocio.id;
                    }

                    // Generar token
                    var token = servicio.GenerarToken(usuario);

                    // Devolver info
                    return Results.Ok(new
                    {
                        mensaje = "Inicio de sesión exitoso",
                        token,
                        usuario = new
                        {
                            usuario.Id,
                            usuario.Nombre,
                            usuario.Apellidos,
                            usuario.Correo,
                            usuario.TipoUsuario
                        },
                        id_negocio = idNegocio
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
            })
            .RequireRateLimiting("login-limiter")
            .WithName("LoginUsuarioConToken");
            
            // ------------------- LISTAR USUARIOS -------------------
            app.MapGet("/usuarios/lista", async (ApiDbContext db, IDataProtectionProvider provider) =>
            {
                var protector = provider.CreateProtector("UserData");
                var usuarios = await db.usuarios.ToListAsync();

                var listaUsuarios = usuarios.Select(u =>
                {
                    int id = u.id;
                    string nombre = ServicioAutenticacion.SafeUnprotect(protector, u.nombre);
                    string apellidos = ServicioAutenticacion.SafeUnprotect(protector, u.apellidos);
                    string telefono = ServicioAutenticacion.SafeUnprotect(protector, u.telefono ?? "");
                    string correo = ServicioAutenticacion.SafeUnprotect(protector, u.correo);
                    string curp = ServicioAutenticacion.SafeUnprotect(protector, u.curp ?? "");
                    string direccion = ServicioAutenticacion.SafeUnprotect(protector, u.direccion);
                    string tipo_usuario = u.tipo_usuario;

                    DateTime? fechaNacimiento = CurpHelper.ExtraerFechaDeCurp(curp);

                    return new
                    {
                        id = id,
                        Nombre = nombre,
                        Apellidos = apellidos,
                        Curp = curp,
                        FechaNacimiento = fechaNacimiento,
                        Telefono = telefono,
                        Correo = correo,
                        Direccion = direccion,
                        TipoUsuario = tipo_usuario
                    };
                }).ToList();

                return Results.Ok(listaUsuarios);
            })
            .RequireAuthorization("SoloAdmins")
            .WithName("ListaUsuarios");

            // ------------------- OBTENER QR DE USUARIO -------------------
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
                    id_tarjeta = id_tarjeta
                };

                string jsonData = System.Text.Json.JsonSerializer.Serialize(data);

                // Generar QR en SVG
                using var qrGenerator = new QRCodeGenerator();
                var qrData = qrGenerator.CreateQrCode(jsonData, QRCodeGenerator.ECCLevel.Q);
                var qrCode = new SvgQRCode(qrData);
                string svgImage = qrCode.GetGraphic(5);

                return Results.Content(svgImage, "image/svg+xml");
            })
            .WithName("GenerarQRPorUsuario");

            // ------------------- EDITAR USUARIO -------------------
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

                if (!string.IsNullOrWhiteSpace(usuarioActualizado.Correo))
                    usuarioExistente.correo = protector.Protect(usuarioActualizado.Correo);


                await db.SaveChangesAsync();

                return Results.Ok(new
                {
                    mensaje = "Usuario actualizado correctamente.",
                    usuario = new
                    {
                        id = usuarioExistente.id,
                        nombre = usuarioActualizado.Nombre,
                        apellidos = usuarioActualizado.Apellidos,
                        correo = usuarioActualizado.Correo,
                        telefono = usuarioActualizado.Telefono,
                        direccion = usuarioActualizado.Direccion,
                        curp = usuarioActualizado.Curp
                    }
                });
            })
            .RequireAuthorization("SoloAdmins")
            .WithName("ActualizarUsuarioPorId");

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
            })
            .RequireAuthorization("SoloAdmins")
            .WithName("EliminarUsuario");

            // =================================================================
            // ENDPOINTS DE NEGOCIOS
            // =================================================================

            // ------------------- AGREGAR NEGOCIO -------------------
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
            })
            .RequireAuthorization("SoloAdmins")
            .WithName("AgregarNegocio");

            // ------------------- LISTAR NEGOCIOS -------------------
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
            })
            //.RequireAuthorization()
            .WithName("ListarNegocios");
            
            // ------------------- OBTENER NEGOCIO POR ID -------------------
            app.MapGet("/api/negocios/{id:int}", async (int id, ApiDbContext db) =>
            {
                var negocio = await db.negocios.FindAsync(id);

                if (negocio == null)
                {
                    return Results.NotFound(new { mensaje = "Negocio no encontrado" });
                }

                return Results.Ok(negocio);
            })
            .RequireAuthorization()
            .WithName("ObtenerNegocioPorId");

            // ------------------- OBTENER VISITAS POR NEGOCIO -------------------
            app.MapGet("/api/negocios/{id_negocio:int}/visitas", async (int id_negocio, ApiDbContext db) =>
            {
                // Verificar que el negocio exista
                var negocio = await db.negocios.FindAsync(id_negocio);
                if (negocio == null)
                    return Results.NotFound(new { mensaje = "Negocio no encontrado." });

                // Contar redenciones asociadas a este negocio
                var totalVisitas = await db.redenciones
                    .Join(db.descuentos,
                          r => r.id_descuento,
                          d => d.id,
                          (r, d) => new { Redencion = r, Descuento = d })
                    .Where(x => x.Descuento.id_negocio == id_negocio)
                    .CountAsync();

                return Results.Ok(new
                {
                    Negocio = negocio.nombre,
                    TotalVisitas = totalVisitas
                });
            })
            .RequireAuthorization()
            .WithName("ObtenerVisitasPorNegocio");
            
            // ------------------- BORRAR NEGOCIO (SOLO ADMINS) -------------------
            app.MapDelete("/api/negocios/{id:int}", async (int id, ApiDbContext db) =>
            {
                // 1. Buscar el negocio
                var negocio = await db.negocios.FindAsync(id);
                if (negocio == null)
                {
                    return Results.NotFound(new { mensaje = "Negocio no encontrado" });
                }

                // 2. (¡IMPORTANTE!) Revisar si tiene datos asociados
                // No puedes borrar un negocio si tiene productos o descuentos que dependen de él.
                
                // Revisar si tiene productos
                bool tieneProductos = await db.productos.AnyAsync(p => p.id_negocio == id);
                if (tieneProductos)
                {
                    return Results.BadRequest(new { mensaje = "Error: No se puede borrar el negocio porque tiene productos asociados. Debe borrarlos primero." });
                }

                // Revisar si tiene descuentos generales
                bool tieneDescuentos = await db.descuentos.AnyAsync(d => d.id_negocio == id);
                if (tieneDescuentos)
                {
                    return Results.BadRequest(new { mensaje = "Error: No se puede borrar el negocio porque tiene descuentos generales asociados. Debe borrarlos primero." });
                }
                
                // (Si tienes otras tablas como "visitas" que dependan del negocio, añade un chequeo aquí también)

                // 3. Si pasa los chequeos, borrar
                db.negocios.Remove(negocio);
                await db.SaveChangesAsync();

                // 4. Retornar éxito (HTTP 204 No Content es estándar para DELETE)
                return Results.NoContent();
            })
            .WithName("BorrarNegocio")
            .RequireAuthorization("SoloAdmins"); 

            // =================================================================
            // ENDPOINTS DE PRODUCTOS
            // =================================================================

            // ------------------- AGREGAR PRODUCTO -------------------
            app.MapPost("/api/productos", async (productos nuevoProducto, ApiDbContext db) =>
            {
                db.productos.Add(nuevoProducto);
                await db.SaveChangesAsync();
                return Results.Created($"/api/productos/{nuevoProducto.id}", nuevoProducto);
            })
            .RequireAuthorization("ParaNegocios")
            .WithName("AgregarProducto");

            // ------------------- LISTAR PRODUCTOS -------------------
            app.MapGet("/api/productos", async (ApiDbContext db) =>
            {
                var lista = await db.productos.ToListAsync();
                return Results.Ok(lista);
            }).WithName("ListaProductos");

            // ------------------- LISTAR PRODUCTOS POR NEGOCIO -------------------
            app.MapGet("/api/productos/negocio/{id_negocio:int}", async (int id_negocio, ApiDbContext db) =>
            {
                var productosNegocio = await db.productos
                    .Where(p => p.id_negocio == id_negocio)
                    .ToListAsync();

                if (!productosNegocio.Any())
                    return Results.NotFound(new { mensaje = "No se encontraron productos para este negocio." });

                return Results.Ok(productosNegocio);
            })
            .RequireAuthorization("ParaNegocios")
            .WithName("ListaProductosNegocios");

            // ------------------- EDITAR PRODUCTO -------------------
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

            // ------------------- BORRAR PRODUCTO -------------------
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

            // =================================================================
            // ENDPOINTS DE DESCUENTOS
            // =================================================================

            // ------------------- AGREGAR DESCUENTO -------------------
            app.MapPost("/api/descuentos", async (descuentos nuevoDescuento, ApiDbContext db) =>
            {
                db.descuentos.Add(nuevoDescuento);
                await db.SaveChangesAsync();
                return Results.Created($"/api/descuentos/{nuevoDescuento.id}", nuevoDescuento);
            })
            .RequireAuthorization("ParaNegocios")
            .WithName("AgregarDescuento");

            // ------------------- LISTAR DESCUENTOS -------------------
            app.MapGet("/api/descuentos", async (ApiDbContext db) =>
            {
                var lista = await db.descuentos.ToListAsync();
                return Results.Ok(lista);
            })
            .WithName("ListaDescuentos");

            // ------------------- OBTENER DESCUENTO POR ID -------------------
            app.MapGet("/api/descuentos/{id:int}", async (int id, ApiDbContext db) =>
            {
                var descuento = await db.descuentos.FindAsync(id);

                if (descuento == null)
                {
                    return Results.NotFound(new { mensaje = "Descuento no encontrado" });
                }

                return Results.Ok(descuento);
            })
            .RequireAuthorization()
            .WithName("ObtenerDescuentoPorId");

            // ------------------- EDITAR DESCUENTO -------------------
            app.MapPut("/api/descuentos/{id:int}", async (int id, descuentos descuentoActualizado, ApiDbContext db) =>
            {
                var descuentoExistente = await db.descuentos.FindAsync(id);

                if (descuentoExistente == null)
                {
                    return Results.NotFound(new { mensaje = "Descuento no encontrado" });
                }

                // Actualizar los campos con los nombres de tu CLASE C#
                descuentoExistente.id_negocio = descuentoActualizado.id_negocio;
                descuentoExistente.id_producto = descuentoActualizado.id_producto;
                descuentoExistente.titulo = descuentoActualizado.titulo;

                // NOMBRES CORREGIDOS (¡AHORA SÍ!):
                descuentoExistente.tipo_descuento = descuentoActualizado.tipo_descuento;
                descuentoExistente.valor_descuento = descuentoActualizado.valor_descuento;
                descuentoExistente.inicia_en = descuentoActualizado.inicia_en;
                descuentoExistente.termina_en = descuentoActualizado.termina_en;

                descuentoExistente.esta_activo = descuentoActualizado.esta_activo;

                await db.SaveChangesAsync();

                return Results.Ok(new { mensaje = "Descuento actualizado correctamente", descuento = descuentoExistente });
            })
            .RequireAuthorization("ParaNegocios")
            .WithName("EditarDescuento");
            
            // ------------------- BORRAR DESCUENTO -------------------
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
            })
            .RequireAuthorization("ParaNegocios")
            .WithName("BorrarDescuento");
            
            // ------------------- OBTENER DESCUENTOS POR CATEGORIA -------------------
            app.MapGet("/api/descuentos/categoria/{id_categoria:int}", async (int id_categoria, ApiDbContext db) =>
            {
                // 1. (Opcional pero recomendado) Verificar que la categoría exista
                var categoria = await db.categorias.FindAsync(id_categoria);
                if (categoria == null)
                {
                    return Results.NotFound(new { mensaje = "Categoría no encontrada" });
                }

                // 2. Buscar descuentos ligados directamente a un negocio de esa categoría
                var descuentosPorNegocio = db.descuentos
                    .Join(db.negocios, // Unir descuentos con negocios
                        descuento => descuento.id_negocio,
                        negocio => negocio.id,
                        (descuento, negocio) => new { Descuento = descuento, Negocio = negocio })
                    .Where(joinResult => joinResult.Negocio.id_categoria == id_categoria) // Filtrar por categoría
                    .Select(joinResult => joinResult.Descuento); // Quedarnos solo con el descuento

                // 3. Buscar descuentos ligados a un producto de un negocio de esa categoría
                var descuentosPorProducto = db.descuentos
                    .Join(db.productos, // Unir descuentos con productos
                        descuento => descuento.id_producto,
                        producto => producto.id,
                        (descuento, producto) => new { Descuento = descuento, Producto = producto })
                    .Join(db.negocios, // Unir ese resultado con negocios
                        joinResultP => joinResultP.Producto.id_negocio,
                        negocio => negocio.id,
                        (joinResultP, negocio) => new { Descuento = joinResultP.Descuento, Negocio = negocio })
                    .Where(joinResultN => joinResultN.Negocio.id_categoria == id_categoria) // Filtrar por categoría
                    .Select(joinResultN => joinResultN.Descuento); // Quedarnos solo con el descuento

                // 4. Unir las dos listas y quitar duplicados
                var listaFinal = await descuentosPorNegocio
                    .Union(descuentosPorProducto) // Combina ambas consultas
                    .Distinct() // Asegura que no haya descuentos repetidos
                    .ToListAsync();

                return Results.Ok(listaFinal);
            })
            .WithName("ObtenerDescuentosPorCategoria");
            
            // =================================================================
            // ENDPOINTS DE CATEGORÍAS
            // =================================================================

            // ------------------- AGREGAR CATEGORÍA -------------------
            app.MapPost("/categorias/agregar", async (ApiDbContext db, categorias nuevaCategoria) =>
            {
                if (string.IsNullOrWhiteSpace(nuevaCategoria.nombre))
                    return Results.BadRequest(new { mensaje = "El nombre de la categoría es obligatorio" });

                db.categorias.Add(nuevaCategoria);
                await db.SaveChangesAsync();

                return Results.Ok(new { mensaje = "Categoría agregada correctamente", categoria = nuevaCategoria });
            })
            .RequireAuthorization("SoloAdmins")
            .WithName("AgregarCategoria");

            // ------------------- LISTAR CATEGORÍAS -------------------
            app.MapGet("/categorias/lista", async (ApiDbContext db) =>
            {
                var listaCategorias = await db.categorias.ToListAsync();
                return Results.Ok(listaCategorias);
            })
            .WithName("ListarCategorias");

            // =================================================================
            // ENDPOINTS DE REDENCIONES
            // =================================================================

            // ------------------- AGREGAR REDENCIÓN -------------------
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
            .RequireAuthorization("ParaNegocios")
            .WithName("AgregarRedencion");
            
            // ------------------- LISTAR REDENCIONES -------------------
            app.MapGet("/api/redenciones", async (ApiDbContext db) =>
            {
                var lista = await db.redenciones
                    .Select(r => new
                    {
                        id = r.id,
                        redimido_en = r.redimido_en
                    })
                    .ToListAsync();
            
                return Results.Ok(lista);
            })
            //.RequireAuthorization("SoloAdmins")
            .WithName("ListaRedenciones");


            // =================================================================
            // CÓDIGO COMENTADO (MOVIDO AL FINAL)
            // =================================================================
            

            /*
            //Obtener Logo del negocio
            app.MapGet("/api/negocios/{id}/logo", async (int id, ApiDbContext db) =>
            {
                var negocio = await db.negocios.FindAsync(id);
                if (negocio == null || negocio.logo == null)
                    return Results.NotFound(new { mensaje = "Logo no encontrado." });
            
                return Results.File(negocio.logo, "image/png"); // o "image/jpeg"
            }).WithName("GenerarLogo");
            
            //Subir logo a negocio
            app.MapPost("/api/negocios/{id_negocio:int}/logo", async (int id_negocio, IFormFile archivo, ApiDbContext db) =>
            {
                // Verificar que el negocio exista
                var negocio = await db.negocios.FindAsync(id_negocio);
                if (negocio == null)
                    return Results.NotFound(new { mensaje = "Negocio no encontrado" });
            
                // Validar archivo
                if (archivo == null || archivo.Length == 0)
                    return Results.BadRequest(new { mensaje = "Archivo no válido" });
            
                // Convertir el archivo a byte[]
                using var ms = new MemoryStream();
                await archivo.CopyToAsync(ms);
                negocio.logo = ms.ToArray();
            
                await db.SaveChangesAsync();
            
                return Results.Ok(new { mensaje = "Logo actualizado correctamente" });
            })
            .WithName("SubirLogoNegocio")
            .WithMetadata(new Microsoft.AspNetCore.Mvc.IgnoreAntiforgeryTokenAttribute());
            */
            
        } // <-- Cierre de MapearEndpointsAutenticacion
    } // <-- Cierre de la clase
} // <-- Cierre del namespace