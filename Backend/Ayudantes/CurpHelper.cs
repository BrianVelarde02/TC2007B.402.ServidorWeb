namespace Backend.Ayudantes
{
    public static class CurpHelper
    {
        /*
        Extrae la fecha de nacimiento de una CURP válida.
        Devuelve null si la CURP no es válida o la fecha no puede ser interpretada.
        */
        public static DateTime? ExtraerFechaDeCurp(string curp)
        {
            if (string.IsNullOrEmpty(curp) || curp.Length < 10)
                return null;

            try
            {
                string anioStr = curp.Substring(4, 2); // posiciones 4-5
                string mesStr = curp.Substring(6, 2);  // posiciones 6-7
                string diaStr = curp.Substring(8, 2);  // posiciones 8-9

                if (int.TryParse(anioStr, out int anio) &&
                    int.TryParse(mesStr, out int mes) &&
                    int.TryParse(diaStr, out int dia))
                {
                    // Determinar siglo (1900s o 2000s)
                    if (anio <= DateTime.UtcNow.Year % 100)
                        anio += 2000;
                    else
                        anio += 1900;

                    return new DateTime(anio, mes, dia);
                }
            }
            catch
            {
                // Si hay error en el formato o fecha inválida
                return null;
            }

            return null;
        }
    }
}
