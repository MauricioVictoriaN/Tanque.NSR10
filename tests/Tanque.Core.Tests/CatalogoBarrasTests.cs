// ---------------------------------------------------------------------------
// Tanque.NSR10
// (c) Mauricio Javier Victoria Niño <hidratecsa@gmail.com> · CC BY-NC-SA 4.0
// Uso exclusivamente académico. El motor de cálculo (Tanque.Core) se distribuye
// como binario ofuscado, protegido por derechos de autor (ver LICENSE).
// ---------------------------------------------------------------------------
using Tanque.Core.Modulos;
using Xunit;

namespace Tanque.Core.Tests;

/// <summary>
/// Pruebas de <see cref="CatalogoBarras.CuantiaMinimaRetracionTemperatura"/> -- la cuantía mínima
/// de retracción de fraguado y variación de temperatura para estructuras ambientales (losas y
/// muros de tanque), NSR-10 C.23-C.7.12.2.1 (Tabla C.23-C.7.12.2.1, folio C-440). La tabla
/// tabula DOS grados de acero (fy=240 y fy=420 MPa) por rango de distancia entre juntas; para fy
/// intermedio se interpola linealmente y fuera de [240,420] se sujeta al grado más cercano.
/// </summary>
public class CatalogoBarrasTests
{
    private static void AssertTol(string nombre, double actual, double esperado, double atol = 1e-9)
        => Assert.True(Tolerancia.SonIguales(actual, esperado, toleranciaAbsoluta: atol, toleranciaRelativa: 0.0),
            Tolerancia.Diagnostico(nombre, actual, esperado, atol, 0.0));

    [Fact]
    public void Fy420_TablaCompleta_PorRangoDeDistancia()
    {
        // Columna fy=420 MPa de la Tabla C.23-C.7.12.2.1.
        AssertTol("<6 m", CatalogoBarras.CuantiaMinimaRetracionTemperatura(0.0, 420), 0.0030);
        AssertTol("<6 m (5.99)", CatalogoBarras.CuantiaMinimaRetracionTemperatura(5.99, 420), 0.0030);
        AssertTol("6 m", CatalogoBarras.CuantiaMinimaRetracionTemperatura(6.0, 420), 0.0030);
        AssertTol("6-9 m (8.99)", CatalogoBarras.CuantiaMinimaRetracionTemperatura(8.99, 420), 0.0030);
        AssertTol("9 m", CatalogoBarras.CuantiaMinimaRetracionTemperatura(9.0, 420), 0.0040);
        AssertTol("9-12 m (11.99)", CatalogoBarras.CuantiaMinimaRetracionTemperatura(11.99, 420), 0.0040);
        AssertTol("12 m", CatalogoBarras.CuantiaMinimaRetracionTemperatura(12.0, 420), 0.0050);
        AssertTol(">=12 m (30)", CatalogoBarras.CuantiaMinimaRetracionTemperatura(30.0, 420), 0.0050);
    }

    [Fact]
    public void Fy240_TablaCompleta_PorRangoDeDistancia()
    {
        // Columna fy=240 MPa de la Tabla C.23-C.7.12.2.1 (más exigente para distancias ≥ 6 m).
        AssertTol("<6 m", CatalogoBarras.CuantiaMinimaRetracionTemperatura(3.0, 240), 0.0030);
        AssertTol("6-9 m", CatalogoBarras.CuantiaMinimaRetracionTemperatura(7.0, 240), 0.0040);
        AssertTol("9-12 m", CatalogoBarras.CuantiaMinimaRetracionTemperatura(10.0, 240), 0.0050);
        AssertTol(">=12 m", CatalogoBarras.CuantiaMinimaRetracionTemperatura(20.0, 240), 0.0060);
    }

    [Fact]
    public void FyIntermedio_InterpolaLinealmenteEntreLasDosColumnas()
    {
        // fy=330 MPa es el punto medio entre 240 y 420: cada cuantía es el promedio de las dos columnas.
        AssertTol("6-9 m en 330", CatalogoBarras.CuantiaMinimaRetracionTemperatura(7.0, 330), 0.0035);
        AssertTol("9-12 m en 330", CatalogoBarras.CuantiaMinimaRetracionTemperatura(10.0, 330), 0.0045);
        AssertTol(">=12 m en 330", CatalogoBarras.CuantiaMinimaRetracionTemperatura(20.0, 330), 0.0055);
        // En <6 m ambas columnas son 0.0030, así que no hay variación con fy.
        AssertTol("<6 m en 330", CatalogoBarras.CuantiaMinimaRetracionTemperatura(3.0, 330), 0.0030);
    }

    [Fact]
    public void FyFueraDeRango_SeSujetaAlGradoMasCercano()
    {
        // fy < 240 → columna de 240 (conservador, más acero).
        AssertTol("fy=200 en 6-9 m → 0.0040", CatalogoBarras.CuantiaMinimaRetracionTemperatura(7.0, 200), 0.0040);
        // fy > 420 → columna de 420 (no se extrapola hacia abajo).
        AssertTol("fy=500 en 9-12 m → 0.0040", CatalogoBarras.CuantiaMinimaRetracionTemperatura(10.0, 500), 0.0040);
    }

