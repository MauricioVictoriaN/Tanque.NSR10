// ---------------------------------------------------------------------------
// Tanque.NSR10
// (c) Mauricio Javier Victoria Niño <hidratecsa@gmail.com> · CC BY-NC-SA 4.0
// Uso exclusivamente académico. El motor de cálculo (Tanque.Core) se distribuye
// como binario ofuscado, protegido por derechos de autor (ver LICENSE).
// ---------------------------------------------------------------------------
namespace Tanque.Reportes;

/// <summary>
/// Escala de color divergente (azul → blanco → rojo) para los mapas de calor de momento de la
/// pestaña "Diagramas" y del reporte HTML (Fase 3). Es pura PRESENTACIÓN (un mapeo valor→color),
/// sin contenido normativo ni de cálculo; se comparte entre Tanque.App y el generador HTML para que
/// ambos usen exactamente el mismo color por valor. Principio rector: no introduce ninguna fórmula
/// ni valor de diseño -- solo pinta el campo de momento ya verificado por el núcleo.
/// </summary>
public static class MapaDeColor
{
    /// <summary>
    /// Color RGB para un valor dentro de [<paramref name="minimo"/>, <paramref name="maximo"/>].
    /// Escala divergente: mínimo → azul, cero → blanco, máximo → rojo.
    /// </summary>
    public static (byte R, byte G, byte B) Color(double valor, double minimo, double maximo)
    {
        if (Math.Abs(maximo - minimo) < 1e-12)
            return (224, 224, 224);

        double t = Math.Clamp((valor - minimo) / (maximo - minimo), 0.0, 1.0);

        // Divergente: mínimo → azul (30,90,190); centro → blanco (255,255,255); máximo → rojo (210,50,50).
        const double rAzul = 30, gAzul = 90, bAzul = 190;
        const double rRojo = 210, gRojo = 50, bRojo = 50;

        if (t < 0.5)
        {
            double k = t / 0.5;
            return ((byte)(rAzul + (255 - rAzul) * k), (byte)(gAzul + (255 - gAzul) * k), (byte)(bAzul + (255 - bAzul) * k));
        }

        double k2 = (t - 0.5) / 0.5;
        return ((byte)(255 - (255 - rRojo) * k2), (byte)(255 - (255 - gRojo) * k2), (byte)(255 - (255 - bRojo) * k2));
    }

    /// <summary>
    /// Rango de color SIMÉTRICO y anclado en cero para un campo de momento: devuelve
    /// (-máx|v|, +máx|v|), de modo que el CERO sea SIEMPRE el centro de la escala divergente
    /// (blanco), el sagging (positivo) vaya hacia el rojo y el hogging (negativo) hacia el azul,
    /// con el MISMO significado en todos los mapas. Cada mapa de cara es de un solo signo (la cara
    /// interior/inferior trae el campo positivo, la exterior/superior el negativo); si en cambio se
    /// usara el rango [min, max] de cada grilla, el cero caería en un extremo saturado (azul en un
    /// mapa, rojo en otro) y el color sería engañoso -- por ejemplo, zonas de momento ≈0 pintadas de
    /// azul dentro de un mapa de cara interior. Presentación pura: no introduce ninguna fórmula ni
    /// valor de diseño, solo decide cómo colorear el campo ya verificado.
    /// </summary>
    public static (double Minimo, double Maximo) RangoSimetrico(double[,] valores)
    {
        double maxAbs = 0.0;
        foreach (var v in valores) maxAbs = Math.Max(maxAbs, Math.Abs(v));
        if (maxAbs < 1e-12) return (-1.0, 1.0); // campo de ceros: escala neutra para no dividir por 0.
        return (-maxAbs, maxAbs);
    }
}
