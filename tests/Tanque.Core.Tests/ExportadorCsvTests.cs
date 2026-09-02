// ---------------------------------------------------------------------------
// Tanque.NSR10
// (c) Mauricio Javier Victoria Niño <hidratecsa@gmail.com> · CC BY-NC-SA 4.0
// Uso exclusivamente académico. El motor de cálculo (Tanque.Core) se distribuye
// como binario ofuscado, protegido por derechos de autor (ver LICENSE).
// ---------------------------------------------------------------------------
using System;
using System.Linq;
using System.Globalization;
using Tanque.Core.Dominio;
using Tanque.Core.Modulos;
using Tanque.Reportes;
using Xunit;

namespace Tanque.Core.Tests;

/// <summary>
/// Pruebas espejo de la sección "Exportación CSV" de tools/Tanque.Core.Verificacion (806/806
/// aserciones al momento de escribir este archivo) -- verifica la ESTRUCTURA del CSV de formato
/// largo que genera <see cref="ExportadorCsv.Generar"/> (Fase4 del frente de interfaz, ítem 2):
/// cabecera fija de 12 columnas, los 8 bloques, que "Momento" vuelca TODAS las celdas de los
/// campos de <see cref="DiagramaMomento"/>, que el escalar exportado coincide con el registro ya
/// verificado (round-trip, sin fórmulas nuevas) y que los campos con coma se escapan con comillas
/// (RFC 4180). Cero dependencias nuevas.
/// </summary>
public class ExportadorCsvTests
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

    private static ResultadoCalculoTanque CalcularBase() =>
        CalculadorTanque.Calcular(new ProyectoTanque(GeoBase(), MatBase()), ParametrosBase());

    // Cuenta campos CSV respetando comillas (las comillas dobles no aparecen en los datos, así que
    // un esquema simple basta para este formato).
    private static int ContarColumnas(string linea)
    {
        var n = 1;
        var enComillas = false;
        foreach (var ch in linea)
        {
            if (enComillas) { if (ch == '"') enComillas = false; }
            else if (ch == '"') enComillas = true;
            else if (ch == ',') n++;
        }
        return n;
    }

    [Fact]
    public void ExportadorCsv_TablaUnicaConCabeceraYLosOchoBloques()
    {
        var resultado = CalcularBase();
        var csv = ExportadorCsv.Generar(resultado);
        var lineas = csv.Split("\r\n").Where(l => l.Length > 0).ToArray();

        // Cabecera fija de 12 columnas.
        Assert.Equal(
            "Bloque,Elemento,Concepto,Detalle,Subdetalle,Fila,Columna,PosFila_m,PosCol_m,Valor,Unidad,Texto",
            lineas[0]);

        // Toda línea (cabecera y datos) tiene exactamente 12 columnas.
        Assert.All(lineas, l => Assert.Equal(12, ContarColumnas(l)));

        // Los ocho bloques presentes.
        foreach (var bloque in new[] { "Veredicto", "Momento", "Diseño", "Cortante",
            "Cargas", "Presiones", "Flotabilidad", "EspesorMínimo" })
            Assert.Contains(lineas, l => l.StartsWith(bloque + ",", StringComparison.Ordinal));
    }

    [Fact]
    public void ExportadorCsv_MomentoVuelcaTodasLasCeldasDeLosCampos()
    {
        var resultado = CalcularBase();
        var csv = ExportadorCsv.Generar(resultado);
        var lineas = csv.Split("\r\n").Where(l => l.Length > 0).ToArray();

        var campos = DiagramaMomento.Calcular(resultado).Campos;
        var celdasEsperadas = campos.Sum(c => c.Valores.GetLength(0) * c.Valores.GetLength(1));
        var filasMomento = lineas.Count(l => l.StartsWith("Momento,", StringComparison.Ordinal));

        Assert.Equal(celdasEsperadas, filasMomento);
        Assert.True(celdasEsperadas > 0, "debe haber al menos un campo de momento exportado");
    }

    [Fact]
    public void ExportadorCsv_EsDeterminista()
    {
        var resultado = CalcularBase();
        Assert.Equal(ExportadorCsv.Generar(resultado), ExportadorCsv.Generar(resultado));
    }

    [Fact]
    public void ExportadorCsv_RoundTripDelMuDeCubiertaMxPositivo()
    {
        var resultado = CalcularBase();
        var csv = ExportadorCsv.Generar(resultado);

        var filaMu = csv.Split("\r\n").FirstOrDefault(l =>
            l.StartsWith("Diseño,Cubierta,Mu,Mx+,", StringComparison.Ordinal));
        Assert.NotNull(filaMu);

        var campos = filaMu!.Split(',');
        // campos[9] es "Valor"; el resto de la fila no lleva comillas (solo números y "kN·m/m").
        var valor = double.Parse(campos[9], CultureInfo.InvariantCulture);
        Assert.Equal(resultado.DisenoCubierta!.MxPositivo.MuKNm, valor, 6);
    }

    [Fact]
    public void ExportadorCsv_EscapaComaDentroDeComillas()
    {
        // El concepto "Fisuración (fs ≤ fs,adm)" lleva una coma interna, así que DEBE quedar
        // entrecomillado (RFC 4180) -- verifica que el escapado de comas funciona.
        var resultado = CalcularBase();
        var csv = ExportadorCsv.Generar(resultado);

        Assert.Contains(",\"Fisuración (fs ≤ fs,adm)\",", csv);

        // Y, recíprocamente, que una línea Veredicto con este concepto tenga sus 12 columnas
        // intactas (la coma interna no rompe la estructura).
        var fila = csv.Split("\r\n").First(l => l.Contains("Fisuración (fs ≤ fs,adm)"));
        Assert.Equal(12, ContarColumnas(fila));
    }

    [Fact]
    public void ExportadorCsv_SinTapaNiNivelFreatico_OmiteCubiertaYFlotabilidad()
    {
        var geo = new Geometria(BAnchoM: 3.0, LLargoM: 4.0, HtAlturaM: 2.5, ConTapa: false,
            EmEspesorMuroM: 0.15, EfEspesorFondoM: 0.20, EtEspesorTapaM: 0.0,
            HLAlturaLiquidoM: 2.0, HmAlturaSueloSobreMuroM: 2.0, WextSobrecargaKNm2: 0.0,
            Tipo: TipoTanque.EnterradoSinNivelFreatico);
        var resultado = CalculadorTanque.Calcular(new ProyectoTanque(geo, MatBase()), ParametrosBase());
        var csv = ExportadorCsv.Generar(resultado);
        var lineas = csv.Split("\r\n").Where(l => l.Length > 0).ToArray();

        // Sin tapa → no hay "Cubierta" en Diseño/Cortante/Momento; sin nivel freático → sin Flotabilidad.
        Assert.DoesNotContain(lineas, l => l.Contains(",Cubierta,") && (l.StartsWith("Diseño,") || l.StartsWith("Cortante,") || l.StartsWith("Momento,")));
        Assert.DoesNotContain(lineas, l => l.StartsWith("Flotabilidad,", StringComparison.Ordinal));

        // El bloque Veredicto y Momento siguen presentes.
        Assert.Contains(lineas, l => l.StartsWith("Veredicto,", StringComparison.Ordinal));
        Assert.Contains(lineas, l => l.StartsWith("Momento,", StringComparison.Ordinal));
    }
}
