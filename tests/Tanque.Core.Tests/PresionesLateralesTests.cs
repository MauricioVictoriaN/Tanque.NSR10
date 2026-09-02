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
/// Pruebas del módulo de presiones laterales. A diferencia de <see cref="CargasGravitacionalesTests"/>
/// (donde el módulo no tiene hallazgos y los valores publicados en casos_prueba/*.json son
/// oráculo directo), este módulo corrige dos hallazgos confirmados del programa original
/// (Ka: hallazgo 1; distribución Ps_i: hallazgo 9 -- ver PresionesLaterales.cs). Los campos
/// "Ka" y "Ps2_suelo_kNm2" de resultados_esperados.presiones_diseno en los JSON de
/// casos_prueba/ reflejan la fórmula ORIGINAL (con el error de Rankine), así que estas
/// pruebas NO los usan como oráculo para Ka/Ps2 -- se comparan contra el valor corregido,
/// calculado independientemente con la fórmula de Rankine correcta y documentado aquí mismo
/// para que la discrepancia con el JSON sea explícita y trazable. Ph sí se compara contra el
/// valor publicado, porque Ph no depende de Ka y no está afectado por ningún hallazgo.
/// </summary>
public class PresionesLateralesTests
{
    private static ProyectoTanque ProyectoDesde(CasoOro caso)
    {
        var geometria = new Geometria(
            BAnchoM: caso.Geo("B_ancho_m"),
            LLargoM: caso.Geo("L_largo_m"),
            HtAlturaM: caso.Geo("Ht_altura_m"),
            ConTapa: caso.GeoBool("con_tapa"),
            EmEspesorMuroM: caso.Geo("em_espesor_muro_m"),
            EfEspesorFondoM: caso.Geo("ef_espesor_fondo_m"),
            EtEspesorTapaM: caso.GeoBool("con_tapa") ? caso.Geo("et_espesor_tapa_m") : 0.0,
            HLAlturaLiquidoM: caso.Geo("HL_altura_liquido_m"),
            HmAlturaSueloSobreMuroM: caso.Geo("Hm_altura_suelo_sobre_muro_m"),
            WextSobrecargaKNm2: caso.Geo("Wext_sobrecarga_kNm2"));

        var materiales = new Materiales(
            FcMPa: caso.Mat("fc_MPa"),
            FyMPa: caso.Mat("fy_MPa"),
            GammaSueloKNm3: caso.Mat("gamma_suelo_kNm3"),
            GammaConcretoKNm3: caso.Mat("gamma_concreto_kNm3"),
            GammaLiquidoKNm3: caso.Mat("gamma_liquido_kNm3"),
            PhiGradosAnguloFriccionSuelo: caso.Suelo("phi_grados"));

        return new ProyectoTanque(geometria, materiales);
    }

    [Fact]
    public void Ejercicio1_Phi32Grados_KaYPs2CoincidenConRankineCorregido_PhCoincideConPublicado()
    {
        var caso = CasoOro.Cargar("ejercicio_1_tanque_lados_iguales.json");
        var proyecto = ProyectoDesde(caso);

        var r = PresionesLaterales.Calcular(proyecto);

        var esperado = caso.PresionesDiseno!;
        // Ph: no depende de Ka, no tiene hallazgo abierto -> se compara contra el valor publicado.
        AssertTol("Ph (hidrostática máxima)", r.PhMaximaKNm2, esperado["Ph_hidrostatica_kNm2"]!.GetValue<double>());

        // Ka/Ps2: el JSON publica 0.2544/12.21, que reflejan la fórmula ORIGINAL (1+cos φ en el
        // denominador, hallazgo 1). El valor correcto de Rankine para φ=32° es Ka=(1-sen32°)/(1+sen32°)
        // ≈ 0.30706, calculado independientemente en esta sesión (no se recalcula aquí a propósito,
        // para que un cambio accidental en la fórmula de producción sea detectado por esta prueba).
        AssertTol("Ka (Rankine corregido)", r.Ka, 0.30706, atol: 0.0005);
        AssertTol("Ps2 (con Ka corregido)", r.Ps2MaximaKNm2, 14.74, atol: 0.02);

        Assert.Equal(11, r.Ph.Count);
        Assert.Equal(11, r.Ps.Count);
        Assert.Equal(0.0, r.Ph[0]);
        Assert.Equal(0.0, r.Ps[0]);
        AssertTol("Ph[10] == PhMaximaKNm2", r.Ph[10], r.PhMaximaKNm2);
        AssertTol("Ps[10] == Ps2MaximaKNm2", r.Ps[10], r.Ps2MaximaKNm2);
    }

    [Fact]
    public void Ejercicio2_Phi28Grados_KaYPs2CoincidenConRankineCorregido_PhUsaHL3mConsistente()
    {
        var caso = CasoOro.Cargar("ejercicio_2_tanque_lados_dispares_sismo.json");
        var proyecto = ProyectoDesde(caso);

        var r = PresionesLaterales.Calcular(proyecto);

        var esperado = caso.PresionesDiseno!;
        // El JSON publica Ph_hidrostatica_kNm2=44.15, pero ese valor usa HL=4.50m -- inconsistente
        // con el HL=3.00m declarado en las entradas de este mismo ejercicio (ver
        // "Ph_ADVERTENCIA" en el propio JSON). Se usa como oráculo el campo
        // "Ph_si_HL_3_00m_consistente_con_resto_del_ejercicio" en su lugar, NO el Ph publicado.
        AssertTol("Ph (con HL=3.00m, consistente)", r.PhMaximaKNm2,
            esperado["Ph_si_HL_3_00m_consistente_con_resto_del_ejercicio"]!.GetValue<double>());

        // Igual que en ejercicio_1: el JSON publica Ka/Ps2 con la fórmula original (hallazgo 1).
        // Rankine correcto para φ=28°: Ka=(1-sen28°)/(1+sen28°) ≈ 0.36095.
        AssertTol("Ka (Rankine corregido)", r.Ka, 0.36095, atol: 0.0005);
        AssertTol("Ps2 (con Ka corregido)", r.Ps2MaximaKNm2, 19.49, atol: 0.02);
    }

    private static void AssertTol(string nombre, double actual, double esperado, double? atol = null)
    {
        var ok = atol.HasValue
            ? Tolerancia.SonIguales(actual, esperado, toleranciaAbsoluta: atol.Value)
            : Tolerancia.SonIguales(actual, esperado);
        Assert.True(ok, Tolerancia.Diagnostico(nombre, actual, esperado));
    }
}
