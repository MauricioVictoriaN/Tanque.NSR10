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
/// Pruebas espejo de <see cref="DisenoPlacas"/> (módulo 8 conectado a placas: cubierta y fondo).
/// Espejo de la sección "Modulo 8 conectado a placas" de tools/Tanque.Core.Verificacion/Program.cs.
/// Cubre: "d" por cara (formado en cubierta; mixto contra-suelo/formado en fondo), el "d" de
/// cortante conservador, la autoconsistencia con el diseño a flexión directo, el rango de cuantías
/// y el rechazo de cubierta sobre tanque sin tapa.
/// </summary>
public class DisenoPlacasTests
{
    private static (ProyectoTanque proyecto, ResultadoCargasGravitacionales cargas) ConstruirConTapa()
    {
        var geoTapa = new Geometria(
            BAnchoM: 4.5, LLargoM: 6.0, HtAlturaM: 3.8, ConTapa: true,
            EmEspesorMuroM: 0.25, EfEspesorFondoM: 0.20, EtEspesorTapaM: 0.20,
            HLAlturaLiquidoM: 3.0, HmAlturaSueloSobreMuroM: 3.0, WextSobrecargaKNm2: 0.0);
        var matTapa = new Materiales(
            FcMPa: 21, FyMPa: 420, GammaConcretoKNm3: 24, GammaLiquidoKNm3: 9.81,
            GammaSueloKNm3: 16, PhiGradosAnguloFriccionSuelo: 30);
        var proyecto = new ProyectoTanque(geoTapa, matTapa);
        var cargas = CargasGravitacionales.Calcular(proyecto, cargaVivaCubiertaKNm2: 1.5, cargaAdicionalCubiertaKNm2: 0.5);
        return (proyecto, cargas);
    }

    [Fact]
    public void Cubierta_AmbasCarasFormadas_MismoD()
    {
        var (proyecto, cargas) = ConstruirConTapa();
        var d = DisenoPlacas.DisenarPlacaCubierta(proyecto, cargas, cvKNm2: 1.5, cgKNm2: 0.5);

        AssertTol("dInferior == dSuperior", d.MxPositivo.DEfectivoM, d.MxNegativo.DEfectivoM, 1e-9);
        var dEsperado = RecubrimientosNSR10.CalcularDEfectivo(proyecto.Geometria.EtEspesorTapaM, RecubrimientosNSR10.RecubrimientoFormadoM, CatalogoBarras.DiametroPredeterminadoBarraMm);
        AssertTol("d = espesor - 0.050 - Ø/2", d.MxPositivo.DEfectivoM, dEsperado, 1e-9);

        // Ms (servicio) <= Mu (mayorado) en las 4 direcciones, ambos no negativos.
        var dirs = new[] { d.MxPositivo, d.MxNegativo, d.MyPositivo, d.MyNegativo };
        foreach (var dir in dirs)
            Assert.True(dir.MsKNm <= dir.MuKNm + 1e-9 && dir.MsKNm >= 0,
                $"Ms fuera de [0, Mu]: Ms={dir.MsKNm}, Mu={dir.MuKNm}");

        // Autoconsistencia con el diseño a flexión directo (mismo Mu/Ms/d), mismo catálogo de
        // detallado y la misma cuantía mínima de retracción/temperatura (C.23-C.7.12.2.1) que usa
        // el módulo -- cubierta: Mx corre a lo largo del Largo (L=6.0m → 0.0030 para fy=420).
        var ec = 4700.0 * Math.Sqrt(proyecto.Materiales.FcMPa);
        var cuantiaMinimaMx = CatalogoBarras.CuantiaMinimaRetracionTemperatura(proyecto.Geometria.LLargoM, proyecto.Materiales.FyMPa);
        var (flexionDirecta, _) = DisenoFlexionCortanteFisuracion.DisenarFlexionConControlFisuracion(
            d.MxPositivo.MuKNm, d.MxPositivo.MsKNm, d.MxPositivo.DEfectivoM, 1.0,
            proyecto.Materiales.FyMPa, proyecto.Materiales.FcMPa, 200000.0, ec, proyecto.Geometria.EtEspesorTapaM,
            diametroBarraMm: CatalogoBarras.DiametroPredeterminadoBarraMm,
            cuantiaMinima: cuantiaMinimaMx,
            espaciamientoMaximoM: CatalogoBarras.EspaciamientoMaximoMuroM);
        AssertTol("Flexion.Rho autoconsistente", d.MxPositivo.Flexion.Rho, flexionDirecta.Rho, 1e-9);
    }

