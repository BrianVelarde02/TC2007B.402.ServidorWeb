using Isopoh.Cryptography.Argon2;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Backend.Modelos.Usuario;
using Backend.Modelos.Tarjetas_Digitales;

namespace Backend.Servicios
{
    public interface IServicioAutenticacion
    {
        Task<UsuarioDto> RegistrarAsync(UsuarioCrearDto dto, IDataProtector protector);
        Task<UsuarioDto> LoginAsync(UsuarioLoginDto dto, IDataProtector protector);
        string GenerarToken(UsuarioDto usuario);
    }

    public class ServicioAutenticacion : IServicioAutenticacion
    {
        private readonly ApiDbContext _db;
        private readonly IConfiguration _config;

        public ServicioAutenticacion(ApiDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        // MÉTODO USADO PARA DESENCRIPTAR
        public static string SafeUnprotect(IDataProtector protector, string encrypted)
        {
            if (string.IsNullOrEmpty(encrypted)) return encrypted;
            try { return protector.Unprotect(encrypted); }
            catch { return encrypted; }
        }

        public async Task<UsuarioDto> RegistrarAsync(UsuarioCrearDto dto, IDataProtector protector)
        {
            if (string.IsNullOrWhiteSpace(dto.correo))
                throw new ArgumentException("Correo es obligatorio");
            if (string.IsNullOrWhiteSpace(dto.contrasena))
                throw new ArgumentException("Contraseña es obligatoria");
            
            var usuarios = await _db.usuarios.ToListAsync();

            // Validaciones únicas
            if (usuarios.Any(u => SafeUnprotect(protector, u.correo) == dto.correo))
                throw new InvalidOperationException("El correo ya está registrado");
            if (usuarios.Any(u => SafeUnprotect(protector, u.telefono) == dto.telefono))
                throw new InvalidOperationException("El teléfono ya está registrado");
            if (usuarios.Any(u => SafeUnprotect(protector, u.curp) == dto.curp))
                throw new InvalidOperationException("El CURP ya está registrado");

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

            // -------------------------
            // Generar tarjeta automáticamente
            string numeroTarjeta;
            do
            {
                numeroTarjeta = GenerarNumeroTarjeta();
            } while (await _db.tarjetas_digitales.AnyAsync(t => t.numero_tarjeta == numeroTarjeta));

            var tarjeta = new tarjetas_digitales
            {
                id_usuario = user.id,
                numero_tarjeta = numeroTarjeta,
                estado = "ACTIVA",
                emitida_en = DateTime.UtcNow,
                expira_en = DateTime.UtcNow.AddYears(3)
            };

            _db.tarjetas_digitales.Add(tarjeta);
            await _db.SaveChangesAsync();
            // -------------------------

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

        private string GenerarNumeroTarjeta()
        {
            var rnd = new Random();
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 16; i++)
            {
                sb.Append(rnd.Next(0, 10));
            }
            return sb.ToString();
        }

        // MÉTODO DE LOGIN
        public async Task<UsuarioDto> LoginAsync(UsuarioLoginDto dto, IDataProtector protector)
        {
            if (string.IsNullOrWhiteSpace(dto.Correo))
                throw new ArgumentException("Correo es obligatorio");
            if (string.IsNullOrWhiteSpace(dto.Contrasena))
                throw new ArgumentException("Contraseña es obligatoria");

            var usuarios = await _db.usuarios.ToListAsync();
            var user = usuarios.FirstOrDefault(u =>
                SafeUnprotect(protector, u.correo).Equals(dto.Correo, StringComparison.OrdinalIgnoreCase)
            );

            if (user == null)
                throw new InvalidOperationException("Usuario no encontrado");

            if (!Argon2.Verify(user.hash_contrasena, dto.Contrasena))
                throw new ArgumentException("Contraseña incorrecta");

            var usuarioDto = new UsuarioDto
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

            return usuarioDto;
        }

        // MÉTODO PARA GENERAR TOKEN JWT
        public string GenerarToken(UsuarioDto usuario)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, usuario.Correo),
                new Claim("id", usuario.Id.ToString()),
                new Claim("tipo_usuario", usuario.TipoUsuario ?? "JOVEN"),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(3),
                signingCredentials: creds
            );
            
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
