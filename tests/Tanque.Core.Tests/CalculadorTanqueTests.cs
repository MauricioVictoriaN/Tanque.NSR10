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
/// Pruebas espejo del facade <see cref="CalculadorTanque"/> (Backlog v3, Fase A): ejecuta la
/// orquestación completa de los ocho módulos de cálculo y tres de integración en el orden y con la
/// lógica condicional que antes vivía en la UI. Dos escenarios:
///
/// 1. Golden del "Example 1" del manual PCA (tanque 30×20 ft eje a eje, agua 10 ft) — SIN sismo,
///    exactamente como lo corrió el usuario (`Ejemplo_PCA.json` → `Ejemplo_PCA.txt`/`.html`): las
///    anclas están verificadas celda a celda contra el manual (Cap. 3, Caso 3 para muros; Caso 10
///    para placas) y son independientes de la barra de detallado (Mu/Ms/coeficientes).
/// 2. Con sismo (B.2.4-5/B.2.4-7): SismoHidro/SismoSuelo no-null y diseño con envolvente sísmica.
/// </summary>
public class CalculadorTanqueTests
{
    private static void AssertTol(string nombre, double actual, double esperado) =>
        Assert.True(Tolerancia.SonIguales(actual, esperado), Tolerancia.Diagnostico(nombre, actual, esperado));

    private static ProyectoTanque ProyectoEjemploPCA() => new(
        new Geometria(
            BAnchoM: 6.553, LLargoM: 9.601, HtAlturaM: 3.658, ConTapa: true,
            EmEspesorMuroM: 0.457, EfEspesorFondoM: 0.61, EtEspesorTapaM: 0.305,
            HLAlturaLiquidoM: 3.048, HmAlturaSueloSobreMuroM: 3.048, WextSobrecargaKNm2: 0.0,
            Tipo: TipoTanque.EnterradoSinNivelFreatico, AlturaNivelFreaticoM: null),
        new Materiales(
            FcMPa: 27.58, FyMPa: 413.7, GammaSueloKNm3: 15.7, GammaConcretoKNm3: 22.8,
            GammaLiquidoKNm3: 11, PhiGradosAnguloFriccionSuelo: 32.6));

    private static ParametrosCalculoTanque ParametrosEjemploPCA() => new(
        CvCubiertaKNm2: 4.79, CgCubiertaKNm2: 0.0, CvFondoKNm2: 0.0,
        DiametrosBarra: new DiametrosBarraCalculo(12.7, 12.7, 12.7, 12.7),
        MetodoInterpolacion: MetodoInterpolacion.Interpolar, IncluirDiagramas: true, Sismo: null);

    [Fact]
    public void Calcular_EjemploManualPCA_SinSismo_AnclasVerificadasContraElManual()
    {
        var r = CalculadorTanque.Calcular(ProyectoEjemploPCA(), ParametrosEjemploPCA());

        // Sismo opcional: sin parámetros -> los módulos sísmicos no aplican (null, con razón implícita en la presentación)
        Assert.Null(r.SismoHidro);
        Assert.Null(r.SismoSuelo);

        // Presión hidrostática máxima: gamma·HL = 11 × 3.048 = 33.528 kN/m²
        AssertTol("Ph máxima (líquido) = gamma·HL", r.Presiones.PhMaximaKNm2, 33.528);

        // Placa de cubierta (Caso 10, r=B/L=0.667 interpolado): los gobernantes del análisis de
        // placa son FACTORIZADOS (la placa embebe la carga de diseño U en el análisis, a diferencia
        // del muro cuyo análisis es de servicio) -- verificado contra el manual: Mu=Mx+ 30.34,
        // Mx- 29.002 (el servicio Ms=22.257/21.276 vive en el diseño)
        AssertTol("Cubierta Mx+ (Mu)", r.PlacaCubierta!.MxPosGobernanteKNmM, 30.340);
        AssertTol("Cubierta Mx- (Mu)", r.PlacaCubierta.MxNegGobernanteKNmM, 29.002);

        // Placa de fondo (Caso 10, r=1.5 directo, factorizada): Mx+ 140.847, Mx- 88.481
        AssertTol("Fondo Mx+ (Mu)", r.PlacaFondo.MxPosGobernanteKNmM, 140.847);
        AssertTol("Fondo Mx- (Mu)", r.PlacaFondo.MxNegGobernanteKNmM, 88.481);

        // Muro longitudinal estático (Cap. 3, Caso 3; b/a=3.0, c/a=2.0 tabulado): el análisis de
        // muro usa q = 1.4×Ph (B.2.4-1), así que los gobernantes son FACTORIZADOS (56.254 = 40.182×1.4;
        // el servicio Ms=40.182 vive en el diseño, ver abajo)
        AssertTol("Longitudinal Mx- gobernante (coef -129 × 1.4·q·a²/1000)", r.MuroLongitudinalEstatico.Interior.MxNegGobernanteKNmM, 56.254);
        AssertTol("Longitudinal My- gobernante (Marcus, coef -83 × 1.4)", r.MuroLongitudinalEstatico.Interior.MyNegGobernanteKNmM, 36.195);

        // Diseño del muro longitudinal: Mu vertical negativo = 1.4 x Ms (NSR-10 B.2.4-1) -> 56.254 kN·m/m
        var verticalNeg = r.DisenoMuroLongitudinal.VerticalNegativo;
        AssertTol("Mu vertical - (longitudinal, interior)", verticalNeg.MuKNm, 56.254);
        Assert.Contains("B.2.4-1", verticalNeg.ComboGobernante);
        Assert.NotNull(verticalNeg.MsKNm);
        AssertTol("Ms vertical - (servicio)", verticalNeg.MsKNm!.Value, 40.182);

        // Muro transversal: Mz base-centro coef -... -> Mu = 35.759 kN·m/m
        AssertTol("Mu vertical - (transversal, interior)", r.DisenoMuroTransversal.VerticalNegativo.MuKNm, 35.759);

        // Cortante (Cs del Cap. 2, r propio): 0.50/0.37/0.24 x q x a, con q = 1.4 x 33.528
        AssertTol("V fondo (longitudinal)", r.DisenoMuroLongitudinal.CortanteFondo.VuKN, 71.535);
        AssertTol("V lateral máximo (longitudinal)", r.DisenoMuroLongitudinal.CortanteLateralMaximo.VuKN, 52.936);
        AssertTol("V lateral medio (longitudinal)", r.DisenoMuroLongitudinal.CortanteLateralMedio.VuKN, 34.337);
    }