    [Fact]
    public void Fondo_CaraInferiorContraSuelo_CaraSuperiorFormada_DDistintoYCortanteConservador()
    {
        var (proyecto, cargas) = ConstruirConTapa();
        var d = DisenoPlacas.DisenarPlacaFondo(proyecto, cargas, cvKNm2: 0.0);

        Assert.True(d.MxPositivo.DEfectivoM < d.MxNegativo.DEfectivoM,
            $"dInferior ({d.MxPositivo.DEfectivoM}) debería ser MENOR que dSuperior ({d.MxNegativo.DEfectivoM})");

        var dInf = RecubrimientosNSR10.CalcularDEfectivo(proyecto.Geometria.EfEspesorFondoM, RecubrimientosNSR10.RecubrimientoContraSueloM, CatalogoBarras.DiametroPredeterminadoBarraMm);
        var dSup = RecubrimientosNSR10.CalcularDEfectivo(proyecto.Geometria.EfEspesorFondoM, RecubrimientosNSR10.RecubrimientoFormadoM, CatalogoBarras.DiametroPredeterminadoBarraMm);
        AssertTol("dInferior = espesor - 0.075 - Ø/2", d.MxPositivo.DEfectivoM, dInf, 1e-9);
        AssertTol("dSuperior = espesor - 0.050 - Ø/2", d.MxNegativo.DEfectivoM, dSup, 1e-9);
        AssertTol("dCortante = min(dInferior, dSuperior)", d.CortanteX.DEfectivoM, Math.Min(dInf, dSup), 1e-9);

        // Cuantías de diseño dentro de [CuantiaMinima, CuantiaMaxima].
        var dirs = new[] { d.MxPositivo, d.MxNegativo, d.MyPositivo, d.MyNegativo };
        foreach (var dir in dirs)
            Assert.True(dir.Flexion.Rho >= DisenoFlexionCortanteFisuracion.CuantiaMinima - 1e-9
                        && dir.Flexion.Rho <= DisenoFlexionCortanteFisuracion.CuantiaMaxima + 1e-9,
                $"Rho fuera de rango: {dir.Flexion.Rho}");
    }

