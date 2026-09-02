// ---------------------------------------------------------------------------
// Tanque.NSR10
// (c) Mauricio Javier Victoria Niño <hidratecsa@gmail.com> · CC BY-NC-SA 4.0
// Uso exclusivamente académico. El motor de cálculo (Tanque.Core) se distribuye
// como binario ofuscado, protegido por derechos de autor (ver LICENSE).
// ---------------------------------------------------------------------------
using System.Globalization;
using Tanque.Core.Modulos;

namespace Tanque.Reportes;

/// <summary>
/// Orientación física y marcado del gobernante para los mapas de calor de momento (2026-08-31,
/// mejora de usabilidad de la pestaña "Diagramas" y del reporte HTML). Es PRESENTACIÓN pura: no
/// calcula momento ni introduce ninguna fórmula -- solo traduce a palabras la convención geométrica
/// YA documentada y verificada en <see cref="DiagramaMomento"/> (muro: filas = altura completa,
/// columnas = semiluz 0.5·b; placa/cubierta/fondo: filas y columnas = semiluz por doble simetría).
/// Compartido entre Tanque.App y el generador HTML para que ambos impriman exactamente las mismas
/// etiquetas de borde (borde/centro, tope/base) y resalten la misma celda gobernante (máx |M|, la
/// que usa el diseño).
/// </summary>
public static class OrientacionMapa
{
    /// <summary>Verdadero si el campo pertenece a un muro (y no a una losa/placa).</summary>
    public static bool EsMuro(CampoMomento campo) =>
        campo.Elemento.StartsWith("Muro", StringComparison.Ordinal);

    /// <summary>Posición física (m) de la fila <paramref name="f"/> a lo largo de <see cref="CampoMomento.LuzFilasM"/>.</summary>
    public static double PosicionFila(CampoMomento campo, int f, int filas) =>
        filas > 1 ? f / (double)(filas - 1) * campo.LuzFilasM : 0.0;

    /// <summary>Posición física (m) de la columna <paramref name="c"/> a lo largo de <see cref="CampoMomento.LuzColumnasM"/>.</summary>
    public static double PosicionColumna(CampoMomento campo, int c, int cols) =>
        cols > 1 ? c / (double)(cols - 1) * campo.LuzColumnasM : 0.0;

    /// <summary>
    /// Etiqueta de la fila <paramref name="f"/> con la palabra del borde físico. En un muro las
    /// filas recorren la ALTURA completa: primera fila = tope (borde libre), última = base
    /// (empotrada). En una losa/placa las filas recorren el semiluz "a": primera = borde, última =
    /// centro. Las filas intermedias conservan solo la posición numérica.
    /// </summary>
    public static string EtiquetaFila(CampoMomento campo, int f, int filas)
    {
        var pos = PosicionFila(campo, f, filas).ToString("0.##", CultureInfo.InvariantCulture);
        if (f == 0) return EsMuro(campo) ? $"tope {pos}" : $"borde {pos}";
        if (f == filas - 1) return EsMuro(campo) ? $"base {pos}" : $"centro {pos}";
        return pos;
    }

    /// <summary>
    /// Etiqueta de la columna <paramref name="c"/> con la palabra del borde físico. Las columnas
    /// siempre recorren el semiluz "b" (tanto en muro como en losa): primera = borde, última =
    /// centro. Las columnas intermedias conservan solo la posición numérica.
    /// </summary>
    public static string EtiquetaColumna(CampoMomento campo, int c, int cols)
    {
        var pos = PosicionColumna(campo, c, cols).ToString("0.##", CultureInfo.InvariantCulture);
        if (c == 0) return $"borde {pos}";
        if (c == cols - 1) return $"centro {pos}";
        return pos;
    }

    /// <summary>
    /// Celda gobernante del campo: la de mayor |momento|, que es exactamente el valor que usa el
    /// diseño (pico del mapa == gobernante, ya verificado). Devuelve (fila, columna, valor con
    /// signo). Requiere un campo no vacío (los llamadores ya lo garantizan).
    /// </summary>
    public static (int Fila, int Columna, double Valor) CeldaGobernante(double[,] valores)
    {
        int filas = valores.GetLength(0), cols = valores.GetLength(1);
        int gf = 0, gc = 0;
        double g = Math.Abs(valores[0, 0]);
        for (int f = 0; f < filas; f++)
            for (int c = 0; c < cols; c++)
            {
                double a = Math.Abs(valores[f, c]);
                if (a > g) { g = a; gf = f; gc = c; }
            }
        return (gf, gc, valores[gf, gc]);
    }
}
