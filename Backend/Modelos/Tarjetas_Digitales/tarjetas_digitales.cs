namespace Backend.Modelos.Tarjetas_Digitales    
{
    public class tarjetas_digitales
    {
        public int id { get; set; }  // id INT (Primary Key)
    
        public int id_usuario { get; set; }
    
        public string numero_tarjeta { get; set; } // VARCHAR(50)
    
        public string estado { get; set; }
        
        public DateTime emitida_en { get; set; } = DateTime.UtcNow;
    
        public DateTime expira_en { get; set; } = DateTime.UtcNow; // TIMESTAMP
    }
}