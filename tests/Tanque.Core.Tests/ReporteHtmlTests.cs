// ---------------------------------------------------------------------------
// Tanque.NSR10
// (c) Mauricio Javier Victoria Niño <hidratecsa@gmail.com> · CC BY-NC-SA 4.0
// Uso exclusivamente académico. El motor de cálculo (Tanque.Core) se distribuye
// como binario ofuscado, protegido por derechos de autor (ver LICENSE).
// ---------------------------------------------------------------------------
using System;
using Tanque.Core.Dominio;
using Tanque.Core.Modulos;
using Tanque.Reportes;
using Xunit;

namespace Tanque.Core.Tests;

/// <summary>
/// Pruebas espejo de la sección "Reporte profesional HTML" de
/// tools/Tanque.Core.Verificacion (795/795 aserciones al momento de escribir este archivo) --
/// verifica la ESTRUCTURA del HTML autocontenido que genera <see cref="ReporteHtml.Generar"/>
/// (Fase3 del frente de interfaz): documento, encabezado con referencia normativa, banner de
/// veredicto (Fase1), las siete secciones agrupadas SIN grillas ASCII, mapas de calor SVG
/// balanceados y pie normativo. No valida el render visual en navegador (eso lo hace el usuario),
/// solo la estructura bien formada -- cero dependencias nuevas.
/// </summary>
public class ReporteHtmlTests
{
    private static Geometria GeoBase() => new(
        BAnchoM: 6.0, LLargoM: 8.0, HtAlturaM: 4.5, ConTapa: true,
        EmEspesorMuroM: 0.3, EfEspesorFondoM: 0.35, EtEspesorTapaM: 0.2,
        HLAlturaLiquidoM: 4.0, HmAlturaSueloSobreMuroM: 3.0, WextSobrecargaKNm2: 0.0,
        Tipo: TipoTanque.EnterradoConNivelFreatico, AlturaNivelFreaticoM: 1.0);

    private static Materiales MatBase() => new(
        FcMPa: 21, FyMPa: 420, GammaConcretoKNm3: 24, GammaLiquidoKNm3: 9.81,
        GammaSueloKNm3: 16, PhiGradosAnguloFriccionSuelo: 30, GammaSueloSaturadoKNm3: 18);

    private static ParametrosCalculoTanque ParametrosBase() => new(
        CvCubiertaKNm2: 0.0, CgCubiertaKNm2: 0.0, CvFondoKNm2: 0.0,
        DiametrosBarra: new DiametrosBarraCalculo(
            CatalogoBarras.DiametroPredeterminadoBarraMm, CatalogoBarras.DiametroPredeterminadoBarraMm,
            CatalogoBarras.DiametroPredeterminadoBarraMm, CatalogoBarras.DiametroPredeterminadoBarraMm),
        MetodoInterpolacion: MetodoInterpolacion.Interpolar, IncluirDiagramas: true, Sismo: null);

