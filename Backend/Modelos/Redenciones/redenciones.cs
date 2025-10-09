namespace Backend.Modelos.Redenciones    
{
    public class redenciones
    {
        public int id { get; set; }  // id INT (Primary Key)
    
        public int id_tarjeta { get; set; }
    
        public int id_descuento { get; set; } 
        
        public DateTime redimido_en { get; set; } = DateTime.UtcNow;
    }
}