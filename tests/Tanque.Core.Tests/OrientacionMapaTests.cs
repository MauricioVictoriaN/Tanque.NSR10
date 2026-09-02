// ---------------------------------------------------------------------------
// Tanque.NSR10
// (c) Mauricio Javier Victoria Niño <hidratecsa@gmail.com> · CC BY-NC-SA 4.0
// Uso exclusivamente académico. El motor de cálculo (Tanque.Core) se distribuye
// como binario ofuscado, protegido por derechos de autor (ver LICENSE).
// ---------------------------------------------------------------------------
using Tanque.Core.Modulos;
using Tanque.Reportes;
using Xunit;

namespace Tanque.Core.Tests;

/// <summary>
/// Pruebas espejo de la orientación de los mapas de calor (2026-08-31, orientación al usuario no
/// experto): el anclaje en cero de la escala de color (<see cref="MapaDeColor.RangoSimetrico"/>) y
/// las etiquetas de borde físico / la celda gobernante (<see cref="OrientacionMapa"/>). Presentación
/// pura, sin fórmula de diseño: verifica que el cero sea SIEMPRE el centro de la escala (blanco),
/// que los ejes se rotulen con el borde correcto según muro/losa y que se marque exactamente la
/// celda de mayor |momento| (la misma que usa el diseño).
/// </summary>
public class OrientacionMapaTests
{
    private static CampoMomento Campo(double[,] vals, string elemento, double luzFilas, double luzCols) =>
        new(elemento, "Mx", "Cara interior", "", vals, luzFilas, luzCols);

    [Fact]
    public void RangoSimetrico_AnclaElCeroEnElCentro()
    {
        // Campo de un solo signo (cara interior, todo ≥ 0): el rango debe ser SIMÉTRICO en cero, de
        // modo que el cero sea el centro (blanco) y no un extremo saturado (azul) de la escala.
        var (min, max) = MapaDeColor.RangoSimetrico(new double[,] { { 0.0, 5.0 }, { 2.0, 11.6 } });
        Assert.Equal(-11.6, min, 6);
        Assert.Equal(11.6, max, 6);

        // Campo de un solo signo negativo (cara exterior, todo ≤ 0): también simétrico en cero.
        var (min2, max2) = MapaDeColor.RangoSimetrico(new double[,] { { -8.0, -2.0 }, { 0.0, -4.0 } });
        Assert.Equal(-8.0, min2, 6);
        Assert.Equal(8.0, max2, 6);

        // Campo de ceros: escala neutra, sin dividir por cero.
        var (min3, max3) = MapaDeColor.RangoSimetrico(new double[,] { { 0.0, 0.0 }, { 0.0, 0.0 } });
        Assert.Equal(-1.0, min3, 6);
        Assert.Equal(1.0, max3, 6);
    }

    [Fact]
    public void EtiquetasFila_MuroTopeBase_LosaBordeCentro()
    {
        var muro = Campo(new double[6, 6], "Muro longitudinal", 4.5, 3.0);
        Assert.Equal("tope 0", OrientacionMapa.EtiquetaFila(muro, 0, 6));
        Assert.Equal("base 4.5", OrientacionMapa.EtiquetaFila(muro, 5, 6));
        Assert.Equal("2.7", OrientacionMapa.EtiquetaFila(muro, 3, 6)); // fila intermedia: solo número (3/5·4.5)

        var losa = Campo(new double[6, 6], "Cubierta", 3.0, 4.0);
        Assert.Equal("borde 0", OrientacionMapa.EtiquetaFila(losa, 0, 6));
        Assert.Equal("centro 3", OrientacionMapa.EtiquetaFila(losa, 5, 6));
    }

    [Fact]
    public void EtiquetasColumna_BordeCentro()
    {
        var muro = Campo(new double[6, 6], "Muro transversal", 4.5, 3.0);
        Assert.Equal("borde 0", OrientacionMapa.EtiquetaColumna(muro, 0, 6));
        Assert.Equal("centro 3", OrientacionMapa.EtiquetaColumna(muro, 5, 6));
    }

    [Fact]
    public void CeldaGobernante_DevuelveLaDeMayorMagnitud()
    {
        var vals = new double[,] { { 1.0, 2.0, 3.0 }, { 4.0, -7.5, 6.0 }, { 7.0, 8.0, 9.0 } };
        var (f, c, v) = OrientacionMapa.CeldaGobernante(vals);
        Assert.Equal(2, f);
        Assert.Equal(2, c);
        Assert.Equal(9.0, v, 6);
    }
}