    [Fact]
    public void ReporteHtml_DocumentoConEncabezadoBannerSeccionesSvgYPie()
    {
        var resultado = CalculadorTanque.Calcular(new ProyectoTanque(GeoBase(), MatBase()), ParametrosBase());
        var html = ReporteHtml.Generar(resultado);

        // Documento y encabezado con referencia normativa.
        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains("Memoria de cálculo", html);
        Assert.Contains("NSR-10 Título C (C.23)", html);
 Assert.Contains("altura interior del muro", html);
 Assert.Contains("cara externa de la losa de fondo", html);
        Assert.Contains("(dimensiones EXTERIORES", html);

        // Banner de veredicto (Fase1) con la tabla de verificaciones.
        Assert.Contains("class=\"veredicto", html);
        Assert.Contains("Elemento", html);
        Assert.Contains("Concepto", html);

        // Las siete secciones agrupadas, sin grillas ASCII (los diagramas van como SVG).
        // Nota: "Dinámico" solo existe cuando hay análisis sísmico (ParametrosBase no lleva sismo).
        foreach (var grupo in new[] { "Datos generales", "Hidrostático / Tierras", "Sismo",
            "Diseño de losas", "Diseño de muros", "Envolventes" })
            Assert.Contains("<h2>" + grupo + "</h2>", html);
        if (resultado.Parametros.Sismo is null)
            Assert.DoesNotContain("<h2>Dinámico</h2>", html);
        else
            Assert.Contains("<h2>Dinámico</h2>", html);
        Assert.DoesNotContain(", kN·m/m (filas:", html);

        // Mapas de calor SVG presentes y balanceados.
        var abre = html.Split("<svg").Length - 1;
        var cierra = html.Split("</svg>").Length - 1;
        Assert.True(abre > 0, "debe haber al menos un SVG de mapa de calor");
        Assert.Equal(abre, cierra);

        // Pie normativo.
        Assert.Contains("<footer", html);
        Assert.Contains("ACI350.3-06", html);
        Assert.Contains("Mononobe-Okabe", html);

        // Balance de etiquetas (smoke-test de estructura bien formada).
        int Cuenta(string tag) => html.Split("<" + tag).Length - 1;
        Assert.Equal(Cuenta("section"), Cuenta("/section"));
        Assert.Equal(Cuenta("table"), Cuenta("/table"));
        Assert.Equal(Cuenta("figure"), Cuenta("/figure"));
        Assert.Equal(Cuenta("div"), Cuenta("/div"));
    }

    [Fact]
    public void ReporteHtml_SinTapaSuperficial_OmiteCubiertaYFlotabilidad()
    {
        var geo = new Geometria(BAnchoM: 3.0, LLargoM: 4.0, HtAlturaM: 2.5, ConTapa: false,
            EmEspesorMuroM: 0.15, EfEspesorFondoM: 0.20, EtEspesorTapaM: 0.0,
            HLAlturaLiquidoM: 2.0, HmAlturaSueloSobreMuroM: 2.0, WextSobrecargaKNm2: 0.0,
            Tipo: TipoTanque.EnterradoSinNivelFreatico);
        var resultado = CalculadorTanque.Calcular(new ProyectoTanque(geo, MatBase()), ParametrosBase());
        var html = ReporteHtml.Generar(resultado);

        // Las omisiones explícitas (nunca silenciosas) quedan en las secciones de texto.
        Assert.Contains("PLACA DE CUBIERTA -- OMITIDA", html);
        Assert.Contains("VERIFICACIÓN DE FLOTABILIDAD / LOSA DE FONDO BAJO SUBPRESIÓN / ENVOLVENTE -- OMITIDAS", html);

        // El banner de veredicto siempre está presente.
        Assert.Contains("class=\"veredicto", html);
        Assert.Contains("CUMPLE", html);
    }

    [Fact]
    public void ReporteHtml_MapasConOrientacionYGobernante()
    {
        var resultado = CalculadorTanque.Calcular(new ProyectoTanque(GeoBase(), MatBase()), ParametrosBase());
        var html = ReporteHtml.Generar(resultado);

        // Nota de orientación en lenguaje llano (orientación al no experto, 2026-08-31).
        Assert.Contains("Cómo leer los mapas", html);
        Assert.Contains("hogging", html);
        Assert.Contains("sagging", html);

        // Etiquetas de borde físico en los ejes (muro: tope/base; losa: borde/centro).
        Assert.Contains("tope ", html);
        Assert.Contains("base ", html);
        Assert.Contains("borde ", html);
        Assert.Contains("centro ", html);

        // Leyenda semántica y celda gobernante resaltada (borde oscuro + valor en negrita).
        Assert.Contains("(hogging)", html);
        Assert.Contains("(sagging)", html);
        Assert.Contains("stroke=\"#111827\"", html);
        Assert.Contains("font-weight=\"bold\"", html);
    }
}
