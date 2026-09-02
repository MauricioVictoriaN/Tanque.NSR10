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

public class CargasGravitacionalesTests
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
    public void Ejercicio1_TanqueLadosIguales_SinTapa_PesosCoincidenConFormulaConfirmadaPorIL()
    {
        var caso = CasoOro.Cargar("ejercicio_1_tanque_lados_iguales.json");
        var proyecto = ProyectoDesde(caso);

        var resultado = CargasGravitacionales.Calcular(proyecto);

        var esperado = caso.PesoTanqueKN!;
        AssertTol("Pm1 (par muros tipo B)", resultado.Pm1ParMurosTipoBKN, esperado["Pm1_par_muros_tipo_B_kN"]!.GetValue<double>());
        AssertTol("Pm2 (par muros tipo L)", resultado.Pm2ParMurosTipoLKN, esperado["Pm2_par_muros_tipo_L_kN"]!.GetValue<double>());
        AssertTol("muros_x4 (2Pm1+2Pm2)", 2 * resultado.Pm1ParMurosTipoBKN + 2 * resultado.Pm2ParMurosTipoLKN, esperado["muros_x4"]!.GetValue<double>());
        AssertTol("placa_cimentacion (Pf)", resultado.PfFondoKN, esperado["placa_cimentacion"]!.GetValue<double>());
        AssertTol("total (Ptt)", resultado.PttTotalKN, esperado["total"]!.GetValue<double>());
        Assert.Equal(0.0, resultado.PtCubiertaKN); // sin tapa -> sin peso de cubierta
    }

    [Fact]
    public void Ejercicio2_TanqueLadosDispares_ConTapa_PesosCoincidenConTabla36DeLaTesis()
    {
        var caso = CasoOro.Cargar("ejercicio_2_tanque_lados_dispares_sismo.json");
        var proyecto = ProyectoDesde(caso);

        var resultado = CargasGravitacionales.Calcular(proyecto);

        var esperado = caso.PesoTanqueKN!;
        AssertTol("Pm1 (par muros dirección corta B)", resultado.Pm1ParMurosTipoBKN, esperado["Pm1_par_muros_direccion_corta_B_kN"]!.GetValue<double>());
        AssertTol("Pm2 (par muros dirección larga L)", resultado.Pm2ParMurosTipoLKN, esperado["Pm2_par_muros_direccion_larga_L_kN"]!.GetValue<double>());
        AssertTol("tapa (Pt)", resultado.PtCubiertaKN, esperado["tapa_kN"]!.GetValue<double>());
        AssertTol("placa_fondo (Pf)", resultado.PfFondoKN, esperado["placa_fondo_kN"]!.GetValue<double>());
        AssertTol("total (Ptt)", resultado.PttTotalKN, esperado["total"]!.GetValue<double>());
    }

    private static void AssertTol(string nombre, double actual, double esperado)
    {
        Assert.True(Tolerancia.SonIguales(actual, esperado), Tolerancia.Diagnostico(nombre, actual, esperado));
    }
}
