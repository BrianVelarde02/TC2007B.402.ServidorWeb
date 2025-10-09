namespace Backend.Modelos.Tarjetas_Digitales    
{
    public class tarjetas_digitales
    {
        public int id { get; set; }  // int UN AI PK
    
        public int id_usuario { get; set; } // int UN
    
        public string numero_tarjeta { get; set; } // VARCHAR(50)
    
        public string estado { get; set; } //enum('ACTIVA','SUSPENDIDA','EXPIRADA')
        
        public DateTime emitida_en { get; set; } = DateTime.UtcNow; //datetime
    
        public DateTime expira_en { get; set; } = DateTime.UtcNow; // datetime
        
    }
}