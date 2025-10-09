namespace Backend.Modelos.Negocios    
{
    public class negocios
    {
        public int id { get; set; }  // id INT (Primary Key)
    
        public string nombre { get; set; } = string.Empty; // VARCHAR(255)
    
        public string? direccion { get; set; } // VARCHAR(255)
    
        public int id_categoria { get; set; }
        
        public int id_propietario_usuario { get; set; }
    
        public DateTime creado_en { get; set; } = DateTime.UtcNow; // TIMESTAMP
    }
}