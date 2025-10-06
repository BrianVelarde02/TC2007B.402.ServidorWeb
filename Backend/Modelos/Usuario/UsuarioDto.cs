namespace Backend.Modelos.Usuario
{
    public class UsuarioDto
    {
        public int Id { get; set; }
        public string Correo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Apellidos { get; set; } = string.Empty;
        public string? Telefono { get; set; }
        public string? Curp { get; set; } = "";
        public string? Direccion { get; set; }
        public string TipoUsuario { get; set; } = "JOVEN";
        public bool EstaActivo { get; set; } = true;
        public DateTime? CreadoEn { get; set; } = DateTime.UtcNow;
    }
}
