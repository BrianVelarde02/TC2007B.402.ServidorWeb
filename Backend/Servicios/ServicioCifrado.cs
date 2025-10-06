using Microsoft.AspNetCore.DataProtection;

namespace Backend.Servicios
{
    public interface IServicioCifrado
    {
        string Proteger(string textoPlano);
        string Desproteger(string textoCifrado);
    }

    public class ServicioCifrado : IServicioCifrado
    {
        private readonly IDataProtector _protector;

        public ServicioCifrado(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector("DatosUsuario");
        }

        public string Proteger(string textoPlano)
        {
            if (string.IsNullOrEmpty(textoPlano)) return textoPlano;
            return _protector.Protect(textoPlano);
        }

        public string Desproteger(string textoCifrado)
        {
            if (string.IsNullOrEmpty(textoCifrado)) return textoCifrado;
            try { return _protector.Unprotect(textoCifrado); }
            catch { return textoCifrado; } 
        }
    }
}
