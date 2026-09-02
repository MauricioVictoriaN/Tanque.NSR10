// ---------------------------------------------------------------------------
// Tanque.NSR10
// (c) Mauricio Javier Victoria Niño <hidratecsa@gmail.com> · CC BY-NC-SA 4.0
// Uso exclusivamente académico. El motor de cálculo (Tanque.Core) se distribuye
// como binario ofuscado, protegido por derechos de autor (ver LICENSE).
// ---------------------------------------------------------------------------
using System;
using Tanque.Core.Dominio;
using Tanque.Core.Modulos;
using Xunit;

namespace Tanque.Core.Tests;

/// <summary>
/// Pruebas de las sugerencias geométricas (Backlog v3, hallazgo de UX confirmado por el usuario
/// 2026-08-26): cuando r cae fuera de [0.5, 4], las envolturas específicas lanzan
/// <see cref="ArgumentOutOfRangeException"/> con un mensaje ACCIONABLE — qué dimensión ajustar y a
/// qué rango — en vez de solo "fuera de rango". La clase <see cref="SugerenciasGeometricas"/> es
/// internal, así que se prueba a través de las envolturas públicas: placa de cubierta (r=B/L),
/// placa de fondo (r=L/B) y muro longitudinal (r=span/altura).
/// </summary>
public class SugerenciasGeometricasTests
{
    private static ProyectoTanque Proyecto(double b, double l, double ht, double hl, double hm,
        double em = 0.3, bool conTapa = true) => new(
        new Geometria(
            BAnchoM: b, LLargoM: l, HtAlturaM: ht, ConTapa: conTapa,
            EmEspesorMuroM: em, EfEspesorFondoM: 0.35, EtEspesorTapaM: conTapa ? 0.2 : 0.0,
            HLAlturaLiquidoM: hl, HmAlturaSueloSobreMuroM: hm, WextSobrecargaKNm2: 0.0,
            Tipo: TipoTanque.EnterradoSinNivelFreatico, AlturaNivelFreaticoM: null),
        new Materiales(
            FcMPa: 21, FyMPa: 420, GammaConcretoKNm3: 24, GammaLiquidoKNm3: 9.81,
            GammaSueloKNm3: 16, PhiGradosAnguloFriccionSuelo: 30));

    private static ResultadoCargasGravitacionales Cargas(ProyectoTanque p, double cv, double cg) =>
        CargasGravitacionales.Calcular(p, cv, cg);

    [Fact]
    public void PlacaCubierta_RFueraDeRango_MensajeAccionable()
    {
        // B=2, L=5 -> r=B/L=0.4 <0.5 (el caso real reportado por el usuario, 2026-08-26)
        var p = Proyecto(b: 2.0, l: 5.0, ht: 4.5, hl: 4.0, hm: 3.0);
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            PlacasRectangulares.CalcularPlacaCubierta(p, Cargas(p, 1.0, 0.5), 1.0, 0.5));

        Assert.Contains("r=B/L", ex.Message);
        Assert.Contains("Ajuste la geometría", ex.Message);
        Assert.Contains("B (ancho del tanque) debe estar entre", ex.Message);
    }

    [Fact]
    public void PlacaFondo_RFueraDeRango_MensajeAccionable()
    {
        // L=2, B=5 -> r=L/B=0.4 <0.5 (la placa de fondo usa la convención invertida a=L, b=B)
        var p = Proyecto(b: 5.0, l: 2.0, ht: 4.5, hl: 4.0, hm: 3.0);
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            PlacasRectangulares.CalcularPlacaFondo(p, Cargas(p, 0.0, 0.0), 0.0));

        Assert.Contains("r=L/B", ex.Message);
        Assert.Contains("Ajuste la geometría", ex.Message);
        Assert.Contains("L (largo del tanque) debe estar entre", ex.Message);
    }

    [Fact]
    public void MuroLongitudinal_RFueraDeRango_MensajeAccionable()
    {
        // L=1, HL=3 -> r=(L-em)/HL=0.233 <0.5
        var p = Proyecto(b: 4.0, l: 1.0, ht: 3.5, hl: 3.0, hm: 3.0, conTapa: false);
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            MurosRectangulares.CalcularMuroLongitudinal(p, PresionesLaterales.Calcular(p)));

        Assert.Contains("Ajuste la geometría", ex.Message);
        Assert.Contains("r=", ex.Message);
    }

    [Fact]
    public void GeometriaEnRango_NoLanza()
    {
        // B=4, L=5 -> r=B/L=0.8 en rango; debe calcular, no lanzar
        var p = Proyecto(b: 4.0, l: 5.0, ht: 4.5, hl: 4.0, hm: 3.0);
        var r = PlacasRectangulares.CalcularPlacaCubierta(p, Cargas(p, 1.0, 0.5), 1.0, 0.5);
        Assert.True(r.MxPosGobernanteKNmM > 0);
    }
}