    [Fact]
    public void Calcular_ConSismo_IncluyeSismoHidroYSuelo_YDisenoConEnvolvente()
    {
        var proyecto = new ProyectoTanque(
            new Geometria(
                BAnchoM: 4.5, LLargoM: 6.0, HtAlturaM: 3.5, ConTapa: false,
                EmEspesorMuroM: 0.50, EfEspesorFondoM: 0.20, EtEspesorTapaM: 0.0,
                HLAlturaLiquidoM: 3.0, HmAlturaSueloSobreMuroM: 3.0, WextSobrecargaKNm2: 0.0,
                Tipo: TipoTanque.EnterradoSinNivelFreatico, AlturaNivelFreaticoM: null),
            new Materiales(
                FcMPa: 21, FyMPa: 420, GammaConcretoKNm3: 24, GammaLiquidoKNm3: 9.81,
                GammaSueloKNm3: 16, PhiGradosAnguloFriccionSuelo: 30));
        // Sin tapa: CV/CG de cubierta deben ser 0 (validación de CargasGravitacionales)
        var parametros = new ParametrosCalculoTanque(
            CvCubiertaKNm2: 0.0, CgCubiertaKNm2: 0.0, CvFondoKNm2: 0.0,
            DiametrosBarra: new DiametrosBarraCalculo(15.9, 15.9, 15.9, 15.9),
            MetodoInterpolacion: MetodoInterpolacion.Interpolar, IncluirDiagramas: true,
            Sismo: new ParametrosSismoCalculo(
                new ParametrosEspectroDiseno(
                    Aa: 0.2, Av: 0.2, Fa: 1.3, Fv: 2.0, I: 1.0,
                    CondicionBase: CondicionBaseMuro.Rigida,
                    CondicionAnclaje: CondicionAnclajeBase.ArticuladaEmpotrada),
                new ParametrosSueloDinamico(
                    KhCoeficienteSismicoHorizontal: 0.2, KvCoeficienteSismicoVertical: 0.0,
                    DeltaGradosFriccionSueloMuro: 0.0, IGradosInclinacionRelleno: 0.0,
                    BetaGradosInclinacionMuro: 90.0)));

        var r = CalculadorTanque.Calcular(proyecto, parametros);

        Assert.NotNull(r.SismoHidro);
        Assert.NotNull(r.SismoSuelo);
        Assert.True(r.DisenoMuroLongitudinal.SismoIncluido);
        Assert.Null(r.DisenoMuroLongitudinal.MotivoSismoOmitido);
        // Con tapa ausente, la cubierta no aplica: null explícito
        Assert.Null(r.PlacaCubierta);
        Assert.Null(r.DisenoCubierta);
    }
}
