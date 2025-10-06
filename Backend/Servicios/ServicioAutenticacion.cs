using Isopoh.Cryptography.Argon2;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using Backend.Modelos.Usuario;

namespace Backend.Servicios
{
    public interface IServicioAutenticacion
    {
        Task<UsuarioDto> RegistrarAsync(UsuarioCrearDto dto, IDataProtector protector);
        Task<UsuarioDto> LoginAsync(UsuarioLoginDto dto, IDataProtector protector);
    }

    public class ServicioAutenticacion : IServicioAutenticacion
    {
        private readonly ApiDbContext _db;

        public ServicioAutenticacion(ApiDbContext db)
        {
            _db = db;
        }

        // ESTE ES EL METODO USADO PARA DESENCRIPTAR
        public static string SafeUnprotect(IDataProtector protector, string encrypted)
        {
            if (string.IsNullOrEmpty(encrypted)) return encrypted;
            try
            {
                return protector.Unprotect(encrypted);
            }
            catch
            {
                return encrypted;
            }
        }

        public async Task<UsuarioDto> RegistrarAsync(UsuarioCrearDto dto, IDataProtector protector)
        {
            if (string.IsNullOrWhiteSpace(dto.correo))
                throw new ArgumentException("Correo es obligatorio");
            if (string.IsNullOrWhiteSpace(dto.contrasena))
                throw new ArgumentException("Contraseña es obligatoria");

            // Traer todos los usuarios a memoria para validar correo único
            var usuarios = await _db.usuarios.ToListAsync();
            
            //Compronaciones
            if (usuarios.Any(u => SafeUnprotect(protector, u.correo) == dto.correo)) // Comprobacion de correo repetido
                throw new InvalidOperationException("El correo ya está registrado");
            if (usuarios.Any(u => SafeUnprotect(protector, u.telefono) == dto.telefono)) // Comprobacion de telefono repetido
                throw new InvalidOperationException("El telefono ya está registrado");
            if (usuarios.Any(u => SafeUnprotect(protector, u.curp) == dto.curp)) // Comprobacion de curp repetido
                throw new InvalidOperationException("El curp ya está registrado");

            // Hash de contraseña
            string passwordHash = Argon2.Hash(dto.contrasena);

            var user = new usuarios
            {
                correo = protector.Protect(dto.correo),
                hash_contrasena = passwordHash,
                nombre = protector.Protect(dto.nombre),
                apellidos = protector.Protect(dto.apellidos ?? ""),
                telefono = protector.Protect(dto.telefono ?? ""),
                curp = protector.Protect(dto.curp ?? ""),
                direccion = protector.Protect(dto.direccion ?? ""),
                tipo_usuario = string.IsNullOrWhiteSpace(dto.tipo_usuario) ? "JOVEN" : dto.tipo_usuario,
                esta_activo = true,
                creado_en = DateTime.UtcNow
            };

            _db.usuarios.Add(user);
            await _db.SaveChangesAsync();

            return new UsuarioDto
            {
                Id = user.id,
                Correo = dto.correo,
                Nombre = dto.nombre,
                Apellidos = dto.apellidos ?? "",
                Telefono = dto.telefono ?? "",
                Curp = dto.curp ?? "",
                Direccion = dto.direccion ?? "",
                TipoUsuario = user.tipo_usuario,
                EstaActivo = user.esta_activo,
                CreadoEn = user.creado_en
            };
        }
        
        //Metodo para el login
        public async Task<UsuarioDto> LoginAsync(UsuarioLoginDto dto, IDataProtector protector)
        {
            //Comprobaciones
            if (string.IsNullOrWhiteSpace(dto.Correo))
                throw new ArgumentException("Correo es obligatorio");
            if (string.IsNullOrWhiteSpace(dto.Contrasena))
                throw new ArgumentException("Contraseña es obligatoria");

            // Traer usuarios a memoria para desencriptar
            var usuarios = await _db.usuarios.ToListAsync();

            var user = usuarios.FirstOrDefault(u =>
                SafeUnprotect(protector, u.correo).Equals(dto.Correo, StringComparison.OrdinalIgnoreCase)
            );

            if (user == null)
                throw new InvalidOperationException("Usuario no encontrado");

            if (!Argon2.Verify(user.hash_contrasena, dto.Contrasena))
                throw new ArgumentException("Contraseña incorrecta");

            return new UsuarioDto
            {
                Id = user.id,
                Correo = dto.Correo,
                Nombre = SafeUnprotect(protector, user.nombre),
                Apellidos = SafeUnprotect(protector, user.apellidos),
                Telefono = SafeUnprotect(protector, user.telefono),
                Curp = SafeUnprotect(protector, user.curp),
                Direccion = SafeUnprotect(protector, user.direccion),
                TipoUsuario = user.tipo_usuario,
                EstaActivo = user.esta_activo,
                CreadoEn = user.creado_en
            };
        }
    }
}
