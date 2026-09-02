// ---------------------------------------------------------------------------
// Tanque.NSR10
// (c) Mauricio Javier Victoria Niño <hidratecsa@gmail.com> · CC BY-NC-SA 4.0
// Uso exclusivamente académico. El motor de cálculo (Tanque.Core) se distribuye
// como binario ofuscado, protegido por derechos de autor (ver LICENSE).
// ---------------------------------------------------------------------------
using System.Linq;
using Tanque.Core.Dominio;
using Tanque.Core.Modulos;
using Xunit;

namespace Tanque.Core.Tests;

/// <summary>
/// Pruebas espejo de <see cref="MurosRectangularesSismico"/> -- primer archivo de pruebas xUnit de
/// este módulo (no existía ninguno antes de la corrección del 2026-08-28; a diferencia del fix de
/// flexión, esto no era un espejo desincronizado, era un hueco de cobertura nunca cerrado, ver
/// RUTA_TRABAJO_PROXIMAS_SESIONES.md). Escrito inmediatamente después de las correcciones
/// H-CRÍTICO-1/H-ALTO-2 (flexión) y P1-P5/R1-R5 (sismo fuera de dominio) como parte de una
/// auditoría interna proactiva del propio proyecto -- ver el docstring de <see cref="DisenoMurosTests"/>
/// para el contexto completo. Mismos datos y valores esperados ya verificados en
/// tools/Tanque.Core.Verificacion/Program.cs (690/690 aserciones al momento de escribir este
/// archivo, secciones "Modulo 8 conectado a muro" y la corrección de sismo fuera de dominio).
/// </summary>
public class MurosRectangularesSismicoTests
{
    private static void AssertTol(string nombre, double actual, double esperado, double? atol = null)
    {
        var ok = atol.HasValue
            ? Tolerancia.SonIguales(actual, esperado, toleranciaAbsoluta: atol.Value)
            : Tolerancia.SonIguales(actual, esperado);
        Assert.True(ok, Tolerancia.Diagnostico(nombre, actual, esperado));
    }

    // ---- Geometría sintética "dentro de dominio", reutilizada de Program.cs ----
    private static Geometria GeoSintDentroDominio() => new(
        BAnchoM: 4.75, LLargoM: 6.25, HtAlturaM: 3.5, ConTapa: false,
        EmEspesorMuroM: 0.25, EfEspesorFondoM: 0.20, EtEspesorTapaM: 0.0,
        HLAlturaLiquidoM: 3.0, HmAlturaSueloSobreMuroM: 3.0, WextSobrecargaKNm2: 0.0);

    private static Materiales MatSint() => new(
        FcMPa: 21, FyMPa: 420, GammaConcretoKNm3: 24, GammaLiquidoKNm3: 9.81,
        GammaSueloKNm3: 16, PhiGradosAnguloFriccionSuelo: 30);

    private static ParametrosEspectroDiseno EspectroSint() => new(
        Aa: 0.2, Av: 0.2, Fa: 1.3, Fv: 2.0, I: 1.0,
        CondicionBase: CondicionBaseMuro.Rigida, CondicionAnclaje: CondicionAnclajeBase.ArticuladaEmpotrada);

    private static ParametrosSueloDinamico SueloSint() => new(
        KhCoeficienteSismicoHorizontal: 0.2, KvCoeficienteSismicoVertical: 0.0,
        DeltaGradosFriccionSueloMuro: 0.0, IGradosInclinacionRelleno: 0.0, BetaGradosInclinacionMuro: 90.0);

    [Fact]
    public void CalcularCargaSismicaInterior_FormulaB245_SRSS()
    {
        // 1.2*Ph + SRSS(Pi,Pc): Ph=10, Pi=6, Pc=8 -> SRSS=10 -> q=1.2*10+10=22.
        var q = MurosRectangularesSismico.CalcularCargaSismicaInterior(10.0, 6.0, 8.0);
        AssertTol("CalcularCargaSismicaInterior (B.2.4-5, SRSS 6-8-10)", q, 22.0, atol: 1e-9);
    }

