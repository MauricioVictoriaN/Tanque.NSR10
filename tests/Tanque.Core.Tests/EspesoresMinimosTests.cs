// ---------------------------------------------------------------------------
// Tanque.NSR10
// (c) Mauricio Javier Victoria Niño <hidratecsa@gmail.com> · CC BY-NC-SA 4.0
// Uso exclusivamente académico. El motor de cálculo (Tanque.Core) se distribuye
// como binario ofuscado, protegido por derechos de autor (ver LICENSE).
// ---------------------------------------------------------------------------
using Tanque.Core.Dominio;
using Tanque.Core.Modulos;
using Xunit;

namespace Tanque.Core.Tests;

/// <summary>
/// Pruebas espejo de la sección "Espesores minimos de muro NSR-10 C.23-C.14.6" de
/// tools/Tanque.Core.Verificacion/Program.cs (611/611 aserciones al momento de escribir este
/// archivo) -- mismos datos y valores esperados. Ver el docstring de Modulos/EspesoresMinimos.cs
/// para el detalle normativo completo (por qué C.23-C.14.5.3 -- método empírico -- no aplica a
/// estructuras ambientales, y por qué solo se implementa el muro, no las placas).
/// </summary>
public class EspesoresMinimosTests
{
    private static Geometria GeoAlta() => new(
        BAnchoM: 6.0, LLargoM: 8.0, HtAlturaM: 4.5, ConTapa: true,
        EmEspesorMuroM: 0.3, EfEspesorFondoM: 0.35, EtEspesorTapaM: 0.2,
        HLAlturaLiquidoM: 4.0, HmAlturaSueloSobreMuroM: 3.0, WextSobrecargaKNm2: 0.0);

    [Fact]
    public void AlturaMayorQue3m_ActivaClausulaContactoConLiquido_Minimo300mm()
    {
        var r = EspesoresMinimos.VerificarMuro(GeoAlta());
        Assert.Equal(0.300, r.EspesorMinimoAplicableM, precision: 9);
        Assert.Contains("C.23-C.14.6.2", r.ClausulaAplicada);
    }

    [Fact]
    public void AlturaMayorQue3m_EspesorExactoEnElMinimo_Cumple()
    {
        var r = EspesoresMinimos.VerificarMuro(GeoAlta()); // Em=0.30m == mínimo exacto
        Assert.True(r.Cumple);
        Assert.Equal(0.0, r.DeficitM, precision: 9);
    }

    [Fact]
    public void AlturaMayorQue3m_EspesorInsuficiente_NoCumpleYReportaDeficit()
    {
        var geo = GeoAlta() with { EmEspesorMuroM = 0.25 };
        var r = EspesoresMinimos.VerificarMuro(geo);
        Assert.False(r.Cumple);
        Assert.Equal(0.05, r.DeficitM, precision: 9);
    }

    [Fact]
    public void AlturaMenorOIgualQue3m_NoActivaClausulaContactoConLiquido_PisoAbsoluto150mm()
    {
        var geo = GeoAlta() with { HtAlturaM = 2.5, HLAlturaLiquidoM = 2.0, HmAlturaSueloSobreMuroM = 1.5, EmEspesorMuroM = 0.1 };
        var r = EspesoresMinimos.VerificarMuro(geo);
        Assert.Equal(0.150, r.EspesorMinimoAplicableM, precision: 9);
        Assert.Contains("C.23-C.14.6.1", r.ClausulaAplicada);
        Assert.False(r.Cumple);
        Assert.Equal(0.05, r.DeficitM, precision: 9);
    }

    [Fact]
    public void AlturaMenorOIgualQue3m_EspesorSuficiente_Cumple()
    {
        var geo = GeoAlta() with { HtAlturaM = 2.5, HLAlturaLiquidoM = 2.0, HmAlturaSueloSobreMuroM = 1.5, EmEspesorMuroM = 0.2 };
        var r = EspesoresMinimos.VerificarMuro(geo);
        Assert.True(r.Cumple);
        Assert.Equal(0.0, r.DeficitM, precision: 9);
    }
}