    [Fact]
    public void EntradasInvalidas_Lanzan()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CatalogoBarras.CuantiaMinimaRetracionTemperatura(-0.1, 420));
        Assert.Throws<ArgumentOutOfRangeException>(() => CatalogoBarras.CuantiaMinimaRetracionTemperatura(6.0, 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => CatalogoBarras.CuantiaMinimaRetracionTemperatura(6.0, -420));
    }

    [Fact]
    public void DiametrosComerciales_ExcluyenNo3_YArrancanEnNo4()
    {
        // NSR-10 C.23-C.7.12.2.2: tamaño mínimo de barra No.4 (12.7 mm) para retracción/temperatura;
        // la No.3 (9.5 mm) queda excluida del catálogo de losas/muros.
        Assert.Equal(12.7, CatalogoBarras.DiametrosComercialesMm[0]);
        Assert.DoesNotContain(9.5, CatalogoBarras.DiametrosComercialesMm);
        Assert.Equal(CatalogoBarras.DiametroMinimoBarraMuroLosaMm, CatalogoBarras.DiametrosComercialesMm[0]);
    }

    [Fact]
    public void EsDiametroValido_RechazaNo3_YNoComerciales()
    {
        Assert.True(CatalogoBarras.EsDiametroValido(12.7)); // No.4
        Assert.True(CatalogoBarras.EsDiametroValido(15.9)); // No.5
        Assert.True(CatalogoBarras.EsDiametroValido(31.8)); // No.10
        Assert.False(CatalogoBarras.EsDiametroValido(9.5));  // No.3 excluida
        Assert.False(CatalogoBarras.EsDiametroValido(16.0)); // no comercial (el comercial es 15.9)
        Assert.False(CatalogoBarras.EsDiametroValido(25.0)); // no comercial (el comercial es 25.4)
    }

    [Fact]
    public void ValidarDiametroBarra_LanzaParaFueraDeCatalogo()
    {
        CatalogoBarras.ValidarDiametroBarra(12.7); // no lanza
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => CatalogoBarras.ValidarDiametroBarra(9.5));
        Assert.Contains("12.7", ex.Message); // menciona el mínimo normativo, sin remitir al código fuente
        Assert.DoesNotContain(".cs", ex.Message);
    }

    [Fact]
    public void DiametroSiguienteMayor_Y_DescripcionBarra()
    {
        Assert.Equal(15.9, CatalogoBarras.DiametroSiguienteMayor(12.7)!.Value);
        Assert.Null(CatalogoBarras.DiametroSiguienteMayor(31.8));
        Assert.Equal("Nº4 (12.7 mm)", CatalogoBarras.DescripcionBarra(12.7));
        Assert.Equal("Nº10 (31.8 mm)", CatalogoBarras.DescripcionBarra(31.8));
    }

    [Fact]
    public void GenerarSeparacionesParaDiametro_Y_SeparacionParaAs()
    {
        // No.4 (12.7 mm): área ≈126.7 mm². Para As=500 mm²/m → s ≤126.7/500=0.253 m → la mayor
        // separación comercial ≤0.253 es 0.250.
        Assert.Equal(0.250, CatalogoBarras.SeparacionParaAs(12.7, 500.0)!.Value, 3);
        // Para As=2000 mm²/m → s≤0.063 m < 0.075 (mínima) → null (diámetro insuficiente).
        Assert.Null(CatalogoBarras.SeparacionParaAs(12.7, 2000.0));

        // GenerarSeparacionesParaDiametro: de MAYOR a MENOR separación, dentro de [mín, máx].
        var seps = CatalogoBarras.GenerarSeparacionesParaDiametro(12.7).Select(x => x.SeparacionM).ToList();
        Assert.Equal(0.300, seps[0]);
        Assert.Equal(0.075, seps[^1]);
        Assert.True(seps.Zip(seps.Skip(1), (a, b) => a > b).All(x => x), "las separaciones deben venir descendentes");
    }

    [Fact]
    public void AjustarCuantiaMinimaRetracionTemperatura_Aplica50PorCiento_Y_Capa300mm()
    {
        // Notas opcionales C.23-C.7.12.2.1 (directiva 2026-08-30):
        // (a) 50 % en cara inferior sobre suelo; (b) espesor ≥ 600 mm → capa de 300 mm por superficie.
        Assert.Equal(0.0030, CatalogoBarras.AjustarCuantiaMinimaRetracionTemperatura(0.0030, 0.35, false), 6);
        Assert.Equal(0.0015, CatalogoBarras.AjustarCuantiaMinimaRetracionTemperatura(0.0030, 0.35, true), 6);
        Assert.Equal(0.0015, CatalogoBarras.AjustarCuantiaMinimaRetracionTemperatura(0.0030, 0.60, false), 6);
        Assert.Equal(0.00075, CatalogoBarras.AjustarCuantiaMinimaRetracionTemperatura(0.0030, 0.60, true), 6);
    }
}