    [Fact]
    public void CalcularCargaSismicaExterior_FormulaB247()
    {
        // 1.6*Ps2 + Qae: Ps2=5, Qae=3 -> q=1.6*5+3=11.
        var q = MurosRectangularesSismico.CalcularCargaSismicaExterior(5.0, 3.0);
        AssertTol("CalcularCargaSismicaExterior (B.2.4-7)", q, 11.0, atol: 1e-9);
    }

    [Fact]
    public void CalcularMuroLongitudinalYTransversal_GeometriaSinteticaNoCuadrada_NoLanzaYRCorrecto()
    {
        var geo = GeoSintDentroDominio();
        var proyecto = new ProyectoTanque(geo, MatSint());
        var presiones = PresionesLaterales.Calcular(proyecto);
        var sismoHidro = FuerzaSismicaHidrodinamica.Calcular(proyecto, EspectroSint());
        var sismoSuelo = FuerzaDinamicaSuelo.Calcular(proyecto, SueloSint());

        var muroL = MurosRectangularesSismico.CalcularMuroLongitudinal(proyecto, presiones, sismoHidro, sismoSuelo);
        var muroT = MurosRectangularesSismico.CalcularMuroTransversal(proyecto, presiones, sismoHidro, sismoSuelo);

        AssertTol("Muro longitudinal, R interior = (L-em)/HL", muroL.Interior.R, (geo.LLargoM - geo.EmEspesorMuroM) / geo.HLAlturaLiquidoM, atol: 1e-9);
        AssertTol("Muro transversal, R interior = (B-em)/HL", muroT.Interior.R, (geo.BAnchoM - geo.EmEspesorMuroM) / geo.HLAlturaLiquidoM, atol: 1e-9);
        Assert.False(muroL.Interior.EsAproximacionConservadora);
        Assert.Null(muroL.MotivoAproximacionInterior);
    }

    // ---- Geometría fuera del dominio sísmico del Capítulo 3 (b/a<1.0), interior Y exterior ----
    // Em generoso (0.6m) a propósito: la cota conservadora de una vía exige más acero que la
    // solución de placa en dos direcciones para la misma carga (sobreestimación segura); un muro
    // delgado típico podría disparar EspesorInsuficienteException, que no es lo que este bloque
    // quiere ejercitar (la mecánica de la aproximación, no el chequeo de espesor).
    private static Geometria GeoFueraDominio() => new(
        BAnchoM: 3.0, LLargoM: 3.5, HtAlturaM: 4.5, ConTapa: false,
        EmEspesorMuroM: 0.6, EfEspesorFondoM: 0.20, EtEspesorTapaM: 0.0,
        HLAlturaLiquidoM: 4.0, HmAlturaSueloSobreMuroM: 4.0, WextSobrecargaKNm2: 0.0);

