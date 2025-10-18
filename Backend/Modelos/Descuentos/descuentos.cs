namespace Backend.Modelos.Descuentos    
{
    public class descuentos
    {
        public int id { get; set; }  // id INT (Primary Key)
    
        public int? id_negocio { get; set; }
        
        public int? id_producto { get; set; }
        
        public string titulo { get; set; }
        
        public string tipo_descuento { get; set; }
        
        public int valor_descuento { get; set; }
        
        public DateTime inicia_en { get; set; } = DateTime.UtcNow;
        
        public DateTime termina_en { get; set; } = DateTime.UtcNow;
    
        public bool esta_activo { get; set; } 
        
        public DateTime creado_en { get; set; } = DateTime.UtcNow;
    }
}