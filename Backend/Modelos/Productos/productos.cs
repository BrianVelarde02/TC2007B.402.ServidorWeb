namespace Backend.Modelos.Productos    
{
    public class productos
    {
        public int id { get; set; }  // id INT (Primary Key)
    
        public int id_negocio { get; set; }
        
        public int precio_centavos { get; set; }
        
        public int stock_cantidad { get; set; }
        
        public bool esta_activo { get; set; } 
        
        public DateTime creado_en { get; set; } = DateTime.UtcNow;
    }
}