    [Fact]
    public void FueraDeDominio_CalcularBajoNivel_SigueLanzando()
    {
        // El motor tabulado en sí (Calcular) NO cambió con la corrección P1-P5/R1-R5 -- solo el
        // nivel superior (CalcularMuroLongitudinal/Transversal) ahora degrada en vez de propagar.
        var geo = GeoFueraDominio();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MurosRectangularesSismico.Calcular(geo.LLargoM / geo.HLAlturaLiquidoM, geo.BAnchoM / geo.HLAlturaLiquidoM, 10, 1));
    }

    [Fact]
    public void FueraDeDominio_InteriorYExterior_UsanAproximacionConservadora_ConMotivoDocumentado()
    {
        var geo = GeoFueraDominio();
        var proyecto = new ProyectoTanque(geo, MatSint());
        var presiones = PresionesLaterales.Calcular(proyecto);
        var sismoHidro = FuerzaSismicaHidrodinamica.Calcular(proyecto, EspectroSint());
        var sismoSuelo = FuerzaDinamicaSuelo.Calcular(proyecto, SueloSint());

        var sismico = MurosRectangularesSismico.CalcularMuroLongitudinal(proyecto, presiones, sismoHidro, sismoSuelo);

        Assert.True(sismico.Interior.EsAproximacionConservadora);
        Assert.NotNull(sismico.MotivoAproximacionInterior);
        Assert.Contains("fuera del dominio tabulado", sismico.MotivoAproximacionInterior);

        Assert.NotNull(sismico.Exterior);
        Assert.True(sismico.Exterior!.EsAproximacionConservadora);
        Assert.NotNull(sismico.MotivoAproximacionExterior);
    }

    [Fact]
    public void FueraDeDominio_FormulasCerradasIndependientes_CantiliverMasFranja()
    {
        var geo = GeoFueraDominio();
        var proyecto = new ProyectoTanque(geo, MatSint());
        var presiones = PresionesLaterales.Calcular(proyecto);
        var sismoHidro = FuerzaSismicaHidrodinamica.Calcular(proyecto, EspectroSint());
        var sismoSuelo = FuerzaDinamicaSuelo.Calcular(proyecto, SueloSint());
        var sismico = MurosRectangularesSismico.CalcularMuroLongitudinal(proyecto, presiones, sismoHidro, sismoSuelo);

        // Recálculo INDEPENDIENTE: estática elemental a partir de la definición de cada fórmula,
        // sin reutilizar ningún método privado de MurosRectangularesSismico.
        var qInterior = MurosRectangularesSismico.CalcularCargaSismicaInterior(
            presiones.PhMaximaKNm2,
            sismoHidro.MuroLongitudinal.PresionImpulsiva.FondoKNm2,
            sismoHidro.MuroLongitudinal.PresionConvectiva.FondoKNm2);
        var a = geo.HLAlturaLiquidoM;
        var l = geo.LLargoM - geo.EmEspesorMuroM; // span EJE A EJE
        var cSobreA = (geo.BAnchoM - geo.EmEspesorMuroM) / a; // c/a=0.6: cabe en la fila b/a=3.0
        var tab = MurosRectangularesSismico.Calcular(3.0, cSobreA, qInterior, a).LadoLargo; // REFINAMIENTO 2026-09-02: cota tabulada de banda
        var mxTab = Math.Max(Math.Abs(tab.MxPosGobernanteKNmM), Math.Abs(tab.MxNegGobernanteKNmM));
        var myTab = Math.Max(Math.Abs(tab.MyPosGobernanteKNmM), Math.Abs(tab.MyNegGobernanteKNmM));
        var mxEsperado = Math.Min(qInterior * a * a / 2.0, mxTab); // cantiléver UNIFORME q*a^2/2 (hallazgo 2026-09-02), acotado por la fila b/a=3.0
        var myEsperado = Math.Min(qInterior * l * l / 8.0, myTab); // franja q*(L-em)^2/8, acotada por la fila b/a=3.0
        var vBaseEsperado = qInterior * a / 2.0;    // q*a/2 (convención triangular del Cs)
        var vLadoEsperado = Math.Min(qInterior * l / 2.0, tab.VSideMaxKNm); // franja q*(L-em)/2, acotada

        AssertTol("Interior conservador: Mx+ = min(q*a^2/2, |Mx| tabulado (3.0,c/a))", sismico.Interior.MxPosGobernanteKNmM, mxEsperado, atol: 1e-6);
        AssertTol("Interior conservador: Mx- = min(q*a^2/2, |Mx| tabulado (3.0,c/a))", sismico.Interior.MxNegGobernanteKNmM, mxEsperado, atol: 1e-6);
        AssertTol("Interior conservador: My+ = min(q*L^2/8, |My| tabulado (3.0,c/a))", sismico.Interior.MyPosGobernanteKNmM, myEsperado, atol: 1e-6);
        AssertTol("Interior conservador: My- = min(q*L^2/8, |My| tabulado (3.0,c/a))", sismico.Interior.MyNegGobernanteKNmM, myEsperado, atol: 1e-6);
        AssertTol("Interior conservador: V fondo = q*a/2", sismico.Interior.VBottomKNm, vBaseEsperado, atol: 1e-6);
        AssertTol("Interior conservador: V lateral max = min(q*L/2, V tabulado (3.0,c/a))", sismico.Interior.VSideMaxKNm, vLadoEsperado, atol: 1e-6);
        AssertTol("Interior conservador: V lateral medio = min(q*L/2, V tabulado (3.0,c/a))", sismico.Interior.VSideMidKNm, vLadoEsperado, atol: 1e-6);
    }

    [Fact]
    public void FueraDeDominio_CotaConservadora_EnLimiteDeDominio_EsSiempreMayorOIgualQueElValorTabulado()
    {
        // En b/a=1.0 (el límite inferior tabulado), la cota conservadora de una vía, evaluada con
        // la MISMA carga q y la misma altura a=b, debe ser >= el momento gobernante real de la
        // placa en dos direcciones -- es una cota superior de una vía, nunca menor.
        const double qLimite = 50.0;
        const double aLimite = 4.0; // b/a=1.0 -> span=altura=4.0
        var tabulado = MurosRectangularesSismico.Calcular(1.0, 0.5, qLimite, aLimite).LadoLargo;
        var mxConservador = qLimite * aLimite * aLimite / 2.0; // q*a^2/2 (cantiléver uniforme, hallazgo 2026-09-02)

        Assert.True(mxConservador >= tabulado.MxPosGobernanteKNmM - 1e-9,
            $"Cota conservadora ({mxConservador}) debe ser >= Mx+ tabulado ({tabulado.MxPosGobernanteKNmM})");
        Assert.True(mxConservador >= -tabulado.MxNegGobernanteKNmM - 1e-9,
            $"Cota conservadora ({mxConservador}) debe ser >= |Mx-| tabulado ({-tabulado.MxNegGobernanteKNmM})");

        // HALLAZGO 2026-09-02: la cota corregida q*a^2/2 (cantiléver UNIFORME) también debe
        // cubrir el borde LARGO del dominio (b/a=4.0), donde la cota anterior q*a^2/6 fallaba
        // (la tabla da |Mx| ≈ 0.43·q·a² > 0.167·q·a² -- la tabla es de carga UNIFORME).
        var tabuladoB4 = MurosRectangularesSismico.Calcular(4.0, 0.5, qLimite, aLimite).LadoLargo;
        var mxConservadorB4 = qLimite * aLimite * aLimite / 2.0;
        var tabuladoMxB4 = Math.Max(tabuladoB4.MxPosGobernanteKNmM, -tabuladoB4.MxNegGobernanteKNmM);
        Assert.True(mxConservadorB4 >= tabuladoMxB4 - 1e-9,
            $"Cota corregida b/a=4.0 ({mxConservadorB4}) debe ser >= |Mx| tabulado ({tabuladoMxB4})");
    }

    [Fact]
    public void Mixta_InteriorEnDominio_ExteriorFuera_InteriorNoQuedaContaminado()
    {
        // R2: interior DENTRO de dominio (b/a=2.0,c/a=1.5), exterior FUERA (b/a=0.8, Hm=7.5m>>HL)
        // -- antes de la corrección, el try/catch ÚNICO habría descartado también el interior
        // válido. Em generoso (0.6m), mismo motivo que GeoFueraDominio.
        var geo = new Geometria(
            BAnchoM: 5.1, LLargoM: 6.6, HtAlturaM: 8.0, ConTapa: false,
            EmEspesorMuroM: 0.6, EfEspesorFondoM: 0.20, EtEspesorTapaM: 0.0,
            HLAlturaLiquidoM: 3.0, HmAlturaSueloSobreMuroM: 7.5, WextSobrecargaKNm2: 0.0);
        var proyecto = new ProyectoTanque(geo, MatSint());
        var presiones = PresionesLaterales.Calcular(proyecto);
        var sismoHidro = FuerzaSismicaHidrodinamica.Calcular(proyecto, EspectroSint());
        var sismoSuelo = FuerzaDinamicaSuelo.Calcular(proyecto, SueloSint());
        var sismico = MurosRectangularesSismico.CalcularMuroLongitudinal(proyecto, presiones, sismoHidro, sismoSuelo);

        Assert.False(sismico.Interior.EsAproximacionConservadora);
        Assert.Null(sismico.MotivoAproximacionInterior);

        Assert.NotNull(sismico.Exterior);
        Assert.True(sismico.Exterior!.EsAproximacionConservadora);
        Assert.NotNull(sismico.MotivoAproximacionExterior);

        // El interior debe coincidir EXACTO con lo que produciría el motor de tabla normal,
        // llamado directamente (sin pasar por CalcularMuroLongitudinal) -- confirma ausencia de
        // contaminación cruzada entre las dos condiciones.
        var qInterior = MurosRectangularesSismico.CalcularCargaSismicaInterior(
            presiones.PhMaximaKNm2,
            sismoHidro.MuroLongitudinal.PresionImpulsiva.FondoKNm2,
            sismoHidro.MuroLongitudinal.PresionConvectiva.FondoKNm2);
        var interiorDirecto = MurosRectangularesSismico.Calcular(
            (geo.LLargoM - geo.EmEspesorMuroM) / geo.HLAlturaLiquidoM, (geo.BAnchoM - geo.EmEspesorMuroM) / geo.HLAlturaLiquidoM, qInterior, geo.HLAlturaLiquidoM).LadoLargo;
        AssertTol("Interior (R2) coincide exacto con Calcular() directo", sismico.Interior.MxPosGobernanteKNmM, interiorDirecto.MxPosGobernanteKNmM, atol: 1e-9);
    }

    // ---- Hallazgo N3/R3 (auditoría externa tercera ronda, 2026-08-28): la cota conservadora ahora
    // distingue régimen -- b/a≥2.0 (largo/bajo) acota My con la tabla REAL en b/a=4.0 en vez de la
    // franja horizontal sin acotar (que sobredimensionaba hasta ≈18× el momento estático). Mismos
    // datos y valores esperados ya verificados en tools/Tanque.Core.Verificacion/Program.cs
    // (698/698 aserciones al momento de escribir este archivo).

    [Fact]
    public void FueraDeDominio_RegimenLargoBajo_AcotaMyConTablaEnBSobreA4_NoConLaFranjaSinAcotar()
    {
        // b/a=(8.0-0.6)/2.5=2.96 (largo/bajo, eje a eje), c/a=(6.0-0.6)/2.5=2.16 (hueco de grilla: no cabe en la fila
        // tabulada b/a=3.0, cuyo c/a máximo es 2.0) -- mismo caso concreto de la auditoría externa.
        var geo = new Geometria(
            BAnchoM: 6.0, LLargoM: 8.0, HtAlturaM: 4.5, ConTapa: false,
            EmEspesorMuroM: 0.6, EfEspesorFondoM: 0.20, EtEspesorTapaM: 0.0,
            HLAlturaLiquidoM: 4.0, HmAlturaSueloSobreMuroM: 2.5, WextSobrecargaKNm2: 0.0);
        var proyecto = new ProyectoTanque(geo, MatSint());
        var presiones = PresionesLaterales.Calcular(proyecto);
        var sismoHidro = FuerzaSismicaHidrodinamica.Calcular(proyecto, EspectroSint());
        var sismoSuelo = FuerzaDinamicaSuelo.Calcular(proyecto, SueloSint());
        var sismico = MurosRectangularesSismico.CalcularMuroLongitudinal(proyecto, presiones, sismoHidro, sismoSuelo);

        Assert.NotNull(sismico.Exterior);
        Assert.True(sismico.Exterior!.EsAproximacionConservadora);
        Assert.DoesNotContain("docstring", sismico.MotivoAproximacionExterior);

        // Recálculo INDEPENDIENTE (no reutiliza TryAcotarMyConTablaEnBSobreA4 ni
        // MurosCapitulo3Caso7Coeficientes): valores del manual PCA, Capítulo 3, Caso 7, fila
        // b/a=4.0, columnas c/a=3.0 y c/a=2.0, copiados aquí literalmente de la fuente normativa.
        var qExterior = MurosRectangularesSismico.CalcularCargaSismicaExterior(presiones.Ps2MaximaKNm2, sismoSuelo.QaeKNm2);
        var aM = geo.HmAlturaSueloSobreMuroM;
        double[][] largoMyB4 =
        [
            [-338,-41,51,64,58,54], [-373,-38,47,58,51,48], [-303,-34,41,50,43,40], [-247,-29,35,40,33,30],
            [-197,-24,27,28,21,18], [-150,-20,17,15,7,4], [-105,-17,5,0,-9,-12], [-64,-14,-7,-17,-26,-29],
            [-28,-14,-22,-35,-44,-47], [-5,-16,-37,-54,-63,-66], [0,-22,-53,-73,-84,-87],
        ]; // c/a=3.0
        double[][] largoMyB4C2 =
        [
            [-281,-28,53,63,56,52], [-323,-26,49,57,50,46], [-267,-23,43,49,42,38], [-221,-20,36,39,32,28],
            [-180,-17,28,27,20,16], [-140,-14,17,14,6,3], [-100,-13,5,-1,-10,-13], [-63,-12,-8,-18,-27,-30],
            [-29,-14,-23,-36,-45,-48], [-5,-18,-39,-55,-64,-67], [0,-24,-55,-75,-84,-87],
        ]; // c/a=2.0
        double[][] largoMxyB4 =
        [
            [4,83,74,50,24,0], [10,79,74,50,24,0], [9,79,74,50,24,0], [8,79,73,50,24,0],
            [8,79,72,48,23,0], [7,76,69,45,21,0], [6,71,64,41,19,0], [5,62,55,34,15,0],
            [4,49,42,25,11,0], [2,29,24,14,6,0], [0,0,0,0,0,0],
        ]; // c/a=3.0
        double[][] largoMxyB4C2 =
        [
            [15,84,73,49,23,0], [25,81,73,49,24,0], [24,80,73,49,23,0], [23,80,72,48,23,0],
            [21,80,71,47,22,0], [19,77,68,44,20,0], [17,72,63,39,18,0], [14,63,54,33,15,0],
            [10,50,41,24,11,0], [5,30,23,13,6,0], [0,0,0,0,0,0],
        ]; // c/a=2.0
        var cSobreA = (geo.BAnchoM - geo.EmEspesorMuroM) / aM; // (6.0-0.6)/2.5 = 2.16 (eje a eje)
        var t = (cSobreA - 2.0) / (3.0 - 2.0);
        var escala = qExterior * aM * aM / 1000.0;
        var myPosGobEsperado = 0.0;
        var myNegGobEsperado = 0.0;
        for (var fila = 0; fila < 11; fila++)
        {
            for (var col = 0; col < 6; col++)
            {
                var myInterp = largoMyB4C2[fila][col] + t * (largoMyB4[fila][col] - largoMyB4C2[fila][col]);
                var mxyInterp = largoMxyB4C2[fila][col] + t * (largoMxyB4[fila][col] - largoMxyB4C2[fila][col]);
                myPosGobEsperado = Math.Max(myPosGobEsperado, Math.Max(0, myInterp + mxyInterp) * escala);
                myNegGobEsperado = Math.Max(myNegGobEsperado, -Math.Min(0, myInterp - mxyInterp) * escala);
            }
        }
        var myAcotadoEsperado = Math.Max(myPosGobEsperado, myNegGobEsperado);
        var mxEsperado = qExterior * aM * aM / 2.0;
        var myFranjaSinAcotar = qExterior * (geo.LLargoM - geo.EmEspesorMuroM) * (geo.LLargoM - geo.EmEspesorMuroM) / 8.0; // valor ANTERIOR a N3 (luz eje a eje)

        AssertTol("Exterior (b/a=2.96): Mx = q*a^2/2 (cantiléver UNIFORME, hallazgo 2026-09-02)", sismico.Exterior.MxPosGobernanteKNmM, mxEsperado, atol: 1e-6);
        AssertTol("Exterior (b/a=2.96): My acotado con tabla real en b/a=4.0 (recálculo independiente)", sismico.Exterior.MyPosGobernanteKNmM, myAcotadoEsperado, atol: 1e-6);
        Assert.True(sismico.Exterior.MyPosGobernanteKNmM < myFranjaSinAcotar - 1e-6,
            $"My acotado ({sismico.Exterior.MyPosGobernanteKNmM}) debe ser MENOR que la franja sin acotar del comportamiento anterior a N3 ({myFranjaSinAcotar})");

        // Espejo para el cortante lateral (hallazgo menor de la cuarta ronda de auditoría): en el
        // régimen largo/bajo, V también se acota al valor REAL tabulado en b/a=4.0 (coeficiente Cs
        // "side edge -- maximum" = 0.38 para r=4.0), NO a la franja completa q*L/2.
        var vAcotadoEsperado = 0.38 * qExterior * aM;
        var vFranjaSinAcotar = qExterior * (geo.LLargoM - geo.EmEspesorMuroM) / 2.0;
        AssertTol("Exterior (b/a=2.96): V lateral acotado con tabla real en b/a=4.0 (Cs=0.38, recálculo independiente)", sismico.Exterior.VSideMaxKNm, vAcotadoEsperado, atol: 1e-6);
        AssertTol("Exterior (b/a=2.96): V lateral medio = misma cota", sismico.Exterior.VSideMidKNm, vAcotadoEsperado, atol: 1e-6);
        Assert.True(sismico.Exterior.VSideMaxKNm < vFranjaSinAcotar - 1e-6,
            $"V lateral acotado ({sismico.Exterior.VSideMaxKNm}) debe ser MENOR que la franja sin acotar del comportamiento anterior ({vFranjaSinAcotar})");
    }

    [Fact]
    public void FueraDeDominio_RegimenLargoBajo_CSobreAFueraInclusoDelExtremoTabulado_UsaMyIgualAMx()
    {
        // b/a=(11.25-0.6)/2.5=4.26 (fuera del dominio incluso en b) y c/a=(8.75-0.6)/2.5=3.26 (>3.0, tampoco cabe
        // en la fila b/a=4.0): el intento de acotar My con la tabla también falla -- último recurso
        // cerrado, My=Mx (el momento horizontal no puede superar, en este régimen de predominio
        // claro del cantiléver, al propio momento vertical).
        var geo = new Geometria(
            BAnchoM: 8.75, LLargoM: 11.25, HtAlturaM: 3.0, ConTapa: false,
            EmEspesorMuroM: 0.6, EfEspesorFondoM: 0.20, EtEspesorTapaM: 0.0,
            HLAlturaLiquidoM: 2.0, HmAlturaSueloSobreMuroM: 2.5, WextSobrecargaKNm2: 0.0);
        var proyecto = new ProyectoTanque(geo, MatSint());
        var presiones = PresionesLaterales.Calcular(proyecto);
        var sismoHidro = FuerzaSismicaHidrodinamica.Calcular(proyecto, EspectroSint());
        var sismoSuelo = FuerzaDinamicaSuelo.Calcular(proyecto, SueloSint());
        var sismico = MurosRectangularesSismico.CalcularMuroLongitudinal(proyecto, presiones, sismoHidro, sismoSuelo);

        var qExterior = MurosRectangularesSismico.CalcularCargaSismicaExterior(presiones.Ps2MaximaKNm2, sismoSuelo.QaeKNm2);
        var mxEsperado = qExterior * geo.HmAlturaSueloSobreMuroM * geo.HmAlturaSueloSobreMuroM / 2.0;

        Assert.NotNull(sismico.Exterior);
        AssertTol("c/a fuera incluso del extremo tabulado b/a=4.0: My = Mx", sismico.Exterior!.MyPosGobernanteKNmM, mxEsperado, atol: 1e-6);
        AssertTol("My- = misma cota", sismico.Exterior.MyNegGobernanteKNmM, mxEsperado, atol: 1e-6);
        var vBaseEsperado = qExterior * geo.HmAlturaSueloSobreMuroM / 2.0; // q*a/2
        AssertTol("c/a fuera incluso del extremo tabulado b/a=4.0: V lateral = vBase (último recurso cerrado)", sismico.Exterior!.VSideMaxKNm, vBaseEsperado, atol: 1e-6);
    }

    [Fact]
    public void FueraDeDominio_BandaIntermedia_UnoPuntoCeroHastaDosPuntoCero_AcotaConFilaBSobreA3()
    {
        // b/a=(6.75-0.6)/4.5=1.367 (eje a eje, antes 1.5), c/a=(5.4-0.6)/4.5=1.067 fuera del
        // hueco de grilla de esa fila (b/a<2.0 solo tabula c/a=[1.0,0.5]). REFINAMIENTO 2026-09-02:
        // la fila b/a=3.0 del Capítulo 3 cubre c/a=1.067 (interpola entre 1.0 y 1.5) y, por la
        // monotonicidad verificada (|Mx| y |My| crecen con b/a y con c/a), es una cota superior
        // válida -- la cota de una vía se acota con el MÍNIMO entre la franja/cantiléver y el valor
        // tabulado en (3.0,c/a) (nunca se afloja respecto del comportamiento previo a N3).
        var geo = new Geometria(
            BAnchoM: 5.4, LLargoM: 6.75, HtAlturaM: 5.5, ConTapa: false,
            EmEspesorMuroM: 0.6, EfEspesorFondoM: 0.20, EtEspesorTapaM: 0.0,
            HLAlturaLiquidoM: 3.0, HmAlturaSueloSobreMuroM: 4.5, WextSobrecargaKNm2: 0.0);
        var proyecto = new ProyectoTanque(geo, MatSint());
        var presiones = PresionesLaterales.Calcular(proyecto);
        var sismoHidro = FuerzaSismicaHidrodinamica.Calcular(proyecto, EspectroSint());
        var sismoSuelo = FuerzaDinamicaSuelo.Calcular(proyecto, SueloSint());
        var sismico = MurosRectangularesSismico.CalcularMuroLongitudinal(proyecto, presiones, sismoHidro, sismoSuelo);

        Assert.NotNull(sismico.Exterior);
        Assert.True(sismico.Exterior!.EsAproximacionConservadora);

        var qExterior = MurosRectangularesSismico.CalcularCargaSismicaExterior(presiones.Ps2MaximaKNm2, sismoSuelo.QaeKNm2);
        var l = geo.LLargoM - geo.EmEspesorMuroM;
        var cSobreA = (geo.BAnchoM - geo.EmEspesorMuroM) / geo.HmAlturaSueloSobreMuroM;
        var tab = MurosRectangularesSismico.Calcular(3.0, cSobreA, qExterior, geo.HmAlturaSueloSobreMuroM).LadoLargo;
        var myTab = Math.Max(Math.Abs(tab.MyPosGobernanteKNmM), Math.Abs(tab.MyNegGobernanteKNmM));
        var myEsperado = Math.Min(qExterior * l * l / 8.0, myTab);
        AssertTol("Banda 1.0<=b/a<2.0: My = min(q*L^2/8, |My| tabulado (3.0,c/a)) [refinamiento 2026-09-02]",
            sismico.Exterior.MyPosGobernanteKNmM, myEsperado, atol: 1e-6);
        // El refinamiento NUNCA afloja respecto de la franja (<= franja); para esta geometría de
        // banda (b/a=1.367, c/a=1.067) la cota tabulada (3.0,c/a) queda ≈ a la franja, así que no se
        // exige estrictamente menor (sí lo es para c/a menores; el Mx sí queda estrictamente
        // refinado en todos los casos de la banda).
        Assert.True(myEsperado <= qExterior * l * l / 8.0 + 1e-9,
            "La cota tabulada (3.0,c/a) nunca debe aflojar respecto de la franja completa");
        var vTab = tab.VSideMaxKNm;
        var vEsperado = Math.Min(qExterior * l / 2.0, vTab);
        AssertTol("Banda 1.0<=b/a<2.0: V lateral = min(q*L/2, V tabulado (3.0,c/a)) [refinamiento 2026-09-02]",
            sismico.Exterior.VSideMaxKNm, vEsperado, atol: 1e-6);
        var mxTab = Math.Max(Math.Abs(tab.MxPosGobernanteKNmM), Math.Abs(tab.MxNegGobernanteKNmM));
        AssertTol("Banda 1.0<=b/a<2.0: Mx = min(q*a^2/2, |Mx| tabulado (3.0,c/a)) [refinamiento 2026-09-02]",
            sismico.Exterior.MxPosGobernanteKNmM, Math.Min(qExterior * geo.HmAlturaSueloSobreMuroM * geo.HmAlturaSueloSobreMuroM / 2.0, mxTab), atol: 1e-6);
    }
}
