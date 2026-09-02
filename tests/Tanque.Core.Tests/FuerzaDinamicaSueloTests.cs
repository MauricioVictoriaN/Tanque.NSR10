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
/// Pruebas del módulo de Fuerza Dinámica de Suelo (Mononobe-Okabe/Seed-Whitman), contra la
/// Tabla 39 del ejercicio 2 de la tesis. Igual que en <see cref="PresionesLateralesTests"/>, Ka
/// (y por tanto Keq/Qae, que dependen de él) se compara contra el valor de Rankine CORREGIDO
/// (hallazgo 1), no contra el publicado -- ver FuerzaDinamicaSuelo.cs para el detalle completo.
/// θ, ψ y Kae no dependen de Ka y sí se comparan directamente contra la tesis.
/// </summary>
public class FuerzaDinamicaSueloTests
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
    public void Ejercicio2_CoincideConTabla39_SalvoKaCorregido()
    {
        var caso = CasoOro.Cargar("ejercicio_2_tanque_lados_dispares_sismo.json");
        var proyecto = ProyectoDesde(caso);
        var parametros = new ParametrosSueloDinamico(
            KhCoeficienteSismicoHorizontal: caso.Sismo("kh_coef_sismico_horizontal"),
            KvCoeficienteSismicoVertical: caso.Sismo("kv_coef_sismico_vertical"),
            DeltaGradosFriccionSueloMuro: 0,
            IGradosInclinacionRelleno: 0,
            BetaGradosInclinacionMuro: 90);

        var r = FuerzaDinamicaSuelo.Calcular(proyecto, parametros);
        var esperado = caso.Resultado("presion_dinamica_suelo_mononobe_okabe_tabla39")!;

        AssertTol("theta", r.ThetaGrados, esperado["theta_grados"]!.GetValue<double>(), atol: 0.01);
        AssertTol("psi", r.Psi, esperado["psi"]!.GetValue<double>(), atol: 0.01);
        AssertTol("Kae", r.Kae, esperado["Kae"]!.GetValue<double>(), atol: 0.001);

        // Ka/Keq/Qae: el JSON publica 0.2818/0.1376/7.43, calculados con la fórmula ORIGINAL
        // (hallazgo 1). Valores correctos con Rankine (φ=28°): Ka≈0.36095, Keq=Kae-Ka≈0.05835,
        // Qae=γsuelo×H×Keq≈3.15 kN/m².
        AssertTol("Ka (Rankine corregido)", r.Ka, 0.36095, atol: 0.0005);
        AssertTol("Keq (con Ka corregido)", r.Keq, 0.05835, atol: 0.001);
        AssertTol("Qae (con Ka corregido)", r.QaeKNm2, 3.15, atol: 0.05);
    }

    private static void AssertTol(string nombre, double actual, double esperado, double? atol = null)
    {
        var ok = atol.HasValue
            ? Tolerancia.SonIguales(actual, esperado, toleranciaAbsoluta: atol.Value)
            : Tolerancia.SonIguales(actual, esperado);
        Assert.True(ok, Tolerancia.Diagnostico(nombre, actual, esperado));
    }
}
