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
/// Pruebas espejo de <see cref="DisenoMuros"/> -- primer archivo de pruebas xUnit de este módulo
/// (no existía ninguno antes de esta sesión).
///
/// CONTEXTO -- por qué se escribe esto ahora, sin que el usuario lo haya pedido módulo por
/// módulo: el 2026-08-28 se confirmaron y corrigieron dos hallazgos de seguridad estructural
/// (H-CRÍTICO-1/H-ALTO-2, signo de la ecuación de flexión y factor φ ausente; P1-P5/R1-R5, sismo
/// de muro omitido silenciosamente fuera del dominio tabulado del Capítulo 3, Caso 7). El segundo
/// de esos hallazgos, en su segunda ronda de auditoría (O1), mostró que un artefacto de
/// verificación desincronizado (una prueba xUnit que seguía validando la fórmula VIEJA) es, en sí
/// mismo, una instancia del principio "corregir, no heredar" incumplido -- no alcanza con
/// corregir el código de producción y la herramienta de verificación sin NuGet
/// (tools/Tanque.Core.Verificacion) si otro artefacto que declara verificar lo mismo queda atrás.
/// <see cref="DisenoMuros"/> y <see cref="MurosRectangularesSismico"/> (ver
/// <see cref="MurosRectangularesSismicoTests"/>) son, de los dos módulos tocados por la
/// corrección de sismo fuera de dominio, los que carecían POR COMPLETO de un espejo xUnit -- a
/// diferencia de la flexión, este no era un espejo desincronizado sino un hueco de cobertura
/// nunca cerrado (ya señalado como pendiente en RUTA_TRABAJO_PROXIMAS_SESIONES.md, "seis
/// módulos sin pruebas xUnit espejo"). Escrito como parte de una auditoría interna proactiva del
/// propio proyecto tras la reprimenda explícita del usuario del 2026-08-28 sobre la gravedad de
/// dejar huecos de verificación en una estructura de almacenamiento de agua -- ver esa misma
/// sección del documento de ruta de trabajo para el registro completo de la reprimenda y la
/// respuesta.
///
/// Mismos datos y valores esperados ya verificados en tools/Tanque.Core.Verificacion/Program.cs
/// (690/690 aserciones al momento de escribir este archivo, secciones "Modulo 8 conectado a
/// muro" y la corrección de sismo fuera de dominio, 2026-08-28).
///
/// LIMITACIÓN HONESTA, igual que el resto de los espejos xUnit de este proyecto: este archivo NO
/// se pudo compilar ni ejecutar con `dotnet test` real (nuget.org bloqueado en el sandbox de
/// sesión en la nube) -- se revisó manualmente contra la API real de Tanque.Core (los mismos
/// tipos y miembros que consume tools/Tanque.Core.Verificacion/Program.cs, ya compilado y
/// ejecutado de verdad) y contra la convención de sintaxis del resto de archivos de este proyecto
/// de pruebas (ImplicitUsings habilitado, mismo patrón AssertTol que
/// <see cref="MurosRectangularesSismicoTests"/>/FlotabilidadTests). El usuario debe confirmar
/// `dotnet test` en su propio equipo.
/// </summary>
public class DisenoMurosTests
{
    private static void AssertTol(string nombre, double actual, double esperado, double? atol = null)
    {
        var ok = atol.HasValue
            ? Tolerancia.SonIguales(actual, esperado, toleranciaAbsoluta: atol.Value)
            : Tolerancia.SonIguales(actual, esperado);
        Assert.True(ok, Tolerancia.Diagnostico(nombre, actual, esperado));
    }

