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
/// Pruebas espejo de la sección "TipoTanque y Flotabilidad" de
/// tools/Tanque.Core.Verificacion/Program.cs (596/596 aserciones al momento de escribir este
/// archivo) -- mismos datos y valores esperados, calculados a mano y ya verificados ahí. Cubre
/// tanto las tres reglas nuevas de <see cref="Geometria.Validar"/> para <see cref="TipoTanque"/>
/// como el módulo <see cref="Flotabilidad"/> (ACI 350.4R-04 §3.1.2). Ver el docstring de
/// Modulos/Flotabilidad.cs y Dominio/TipoTanque.cs para el detalle normativo completo.
/// </summary>
public class FlotabilidadTests
{
    private static Geometria GeoBase() => new(
        BAnchoM: 6.0, LLargoM: 8.0, HtAlturaM: 4.5, ConTapa: true,
        EmEspesorMuroM: 0.3, EfEspesorFondoM: 0.35, EtEspesorTapaM: 0.2,
        HLAlturaLiquidoM: 4.0, HmAlturaSueloSobreMuroM: 3.0, WextSobrecargaKNm2: 0.0);

    private static Materiales MatBase() => new(
        FcMPa: 21, FyMPa: 420, GammaConcretoKNm3: 24, GammaLiquidoKNm3: 9.81,
        GammaSueloKNm3: 16, PhiGradosAnguloFriccionSuelo: 30);

    [Fact]
    public void Superficial_ConHmMayorQueCero_Lanza()
    {
        var geo = GeoBase() with { Tipo = TipoTanque.Superficial };
        Assert.Throws<ArgumentException>(() => geo.Validar());
    }

    [Fact]
    public void Superficial_ConHmCero_NoLanza()
    {
        var geo = GeoBase() with { Tipo = TipoTanque.Superficial, HmAlturaSueloSobreMuroM = 0 };
        geo.Validar(); // no debe lanzar
    }

    [Fact]
    public void EnterradoConNivelFreatico_SinAlturaNivelFreatico_Lanza()
    {
        var geo = GeoBase() with { Tipo = TipoTanque.EnterradoConNivelFreatico };
        Assert.Throws<ArgumentException>(() => geo.Validar());
    }

    [Fact]
    public void EnterradoConNivelFreatico_AlturaMayorQueHm_Lanza()
    {
        var geo = GeoBase() with { Tipo = TipoTanque.EnterradoConNivelFreatico, AlturaNivelFreaticoM = 3.5 };
        Assert.Throws<ArgumentOutOfRangeException>(() => geo.Validar());
    }

    [Fact]
    public void AlturaNivelFreatico_FueraDeEnterradoConNivelFreatico_Lanza()
    {
        var geo = GeoBase() with { Tipo = TipoTanque.EnterradoSinNivelFreatico, AlturaNivelFreaticoM = 1.0 };
        Assert.Throws<ArgumentException>(() => geo.Validar());
    }

    [Fact]
    public void Superficial_ConHmCero_NoNecesitaGatingAdicional_Ps2ExteriorEsCero()
    {
        // Descubrimiento de diseño documentado en Flotabilidad.cs / Geometria.cs: Hm=0 hace que
        // Ps2MaximaKNm2 (presión exterior de suelo) sea idénticamente 0 -- la física de la carga
        // ya resuelve el "interruptor" que TipoTanque.Superficial necesitaría, sin gating extra
        // en MurosRectangulares/DisenoMuros.
        var geo = GeoBase() with { Tipo = TipoTanque.Superficial, HmAlturaSueloSobreMuroM = 0 };
        var proyecto = new ProyectoTanque(geo, MatBase());
        var presiones = PresionesLaterales.Calcular(proyecto);
        Assert.Equal(0.0, presiones.Ps2MaximaKNm2, precision: 9);
    }

    [Fact]
    public void Verificar_FueraDeEnterradoConNivelFreatico_Lanza()
    {
        var geo = GeoBase(); // Tipo por defecto: EnterradoSinNivelFreatico
        var proyecto = new ProyectoTanque(geo, MatBase());
        var cargas = CargasGravitacionales.Calcular(proyecto, cargaVivaCubiertaKNm2: 0.0, cargaAdicionalCubiertaKNm2: 0.0);
        Assert.Throws<InvalidOperationException>(() => Flotabilidad.Verificar(proyecto, cargas));
    }

    [Fact]
    public void Verificar_NivelFreaticoSomero_Cumple()
    {
        // h=1.0m: PesoPropio=1395.792 kN, Subpresion=470.88 kN, FS=2.9642..., calculado a mano y
        // confirmado en tools/Tanque.Core.Verificacion.
        var geo = GeoBase() with { Tipo = TipoTanque.EnterradoConNivelFreatico, AlturaNivelFreaticoM = 1.0 };
        var proyecto = new ProyectoTanque(geo, MatBase());
        var cargas = CargasGravitacionales.Calcular(proyecto, cargaVivaCubiertaKNm2: 0.0, cargaAdicionalCubiertaKNm2: 0.0);

        var r = Flotabilidad.Verificar(proyecto, cargas);

        AssertTol("PesoPropioKN", r.PesoPropioKN, 1395.792);
        AssertTol("PesoPropioKN == PttTotalKN", r.PesoPropioKN, cargas.PttTotalKN, atol: 1e-9);
        AssertTol("SubpresionKN", r.SubpresionKN, 470.88);
        AssertTol("FS", r.FS, 2.9642, atol: 0.001);
        Assert.True(r.Cumple);
        Assert.Equal(0.0, r.DeficitPesoKN, precision: 6);
    }

    [Fact]
    public void Verificar_NivelFreaticoAlMaximoPermitido_NoCumple()
    {
        // h=Hm=3.0m (el máximo permitido por Validar): Subpresion=1412.64 kN, FS<1.25,
        // DeficitPesoKN=370.008 kN -- calculado a mano y confirmado en
        // tools/Tanque.Core.Verificacion.
        var geo = GeoBase();
        var geoFalla = geo with { Tipo = TipoTanque.EnterradoConNivelFreatico, AlturaNivelFreaticoM = geo.HmAlturaSueloSobreMuroM };
        var proyecto = new ProyectoTanque(geoFalla, MatBase());
        var cargas = CargasGravitacionales.Calcular(proyecto, cargaVivaCubiertaKNm2: 0.0, cargaAdicionalCubiertaKNm2: 0.0);

        var r = Flotabilidad.Verificar(proyecto, cargas);

        AssertTol("SubpresionKN", r.SubpresionKN, 1412.64);
        Assert.False(r.Cumple);
        Assert.True(r.FS < Flotabilidad.FactorSeguridadMinimo);
        AssertTol("DeficitPesoKN", r.DeficitPesoKN, 370.008, atol: 0.01);

        // Sumar el déficit al peso propio debe alcanzar exactamente el FS mínimo exigido.
        var fsConDeficit = (r.PesoPropioKN + r.DeficitPesoKN) / r.SubpresionKN;
        Assert.True(fsConDeficit >= Flotabilidad.FactorSeguridadMinimo - 1e-9);
    }

    private static void AssertTol(string nombre, double actual, double esperado, double? atol = null)
    {
        var ok = atol.HasValue
            ? Tolerancia.SonIguales(actual, esperado, toleranciaAbsoluta: atol.Value)
            : Tolerancia.SonIguales(actual, esperado);
        Assert.True(ok, Tolerancia.Diagnostico(nombre, actual, esperado));
    }
}