    [Fact]
    public void CuantiaMinimaRetraccionTemperatura_AplicaALasPlacas_NoLaGenerica_0_0018()
    {
        // Cruce normativo C.23-C.7.12.2.1 (2026-08-29): la cuantía mínima de retracción/temperatura
        // de PLACA es FUNCIÓN de la distancia entre juntas (≥0.0030 para fy=420), no la genérica
        // 0.0018 de losas ordinarias. Se verifica que cada dirección de cubierta y fondo usa, al
        // menos, el valor de la tabla en la dirección de su refuerzo -- cubierta Mx a lo largo del
        // Largo (L), cubierta My a lo largo del Ancho (B); fondo Mx a lo largo del Ancho (B), fondo
        // My a lo largo del Largo (L) -- ver la convención en DisenoPlacas.
        var (proyecto, cargas) = ConstruirConTapa();
        var g = proyecto.Geometria;
        var fy = proyecto.Materiales.FyMPa;

        var cub = DisenoPlacas.DisenarPlacaCubierta(proyecto, cargas, cvKNm2: 1.5, cgKNm2: 0.5);
        var fdo = DisenoPlacas.DisenarPlacaFondo(proyecto, cargas, cvKNm2: 0.0);

        var minCubMx = CatalogoBarras.CuantiaMinimaRetracionTemperatura(g.LLargoM, fy);
        var minCubMy = CatalogoBarras.CuantiaMinimaRetracionTemperatura(g.BAnchoM, fy);
        var minFdoMx = CatalogoBarras.CuantiaMinimaRetracionTemperatura(g.BAnchoM, fy);
        var minFdoMy = CatalogoBarras.CuantiaMinimaRetracionTemperatura(g.LLargoM, fy);

        Assert.True(cub.MxPositivo.Flexion.Rho >= minCubMx - 1e-9 && cub.MxNegativo.Flexion.Rho >= minCubMx - 1e-9,
            "Cubierta Mx debe usar al menos la cuantía mínima del Largo");
        Assert.True(cub.MyPositivo.Flexion.Rho >= minCubMy - 1e-9 && cub.MyNegativo.Flexion.Rho >= minCubMy - 1e-9,
            "Cubierta My debe usar al menos la cuantía mínima del Ancho");
        Assert.True(fdo.MxPositivo.Flexion.Rho >= minFdoMx - 1e-9 && fdo.MxNegativo.Flexion.Rho >= minFdoMx - 1e-9,
            "Fondo Mx debe usar al menos la cuantía mínima del Ancho");
        Assert.True(fdo.MyPositivo.Flexion.Rho >= minFdoMy - 1e-9 && fdo.MyNegativo.Flexion.Rho >= minFdoMy - 1e-9,
            "Fondo My debe usar al menos la cuantía mínima del Largo");

        // Y en ningún caso debe quedar por debajo de la genérica 0.0018.
        Assert.True(minCubMx >= DisenoFlexionCortanteFisuracion.CuantiaMinima,
            "El mínimo ambiental de cubierta debe superar la genérica 0.0018");
    }

    [Fact]
    public void Cubierta_SobreTanqueSinTapa_Lanza()
    {
        var (proyectoTapa, _) = ConstruirConTapa();
        var geoSinTapa = proyectoTapa.Geometria with { ConTapa = false, EtEspesorTapaM = 0.0 };
        var proyectoSinTapa = new ProyectoTanque(geoSinTapa, proyectoTapa.Materiales);
        var cargasSinTapa = CargasGravitacionales.Calcular(proyectoSinTapa);

        Assert.Throws<ArgumentException>(() => DisenoPlacas.DisenarPlacaCubierta(proyectoSinTapa, cargasSinTapa, 0.0, 0.0));
    }

    [Fact]
    public void Cubierta_DetalladoUsaElDiametroElegidoPorElemento()
    {
        var (proyecto, cargas) = ConstruirConTapa();
        var d = DisenoPlacas.DisenarPlacaCubierta(proyecto, cargas, cvKNm2: 1.5, cgKNm2: 0.5);

        // El detallado ya NO auto-selecciona la barra más delgada: usa el diámetro elegido por el
        // usuario (No.5 =15.9 mm por defecto), nunca9.5 mm (No.3, excluida por C.23-C.7.12.2.2).
        var f = d.MxPositivo.Flexion;
        Assert.NotNull(f.DiametroBarraMm);
        Assert.NotNull(f.SeparacionM);
        Assert.Equal(CatalogoBarras.DiametroPredeterminadoBarraMm, f.DiametroBarraMm!.Value);
        // La separación comercial resuelta suministra al menos el As requerido.
        Assert.True(CatalogoBarras.AreaBarraMm2(f.DiametroBarraMm.Value) / f.SeparacionM!.Value >= f.AsRequeridoMm2 - 1e-6,
            "La combinación Ø/s resuelta debe suministrar al menos el As requerido");
    }

    private static void AssertTol(string nombre, double actual, double esperado, double atol)
        => Assert.True(Tolerancia.SonIguales(actual, esperado, atol, 0.0),
            Tolerancia.Diagnostico(nombre, actual, esperado, atol, 0.0));
}