    private static Geometria GeoSintDentroDominio() => new(
        BAnchoM: 4.5, LLargoM: 6.0, HtAlturaM: 3.5, ConTapa: false,
        // Em=0.50 (no 0.25): con la cota conservadora corregida Mx=q·a²/2 (hallazgo 2026-09-02,
        // tablas del Caso 7 UNIFORMES), el muro de 0.25 m superaba la cuantía máxima en la
        // envolvente sísmica fuera de dominio -- 0.50 m deja ρ ≈ 0.003, el mecanismo intacto.
        EmEspesorMuroM: 0.50, EfEspesorFondoM: 0.20, EtEspesorTapaM: 0.0,
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
    public void CalcularDEfectivo_MuroFormado_Em025_Barra25mm()
    {
        var d = RecubrimientosNSR10.CalcularDEfectivo(0.25, RecubrimientosNSR10.RecubrimientoFormadoM, 25.0);
        AssertTol("d = espesor - recubrimiento formado - Ø/2 (em=0.25, recub=0.05, Ø25mm)", d, 0.1875, atol: 1e-9);
    }

    [Fact]
    public void CalcularDEfectivo_EspesorInsuficiente_Lanza()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RecubrimientosNSR10.CalcularDEfectivo(0.05, RecubrimientosNSR10.RecubrimientoFormadoM, 25.0));
    }

    private (ProyectoTanque proyecto, ResultadoPresionesLaterales presiones,
        ResultadoFuerzaSismicaHidrodinamica sismoHidro, ResultadoFuerzaDinamicaSuelo sismoSuelo,
        ResultadoMuroSismicoPorCondiciones sismicoL, ResultadoMuroPorCondiciones estaticoL) EscenarioConSismo()
    {
        var geo = GeoSintDentroDominio();
        var proyecto = new ProyectoTanque(geo, MatSint());
        var presiones = PresionesLaterales.Calcular(proyecto);
        var sismoHidro = FuerzaSismicaHidrodinamica.Calcular(proyecto, EspectroSint());
        var sismoSuelo = FuerzaDinamicaSuelo.Calcular(proyecto, SueloSint());
        var estaticoL = MurosRectangulares.CalcularMuroLongitudinal(proyecto, presiones);
        var sismicoL = MurosRectangularesSismico.CalcularMuroLongitudinal(proyecto, presiones, sismoHidro, sismoSuelo);
        return (proyecto, presiones, sismoHidro, sismoSuelo, sismicoL, estaticoL);
    }

    [Fact]
    public void DisenarMuroLongitudinal_ConSismoDisponible_SismoIncluidoYDEfectivoCorrecto()
    {
        var (proyecto, presiones, sismoHidro, sismoSuelo, _, _) = EscenarioConSismo();
        var geo = GeoSintDentroDominio();
        var disenoL = DisenoMuros.DisenarMuroLongitudinal(proyecto, presiones, sismoHidro, sismoSuelo);

        Assert.True(disenoL.SismoIncluido);
        Assert.Null(disenoL.MotivoSismoOmitido);
        AssertTol("d expuesto == RecubrimientosNSR10.CalcularDEfectivo(em)", disenoL.DEfectivoM,
            RecubrimientosNSR10.CalcularDEfectivo(geo.EmEspesorMuroM, RecubrimientosNSR10.RecubrimientoFormadoM, CatalogoBarras.DiametroPredeterminadoBarraMm), atol: 1e-9);
    }

    [Fact]
    public void DisenarMuroLongitudinal_Envolvente_EsElMaximoDeLas4CondicionesIndependientes()
    {
        var (proyecto, presiones, sismoHidro, sismoSuelo, sismicoL, estaticoL) = EscenarioConSismo();
        var disenoL = DisenoMuros.DisenarMuroLongitudinal(proyecto, presiones, sismoHidro, sismoSuelo);

        // geoSint tiene Hm=3.0 (!=0), así que Exterior nunca es null aquí.
        double MaxDe4(Func<ResultadoMuroRectangular, double> sel) =>
            Math.Max(Math.Max(sel(estaticoL.Interior), sel(estaticoL.Exterior!)),
                     Math.Max(sel(sismicoL.Interior), sel(sismicoL.Exterior!)));

        AssertTol("Envolvente Mx+ = maximo de las 4 condiciones", disenoL.VerticalPositivo.MuKNm, MaxDe4(r => r.MxPosGobernanteKNmM), atol: 1e-9);
        AssertTol("Envolvente Mx- = maximo de las 4 condiciones", disenoL.VerticalNegativo.MuKNm, MaxDe4(r => r.MxNegGobernanteKNmM), atol: 1e-9);
        AssertTol("Envolvente My+ = maximo de las 4 condiciones", disenoL.HorizontalPositivo.MuKNm, MaxDe4(r => r.MyPosGobernanteKNmM), atol: 1e-9);
        AssertTol("Envolvente My- = maximo de las 4 condiciones", disenoL.HorizontalNegativo.MuKNm, MaxDe4(r => r.MyNegGobernanteKNmM), atol: 1e-9);
        AssertTol("Envolvente Vu fondo = maximo de las 4 condiciones", disenoL.CortanteFondo.VuKN, MaxDe4(r => r.VBottomKNm), atol: 1e-9);
        AssertTol("Envolvente Vu lateral maximo = maximo de las 4 condiciones", disenoL.CortanteLateralMaximo.VuKN, MaxDe4(r => r.VSideMaxKNm), atol: 1e-9);
    }

    [Fact]
    public void DisenarMuroLongitudinal_FlexionYFisuracion_AutoconsistentesConRecalculoDirecto()
    {
        var (proyecto, presiones, sismoHidro, sismoSuelo, _, _) = EscenarioConSismo();
        var geo = GeoSintDentroDominio();
        var mat = MatSint();
        var disenoL = DisenoMuros.DisenarMuroLongitudinal(proyecto, presiones, sismoHidro, sismoSuelo);

        var ec = 4700.0 * Math.Sqrt(mat.FcMPa);
        var hMuro = disenoL.DEfectivoM + RecubrimientosNSR10.RecubrimientoFormadoM + CatalogoBarras.DiametroPredeterminadoBarraMm / 2000.0;
        var (flexionDirecta, fisuracionDirecta) = DisenoFlexionCortanteFisuracion.DisenarFlexionConControlFisuracion(
            disenoL.VerticalPositivo.MuKNm, disenoL.VerticalPositivo.MsKNm, disenoL.DEfectivoM, 1.0,
            mat.FyMPa, mat.FcMPa, 200000.0, ec, hMuro,
            diametroBarraMm: CatalogoBarras.DiametroPredeterminadoBarraMm,
            cuantiaMinima: DisenoFlexionCortanteFisuracion.CuantiaMinimaMuroTanque,
            espaciamientoMaximoM: CatalogoBarras.EspaciamientoMaximoMuroM);

        AssertTol("Flexion.Rho (Mx+) autoconsistente", disenoL.VerticalPositivo.Flexion.Rho, flexionDirecta.Rho, atol: 1e-9);

        // Ms puede ser null (combinación gobernante sísmica) -- en ese caso ambos lados deben ser null.
        if (disenoL.VerticalPositivo.Fisuracion is null || fisuracionDirecta is null)
        {
            Assert.True(disenoL.VerticalPositivo.Fisuracion is null && fisuracionDirecta is null);
        }
        else
        {
            AssertTol("Fisuracion.FsMPa (Mx+) autoconsistente", disenoL.VerticalPositivo.Fisuracion.FsMPa, fisuracionDirecta.FsMPa, atol: 1e-9);
            Assert.Equal(disenoL.VerticalPositivo.Fisuracion.Cumple, fisuracionDirecta.Cumple);
        }
    }

    [Fact]
    public void SegundaPasadaFisuracion_AumentaAsCuandoFlexionPuraNoCumpleFisuracion()
    {
        // Reproduce el escenario reportado por el usuario (captura de pantalla, fs,adm≈21 MPa):
        // Mu pequeño (As por flexión cae en cuantía mínima) pero Ms grande (gamma≈1) fuerza fs a
        // superar fs,adm con la separación "nominal" que produce el As mínimo.
        // Escenario SINTÉTICO con pared fija delgada (d=0.1875, Em=0.25): el mecanismo de segunda
        // pasada es independiente de la geometría de diseño, y con la pared gruesa del resto de
        // estas pruebas (Em=0.50, d=0.4375) la fisuración deja de gobernar y el escenario no se
        // reproduce.
        var mat = MatSint();
        var ec = 4700.0 * Math.Sqrt(mat.FcMPa);
        const double dFijo = 0.1875; // Em=0.25 - recubrimiento 0.05 - Ø25/2 (escenario del usuario)
        const double hFijo = 0.25;

        const double muPequeno = 15.0;
        const double msGrande = 15.0;
        var flexionMin = DisenoFlexionCortanteFisuracion.DisenarFlexion(muPequeno, dFijo, 1.0, mat.FyMPa, mat.FcMPa);
        var areaBarra25 = Math.PI / 4.0 * 25.0 * 25.0;
        var sNominalMin = areaBarra25 / flexionMin.AsRequeridoMm2;
        var sinSegundaPasada = DisenoFlexionCortanteFisuracion.VerificarControlFisuracion(
            msGrande, flexionMin.AsRequeridoMm2, flexionMin.Rho, 200000.0, ec, dFijo, sNominalMin, hFijo);

        var (flexionConSegundaPasada, conSegundaPasada) = DisenoFlexionCortanteFisuracion.DisenarFlexionConControlFisuracion(
            muPequeno, msGrande, dFijo, 1.0, mat.FyMPa, mat.FcMPa, 200000.0, ec, hFijo);

        Assert.False(sinSegundaPasada.Cumple);
        Assert.NotNull(conSegundaPasada);
        Assert.True(conSegundaPasada!.Cumple);
        Assert.True(flexionConSegundaPasada.AsRequeridoMm2 > flexionMin.AsRequeridoMm2 + 1e-6);
        Assert.True(flexionConSegundaPasada.Rho <= DisenoFlexionCortanteFisuracion.CuantiaMaxima + 1e-9);
    }

    [Fact]
    public void DisenarMuroLongitudinal_TodasLasCuantias_CaenEnRangoValido()
    {
        var (proyecto, presiones, sismoHidro, sismoSuelo, _, _) = EscenarioConSismo();
        var disenoL = DisenoMuros.DisenarMuroLongitudinal(proyecto, presiones, sismoHidro, sismoSuelo);
        var direcciones = new[] { disenoL.VerticalPositivo, disenoL.VerticalNegativo, disenoL.HorizontalPositivo, disenoL.HorizontalNegativo };
        Assert.All(direcciones, d => Assert.InRange(d.Flexion.Rho,
            DisenoFlexionCortanteFisuracion.CuantiaMinima - 1e-9, DisenoFlexionCortanteFisuracion.CuantiaMaxima + 1e-9));
    }

    [Fact]
    public void DiametroInsuficiente_MarcaNoCumpleYSugiereElDiametroSuperior()
    {
        // Con el diámetro MÍNIMO (No.4 = 12.7 mm) y un momento grande (As de flexión por encima de
        // lo que No.4 puede suministrar ni a 75 mm), el detallado debe marcar NO CUMPLE
        // (DetalladoInsuficiente) y sugerir No.5 (15.9 mm) -- blindaje 2026-08-29.
        var mat = MatSint();
        var ec = 4700.0 * Math.Sqrt(mat.FcMPa);
        const double mu = 100.0, ms = 60.0, d = 0.15, h = 0.20;
        var (flexion, _) = DisenoFlexionCortanteFisuracion.DisenarFlexionConControlFisuracion(
            mu, ms, d, 1.0, mat.FyMPa, mat.FcMPa, 200000.0, ec, h,
            diametroBarraMm: CatalogoBarras.DiametroMinimoBarraMuroLosaMm,
            cuantiaMinima: DisenoFlexionCortanteFisuracion.CuantiaMinimaMuroTanque,
            espaciamientoMaximoM: CatalogoBarras.EspaciamientoMaximoMuroM);

        Assert.True(flexion.DetalladoInsuficiente, "El diámetro No.4 no alcanza para ese Mu; debe marcarse NO CUMPLE");
        Assert.Equal(CatalogoBarras.DiametroSiguienteMayor(CatalogoBarras.DiametroMinimoBarraMuroLosaMm)!.Value, flexion.DiametroSugeridoMm!.Value);
    }

    [Fact]
    public void RefuerzoMinimoMuro_CuantiaMinimaMuroTanque_GobiernaSobreLaGenerica()
    {
        // Cruce normativo C.23-C.14.3 (2026-08-29): el refuerzo de MURO de tanque usa la cuantía
        // mínima 0.0030 (vertical y horizontal), NO la genérica 0.0018 de retracción/temperatura
        // de losas. Un Mu pequeño debe saturarse en 0.0030 al diseñar un muro, mientras que la losa
        // (DisenoPlacas) sigue en 0.0018.
        var (proyecto, presiones, sismoHidro, sismoSuelo, _, _) = EscenarioConSismo();
        var mat = MatSint();
        var disenoL = DisenoMuros.DisenarMuroLongitudinal(proyecto, presiones, sismoHidro, sismoSuelo);

        const double muChico = 1.0;
        var flexionLosa = DisenoFlexionCortanteFisuracion.DisenarFlexion(muChico, disenoL.DEfectivoM, 1.0, mat.FyMPa, mat.FcMPa);
        var flexionMuro = DisenoFlexionCortanteFisuracion.DisenarFlexion(muChico, disenoL.DEfectivoM, 1.0, mat.FyMPa, mat.FcMPa,
            cuantiaMinima: DisenoFlexionCortanteFisuracion.CuantiaMinimaMuroTanque);

        AssertTol("losa: cuantía mínima genérica 0.0018", flexionLosa.Rho, DisenoFlexionCortanteFisuracion.CuantiaMinima, 1e-9);
        AssertTol("muro: cuantía mínima 0.0030 (C.23-C.14.3)", flexionMuro.Rho, DisenoFlexionCortanteFisuracion.CuantiaMinimaMuroTanque, 1e-9);
        Assert.True(flexionMuro.AsRequeridoMm2 > flexionLosa.AsRequeridoMm2 + 1e-6);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DisenoFlexionCortanteFisuracion.DisenarFlexion(muChico, disenoL.DEfectivoM, 1.0, mat.FyMPa, mat.FcMPa, cuantiaMinima: 0.02));
    }

    [Fact]
    public void RefuerzoMinimoMuroHorizontal_EsFuncionDeLaDistanciaEntreJuntas()
    {
        // Cruce normativo C.23-C.14.3.3 (2026-08-29): el refuerzo HORIZONTAL de muro usa la cuantía
        // FUNCIÓN de la distancia entre juntas (C.23-C.7.12.2.1), NO el 0.0030 fijo del refuerzo
        // VERTICAL (C.23-C.14.3.2). La distancia horizontal es la longitud del muro: L para el
        // longitudinal, B para el transversal. Se verifica como cota inferior (siempre ≥ tabla).
        var (proyecto, presiones, sismoHidro, sismoSuelo, _, _) = EscenarioConSismo();
        var g = proyecto.Geometria;
        var fy = proyecto.Materiales.FyMPa;

        var disenoL = DisenoMuros.DisenarMuroLongitudinal(proyecto, presiones, sismoHidro, sismoSuelo);
        var disenoT = DisenoMuros.DisenarMuroTransversal(proyecto, presiones, sismoHidro, sismoSuelo);

        var minHorizontalL = CatalogoBarras.CuantiaMinimaRetracionTemperatura(g.LLargoM, fy);
        var minHorizontalT = CatalogoBarras.CuantiaMinimaRetracionTemperatura(g.BAnchoM, fy);
        var minVertical = DisenoFlexionCortanteFisuracion.CuantiaMinimaMuroTanque;

        Assert.True(disenoL.HorizontalPositivo.Flexion.Rho >= minHorizontalL - 1e-9
                    && disenoL.HorizontalNegativo.Flexion.Rho >= minHorizontalL - 1e-9,
            "Muro longitudinal: el refuerzo horizontal debe respetar la cuantía mínima de la tabla para L");
        Assert.True(disenoT.HorizontalPositivo.Flexion.Rho >= minHorizontalT - 1e-9
                    && disenoT.HorizontalNegativo.Flexion.Rho >= minHorizontalT - 1e-9,
            "Muro transversal: el refuerzo horizontal debe respetar la cuantía mínima de la tabla para B");

        // El vertical conserva el 0.0030 fijo (C.23-C.14.3.2).
        Assert.True(disenoL.VerticalPositivo.Flexion.Rho >= minVertical - 1e-9
                    && disenoL.VerticalNegativo.Flexion.Rho >= minVertical - 1e-9,
            "Muro: el refuerzo vertical debe respetar 0.0030");

        // La cuantía mínima horizontal tabulada es siempre ≥ la vertical 0.0030 para fy=420.
        Assert.True(minHorizontalL >= minVertical - 1e-9 && minHorizontalT >= minVertical - 1e-9,
            "La cuantía mínima horizontal no debe ser menor que la vertical 0.0030");
    }

    [Fact]
    public void ControlDeFisuracion_PresenteSoloCuandoLaCondicionGobernanteEsEstatica()
    {
        var (proyecto, presiones, sismoHidro, sismoSuelo, _, _) = EscenarioConSismo();
        var disenoL = DisenoMuros.DisenarMuroLongitudinal(proyecto, presiones, sismoHidro, sismoSuelo);
        var direcciones = new[] { disenoL.VerticalPositivo, disenoL.VerticalNegativo, disenoL.HorizontalPositivo, disenoL.HorizontalNegativo };

        Assert.All(direcciones, d =>
        {
            if (d.ComboGobernante.StartsWith("Estático"))
            {
                Assert.NotNull(d.MsKNm);
                Assert.NotNull(d.Fisuracion);
                Assert.NotNull(d.Servicio);
            }
            else if (d.ComboGobernante.StartsWith("Sísmico"))
            {
                Assert.Null(d.MsKNm);
                Assert.Null(d.Fisuracion);
                Assert.Null(d.Servicio);
            }
        });
    }

    [Fact]
    public void DisenarMuroTransversal_MismoPatron_SismoIncluido()
    {
        var (proyecto, presiones, sismoHidro, sismoSuelo, _, _) = EscenarioConSismo();
        var disenoT = DisenoMuros.DisenarMuroTransversal(proyecto, presiones, sismoHidro, sismoSuelo);
        Assert.True(disenoT.SismoIncluido);
        Assert.Null(disenoT.MotivoSismoOmitido);
    }

    [Fact]
    public void DisenarMuroLongitudinal_SismoNoProvisto_CompletaConEnvolventeSoloEstatica()
    {
        var (proyecto, presiones, _, _, _, estaticoL) = EscenarioConSismo();
        var disenoSinSismo = DisenoMuros.DisenarMuroLongitudinal(proyecto, presiones, null, null);

        Assert.False(disenoSinSismo.SismoIncluido);
        Assert.NotNull(disenoSinSismo.MotivoSismoOmitido);
        Assert.Contains("No se proveyeron", disenoSinSismo.MotivoSismoOmitido);
        AssertTol("Sin sismo: envolvente Mx+ = maximo SOLO de las 2 condiciones estaticas",
            disenoSinSismo.VerticalPositivo.MuKNm,
            Math.Max(estaticoL.Interior.MxPosGobernanteKNmM, estaticoL.Exterior!.MxPosGobernanteKNmM), atol: 1e-9);
    }

    // ---- Corrección P1-P5/R1-R5 (sismo fuera del dominio tabulado, 2026-08-28): extremo a extremo ----
    private static Geometria GeoFueraDominio() => new(
        BAnchoM: 3.0, LLargoM: 3.5, HtAlturaM: 4.5, ConTapa: false,
        EmEspesorMuroM: 0.6, EfEspesorFondoM: 0.20, EtEspesorTapaM: 0.0,
        HLAlturaLiquidoM: 4.0, HmAlturaSueloSobreMuroM: 4.0, WextSobrecargaKNm2: 0.0);

    [Fact]
    public void FueraDeDominio_DisenarMuroLongitudinal_YaNoOmiteElSismo()
    {
        var geo = GeoFueraDominio();
        var proyecto = new ProyectoTanque(geo, MatSint());
        var presiones = PresionesLaterales.Calcular(proyecto);
        var sismoHidro = FuerzaSismicaHidrodinamica.Calcular(proyecto, EspectroSint());
        var sismoSuelo = FuerzaDinamicaSuelo.Calcular(proyecto, SueloSint());

        var diseno = DisenoMuros.DisenarMuroLongitudinal(proyecto, presiones, sismoHidro, sismoSuelo);

        Assert.True(diseno.SismoIncluido);
        Assert.Null(diseno.MotivoSismoOmitido);
        Assert.NotNull(diseno.NotaAproximacionSismicaInterior);
        Assert.NotNull(diseno.NotaAproximacionSismicaExterior);

        // Ningún ComboGobernante que empiece con "Sísmico" debe omitir el sufijo de aproximación
        // conservadora en esta geometría (interior Y exterior son ambos fuera de dominio aquí).
        var direcciones = new[] { diseno.VerticalPositivo.ComboGobernante, diseno.VerticalNegativo.ComboGobernante, diseno.HorizontalPositivo.ComboGobernante, diseno.HorizontalNegativo.ComboGobernante };
        var cortantes = new[] { diseno.CortanteFondo.ComboGobernante, diseno.CortanteLateralMaximo.ComboGobernante, diseno.CortanteLateralMedio.ComboGobernante };
        Assert.All(direcciones.Concat(cortantes), c => Assert.True(!c.StartsWith("Sísmico") || c.Contains("aproximación conservadora")));

        // H2 (2026-08-28): el motivo NO debe filtrar el mensaje crudo de una excepción .NET interna.
        Assert.DoesNotContain("Parameter", diseno.NotaAproximacionSismicaInterior);
        Assert.DoesNotContain("Parameter", diseno.NotaAproximacionSismicaExterior);
    }

    [Fact]
    public void Mixta_R2_SismoIncluido_NotaConservadoraSoloEnExterior_InteriorLimpio()
    {
        var geo = new Geometria(
            BAnchoM: 5.1, LLargoM: 6.6, HtAlturaM: 8.0, ConTapa: false,
            EmEspesorMuroM: 0.6, EfEspesorFondoM: 0.20, EtEspesorTapaM: 0.0,
            // Hm=6.2 (no 7.5): conserva el exterior FUERA de dominio (b/a=6.0/6.2=0.97<1.0) con un
            // Mu conservador que la pared de 0.6 m sí resiste (con la cota corregida Mx=q·a²/2).
            HLAlturaLiquidoM: 3.0, HmAlturaSueloSobreMuroM: 6.2, WextSobrecargaKNm2: 0.0);
        var proyecto = new ProyectoTanque(geo, MatSint());
        var presiones = PresionesLaterales.Calcular(proyecto);
        var sismoHidro = FuerzaSismicaHidrodinamica.Calcular(proyecto, EspectroSint());
        var sismoSuelo = FuerzaDinamicaSuelo.Calcular(proyecto, SueloSint());

        var diseno = DisenoMuros.DisenarMuroLongitudinal(proyecto, presiones, sismoHidro, sismoSuelo);

        Assert.True(diseno.SismoIncluido);
        Assert.Null(diseno.MotivoSismoOmitido);
        Assert.Null(diseno.NotaAproximacionSismicaInterior);
        Assert.NotNull(diseno.NotaAproximacionSismicaExterior);
    }

    [Fact]
    public void TipoTanqueSuperficial_HmCero_CondicionExteriorNoExiste_NoLanza()
    {
        // Hallazgo confirmado 2026-08-26 vía prueba real de usuario: TipoTanque.Superficial (Hm=0
        // por definición) lanzaba ArgumentOutOfRangeException sin capturar. Corregido para
        // devolver Exterior=null y propagar la ausencia hasta DisenoMuros.
        var geo = new Geometria(
            BAnchoM: 6.0, LLargoM: 8.0, HtAlturaM: 4.5, ConTapa: true,
            EmEspesorMuroM: 0.3, EfEspesorFondoM: 0.35, EtEspesorTapaM: 0.2,
            HLAlturaLiquidoM: 4.0, HmAlturaSueloSobreMuroM: 0.0, WextSobrecargaKNm2: 0.0,
            Tipo: TipoTanque.Superficial);
        var proyecto = new ProyectoTanque(geo, MatSint());
        var presiones = PresionesLaterales.Calcular(proyecto);

        var estaticoL = MurosRectangulares.CalcularMuroLongitudinal(proyecto, presiones);
        Assert.Null(estaticoL.Exterior);
        Assert.NotNull(estaticoL.Interior);

        var disenoL = DisenoMuros.DisenarMuroLongitudinal(proyecto, presiones, null, null);
        Assert.NotNull(disenoL.MotivoExteriorOmitido);
        Assert.Contains("Hm=0", disenoL.MotivoExteriorOmitido);
        Assert.StartsWith("Estático interior", disenoL.VerticalPositivo.ComboGobernante);
        Assert.True(disenoL.VerticalPositivo.MuKNm > 0);
    }
}
