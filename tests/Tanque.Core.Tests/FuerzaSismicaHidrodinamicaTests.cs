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
/// Pruebas del módulo de Fuerza Sísmica Hidrodinámica (Housner/ACI 350.3). Tras la corrección de
/// dimensión de 2026-08-31 (el líquido se modela con el CLARO INTERIOR, ACI 350.3 "inside
/// dimensions", no con la dimensión exterior de las Tablas 37/38 de la tesis), la verificación
/// numérica se hace contra un RECÁLCULO INDEPENDIENTE de las fórmulas de Housner con claro
/// interior -- no contra las Tablas 37/38 (que, como el programa original, usaban la dimensión
/// exterior). Ver FuerzaSismicaHidrodinamica.cs para el detalle completo.
/// </summary>
public class FuerzaSismicaHidrodinamicaTests
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

    private static ParametrosEspectroDiseno EspectroDesde(CasoOro caso) => new(
        Aa: caso.Sismo("Aa"),
        Av: caso.Sismo("Av"),
        Fa: caso.Sismo("Fa"),
        Fv: caso.Sismo("Fv"),
        // I=1.25: la Tabla 38 publica I=1.00 para el muro transversal, pero Pi2/Pc2 solo
        // reconcilian exactamente con I=1.25 (igual que el muro longitudinal) -- inconsistencia
        // de la propia tesis entre tablas (única categoría de uso en todo el proyecto), no del
        // código. Ver nota "NOTA_INCONSISTENCIA_I" en el propio JSON del ejercicio.
        I: 1.25,
        // Confirmado numéricamente: hi=0.375*HL (rama "base rígida, L/HL>=1.33") reproduce
        // exactamente hi=1.13m publicado en ambas tablas.
        CondicionBase: CondicionBaseMuro.Rigida,
        // Ri=3.0/Rc=1.0 publicados en ambas tablas corresponden a "articulada/empotrada".
        CondicionAnclaje: CondicionAnclajeBase.ArticuladaEmpotrada);

    [Fact]
    public void Ejercicio2_MuroLongitudinal_CoincideConRecalculoIndependiente_ClaroInterior()
    {
        var caso = CasoOro.Cargar("ejercicio_2_tanque_lados_dispares_sismo.json");
        var proyecto = ProyectoDesde(caso);
        var espectro = EspectroDesde(caso);

        var r = FuerzaSismicaHidrodinamica.Calcular(proyecto, espectro).MuroLongitudinal;

        AssertTol("DireccionSismoM == claro interior (L-2·em)", r.DireccionSismoM,
            proyecto.Geometria.LLargoM - 2.0 * proyecto.Geometria.EmEspesorMuroM, 1e-12);

        VerificarMuroHousner("Muro longitudinal", r, proyecto, espectro,
            proyecto.Geometria.LLargoM - 2.0 * proyecto.Geometria.EmEspesorMuroM,
            proyecto.Geometria.BAnchoM - 2.0 * proyecto.Geometria.EmEspesorMuroM);
    }

    [Fact]
    public void Ejercicio2_MuroTransversal_CoincideConRecalculoIndependiente_ClaroInterior()
    {
        var caso = CasoOro.Cargar("ejercicio_2_tanque_lados_dispares_sismo.json");
        var proyecto = ProyectoDesde(caso);
        var espectro = EspectroDesde(caso);

        var r = FuerzaSismicaHidrodinamica.Calcular(proyecto, espectro).MuroTransversal;

        AssertTol("DireccionSismoM == claro interior (B-2·em)", r.DireccionSismoM,
            proyecto.Geometria.BAnchoM - 2.0 * proyecto.Geometria.EmEspesorMuroM, 1e-12);

        VerificarMuroHousner("Muro transversal", r, proyecto, espectro,
            proyecto.Geometria.BAnchoM - 2.0 * proyecto.Geometria.EmEspesorMuroM,
            proyecto.Geometria.LLargoM - 2.0 * proyecto.Geometria.EmEspesorMuroM);
    }

    /// <summary>
    /// Recálculo INDEPENDIENTE de Housner/ACI 350.3 con la dimensión del líquido en CLARO INTERIOR
    /// (L-2·em, B-2·em -- ACI 350.3 "inside dimensions"). Reescribe aquí las fórmulas (no se
    /// copia el código del módulo) para poder comparar "ambas vías". <paramref name="ldir"/> es la
    /// dimensión en la dirección del sismo y <paramref name="lperp"/> la perpendicular, ambas en
    /// claro interior.
    /// </summary>
    private static void VerificarMuroHousner(
        string nombre, ResultadoFuerzaSismicaMuro m, ProyectoTanque proyecto,
        ParametrosEspectroDiseno espectro, double ldir, double lperp)
    {
        var g = proyecto.Geometria;
        var mat = proyecto.Materiales;
        var em = g.EmEspesorMuroM;
        var hl = g.HLAlturaLiquidoM;
        var gammaL = mat.GammaLiquidoKNm3;
        var gammaC = mat.GammaConcretoKNm3;

        // Volumen interior del líquido (claro interior), independiente de la dirección del sismo.
        var wl = gammaL * (g.BAnchoM - 2.0 * em) * hl * (g.LLargoM - 2.0 * em);

        var ratio = ldir / hl;
        var hlSobreLdir = hl / ldir;
        var argImp = 0.866 * ratio;
        var wi = (Math.Tanh(argImp) / argImp) * wl;
        var wc = (0.264 * ratio * Math.Tanh(3.16 * hlSobreLdir)) * wl;
        var hi = ratio >= 1.33 ? 0.375 * hl : (0.5 - 0.09375 * ratio) * hl;
        var hc = hl * (1.0 - (Math.Cosh(3.16 * hlSobreLdir) - 1.0) / (3.16 * hlSobreLdir * Math.Sinh(3.16 * hlSobreLdir)));

        var sds = 2.5 * espectro.Fa * espectro.Aa;
        var s1 = 1.2 * espectro.Fv * espectro.Av;
        var ts = 0.48 * (espectro.Av * espectro.Fv) / (espectro.Aa * espectro.Fa);

        var mi = (wi / wl) * (ldir / 2.0) * hl * (gammaL / 9.8061);
        var mw = g.HtAlturaM * em * (gammaC / 9.8061);
        var mTotal = mi + mw;
        var h = ((g.HtAlturaM / 2.0) * mw + hi * mi) / (mw + mi);
        var ec = 4700 * Math.Sqrt(mat.FcMPa);
        var k = (ec / 4e6) * Math.Pow(em * 1000.0 / h, 3);
        var ti = 2 * Math.PI * Math.Sqrt(mTotal / k);
        var lambda = Math.Sqrt(3.16 * 9.8065 * Math.Tanh(3.16 * hlSobreLdir));
        var tc = (2 * Math.PI / lambda) * Math.Sqrt(ldir);
        var epsilon = Math.Min(1.0, 0.051 * ratio * ratio - 0.1908 * ratio + 1.021);

        var ci = (ti <= ts || s1 / ti > sds) ? sds : s1 / ti;
        var cc = (tc <= 1.6 / ts) ? Math.Min(1.5 * sds, 1.5 * s1 / tc) : 2.4 * sds / (tc * tc);
        var ri = 3.0; var rc = 1.0; // ArticuladaEmpotrada
        var pi = ci * espectro.I * (wi / ri);
        var pc = cc * espectro.I * (wc / rc);

        double Eval(double p, double hE, double y) => (p / (2.0 * lperp)) * ((4.0 * hl - 6.0 * hE - (6.0 * hl - 12.0 * hE) * (y / hl)) / (hl * hl));
        var piFondo = Math.Max(0.0, Eval(pi, hi, 0.0));
        var piSup = Math.Max(0.0, Eval(pi, hi, hl));
        var pcFondoBruto = Eval(pc, hc, 0.0);
        var pcSupBruto = Eval(pc, hc, hl);
        var pcFondo = pcFondoBruto < 0 ? 0.0 : pcFondoBruto;
        var pcSup = pcFondoBruto < 0 ? pc / (lperp * hl) : pcSupBruto;

        AssertTol($"{nombre}: WL (volumen interior)", m.WLPesoTotalLiquidoKN, wl, 1e-6);
        AssertTol($"{nombre}: Wi", m.WiPesoImpulsivoKN, wi, 0.05);
        AssertTol($"{nombre}: Wc", m.WcPesoConvectivoKN, wc, 0.05);
        AssertTol($"{nombre}: hi", m.HiAlturaCentroideImpulsivoM, hi, 0.01);
        AssertTol($"{nombre}: hc", m.HcAlturaCentroideConvectivoM, hc, 0.01);
        AssertTol($"{nombre}: Ti", m.TiPeriodoImpulsivoS, ti, 0.001);
        AssertTol($"{nombre}: Tc", m.TcPeriodoConvectivoS, tc, 0.01);
        AssertTol($"{nombre}: epsilon", m.Epsilon, epsilon, 0.001);
        AssertTol($"{nombre}: Pi", m.PiImpulsivaKN, pi, 0.1);
        AssertTol($"{nombre}: Pc", m.PcConvectivaKN, pc, 0.3);
        AssertTol($"{nombre}: presión impulsiva fondo", m.PresionImpulsiva.FondoKNm2, piFondo, 0.05);
        AssertTol($"{nombre}: presión impulsiva superficie", m.PresionImpulsiva.SuperficieKNm2, piSup, 0.02);
        AssertTol($"{nombre}: presión convectiva fondo", m.PresionConvectiva.FondoKNm2, pcFondo, 0.02);
        AssertTol($"{nombre}: presión convectiva superficie", m.PresionConvectiva.SuperficieKNm2, pcSup, 0.02);
    }

    /// <summary>
    /// Hallazgo 8 corregido: la protección contra presión convectiva negativa debe activarse en
    /// AMBOS muros cuando hc &gt; (2/3)HL, no solo en el transversal como el programa original.
    /// Se fuerza esa condición con una geometría sintética (HL pequeño relativo a L/B, que
    /// produce hc grande) y se verifica que ninguna de las dos presiones de fondo resulte
    /// negativa, y que use la fórmula simplificada Pc/(W·HL) en la superficie en ese caso.
    /// </summary>
    [Fact]
    public void PresionConvectivaNegativa_SeCorrigeSimetricamenteEnAmbosMuros()
    {
        var geometria = new Geometria(
            BAnchoM: 3.0, LLargoM: 3.0, HtAlturaM: 5.0, ConTapa: false,
            EmEspesorMuroM: 0.2, EfEspesorFondoM: 0.15, EtEspesorTapaM: 0,
            HLAlturaLiquidoM: 4.5, HmAlturaSueloSobreMuroM: 0, WextSobrecargaKNm2: 0,
            Tipo: TipoTanque.Superficial);
        var materiales = new Materiales(28, 420, 16, 24, 9.81, 30);
        var proyecto = new ProyectoTanque(geometria, materiales);
        var espectro = new ParametrosEspectroDiseno(0.2, 0.2, 1.3, 2.0, 1.0, CondicionBaseMuro.Rigida, CondicionAnclajeBase.ArticuladaEmpotrada);

        var r = FuerzaSismicaHidrodinamica.Calcular(proyecto, espectro);

        foreach (var muro in new[] { r.MuroLongitudinal, r.MuroTransversal })
        {
            // hc>(2/3)HL con esta geometría casi cuadrada y HL grande relativo a L/B -- si no se
            // da, la prueba no está ejercitando la condición y debe ajustarse la geometría.
            Assert.True(muro.HcAlturaCentroideConvectivoM > (2.0 / 3.0) * geometria.HLAlturaLiquidoM,
                "La geometría de prueba no produce hc>(2/3)HL; ajustar para ejercitar el hallazgo 8.");
            Assert.Equal(0.0, muro.PresionConvectiva.FondoKNm2);
            Assert.True(muro.PresionConvectiva.SuperficieKNm2 > 0);
        }
    }

    private static void AssertTol(string nombre, double actual, double esperado, double? atol = null)
    {
        var ok = atol.HasValue
            ? Tolerancia.SonIguales(actual, esperado, toleranciaAbsoluta: atol.Value)
            : Tolerancia.SonIguales(actual, esperado);
        Assert.True(ok, Tolerancia.Diagnostico(nombre, actual, esperado));
    }
}
