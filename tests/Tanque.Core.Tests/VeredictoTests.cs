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
/// Pruebas espejo de la sección "Veredicto global (Fase 1 del frente de interfaz)" de
/// tools/Tanque.Core.Verificacion/Program.cs (764/764 aserciones al momento de escribir este
/// archivo). El veredicto (<see cref="Veredicto.Calcular"/>) solo compone señales normativas YA
/// verificadas por cada módulo (espesor C.23-C.14.6, detallado Ø/s, fisuración fs≤fs,adm,
/// cortante Vu≤Vc, flotabilidad FS≥1.25) -- no introduce ninguna fórmula ni valor nuevo (principio
/// rector). Ver el docstring de Modulos/Veredicto.cs para el detalle de alcance.
/// </summary>
public class VeredictoTests
{
    private static Geometria GeoBase() => new(
        BAnchoM: 6.0, LLargoM: 8.0, HtAlturaM: 4.5, ConTapa: true,
        EmEspesorMuroM: 0.3, EfEspesorFondoM: 0.35, EtEspesorTapaM: 0.2,
        HLAlturaLiquidoM: 4.0, HmAlturaSueloSobreMuroM: 3.0, WextSobrecargaKNm2: 0.0);

    private static Materiales MatBase() => new(
        FcMPa: 21, FyMPa: 420, GammaSueloKNm3: 16, GammaConcretoKNm3: 24, GammaLiquidoKNm3: 9.81,
        PhiGradosAnguloFriccionSuelo: 30, GammaSueloSaturadoKNm3: 18);

    private static ParametrosCalculoTanque ParametrosBase() => new(
        CvCubiertaKNm2: 0.0, CgCubiertaKNm2: 0.0, CvFondoKNm2: 2.0,
        DiametrosBarra: new DiametrosBarraCalculo(
            CatalogoBarras.DiametroPredeterminadoBarraMm, CatalogoBarras.DiametroPredeterminadoBarraMm,
            CatalogoBarras.DiametroPredeterminadoBarraMm, CatalogoBarras.DiametroPredeterminadoBarraMm),
        MetodoInterpolacion: MetodoInterpolacion.Interpolar,
        IncluirDiagramas: false,
        Sismo: null);

    [Fact]
    public void Veredicto_EsConjuncionDeSusItems_Y_CruzaSenalesCrudas()
    {
        var geo = GeoBase() with { Tipo = TipoTanque.EnterradoConNivelFreatico, AlturaNivelFreaticoM = 1.0 };
        var resultado = CalculadorTanque.Calcular(new ProyectoTanque(geo, MatBase()), ParametrosBase());
        var v = Veredicto.Calcular(resultado);

        // (1) Invariante: Cumple == conjunción de todos los ítems.
        Assert.Equal(v.Items.All(i => i.Cumple), v.Cumple);

        // (2) Cruce contra señales crudas del resultado (independiente del código de Veredicto).
        bool Item(string elem, string concepto) => v.Items.First(i => i.Elemento == elem && i.Concepto == concepto).Cumple;

        Assert.Equal(resultado.EspesorMinimoMuro.Cumple, Item("Muros", "Espesor mínimo"));

        var flotEsperado = resultado.Flotabilidad!.Cumple || resultado.Sobreancho?.EsPosible == true;
        Assert.Equal(flotEsperado, Item("Estructura", "Flotabilidad (FS ≥ 1.25)"));

        var cub = resultado.DisenoCubierta!;
        var cubDetEsperado = !new[] { cub.MxPositivo, cub.MxNegativo, cub.MyPositivo, cub.MyNegativo }
            .Any(d => d.Flexion.DetalladoInsuficiente);
        Assert.Equal(cubDetEsperado, Item("Cubierta", "Detallado Ø/s"));

        var cubCortEsperado = new[] { cub.CortanteX, cub.CortanteY }.All(c => c.Cortante.Cumple);
        Assert.Equal(cubCortEsperado, Item("Cubierta", "Cortante (Vu ≤ Vc)"));
    }

    [Fact]
    public void Veredicto_EspesorInsuficiente_NoCumple()
    {
        // Altura 2.5m ≤ 3m → mínimo C.23-C.14.6.1 = 150mm; Em=0.14m lo incumple → NO CUMPLE.
        var geo = new Geometria(
            BAnchoM: 3.0, LLargoM: 4.0, HtAlturaM: 2.5, ConTapa: false,
            EmEspesorMuroM: 0.14, EfEspesorFondoM: 0.20, EtEspesorTapaM: 0.0,
            HLAlturaLiquidoM: 2.0, HmAlturaSueloSobreMuroM: 2.0, WextSobrecargaKNm2: 0.0);
        var resultado = CalculadorTanque.Calcular(new ProyectoTanque(geo, MatBase()), ParametrosBase());
        var v = Veredicto.Calcular(resultado);

        Assert.False(v.Cumple);
        var espesor = v.Items.First(i => i.Elemento == "Muros" && i.Concepto == "Espesor mínimo");
        Assert.False(espesor.Cumple);
        Assert.Contains("déficit", espesor.Detalle);
    }
}
