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
/// Pruebas espejo de <see cref="DisenoLosaFondoSubpresion"/> (losa de fondo bajo subpresión de agua
/// freática, tanques enterrados con nivel freático). Espejo de la sección homónima de
/// tools/Tanque.Core.Verificacion/Program.cs. Cubre: rechazo fuera de EnterradoConNivelFreatico,
/// el caso "no aplica" (peso propio ya contrarresta la subpresión mayorada), el caso "sí aplica"
/// (recálculo independiente de la presión neta y de los momentos con inversión de caras), la
/// envolvente <see cref="DisenoLosaFondoSubpresion.Envolver"/> (por As requerido) y
/// <see cref="DisenoLosaFondoSubpresion.EnvolverCampos"/> (celda a celda), y la propagación del
/// ajuste geométrico cuando r cae fuera de rango.
/// </summary>
public class DisenoLosaFondoSubpresionTests
{
    private static readonly Materiales MatFlot = new(
        FcMPa: 21, FyMPa: 420, GammaConcretoKNm3: 24, GammaLiquidoKNm3: 9.81,
        GammaSueloKNm3: 16, PhiGradosAnguloFriccionSuelo: 30);

    private static readonly Geometria GeoBase = new(
        BAnchoM: 6.0, LLargoM: 8.0, HtAlturaM: 4.5, ConTapa: true,
        EmEspesorMuroM: 0.3, EfEspesorFondoM: 0.35, EtEspesorTapaM: 0.2,
        HLAlturaLiquidoM: 4.0, HmAlturaSueloSobreMuroM: 3.0, WextSobrecargaKNm2: 0.0);

    private static Geometria GeoFlotOk => GeoBase with { Tipo = TipoTanque.EnterradoConNivelFreatico, AlturaNivelFreaticoM = 1.0 };
    private static Geometria GeoFlotFail => GeoBase with { Tipo = TipoTanque.EnterradoConNivelFreatico, AlturaNivelFreaticoM = GeoBase.HmAlturaSueloSobreMuroM };

    [Fact]
    public void Disenar_RechazaFueraDeEnterradoConNivelFreatico()
    {
        var proyecto = new ProyectoTanque(GeoBase, MatFlot); // default EnterradoSinNivelFreatico
        var cargas = CargasGravitacionales.Calcular(proyecto);
        Assert.Throws<InvalidOperationException>(() => DisenoLosaFondoSubpresion.Disenar(proyecto, cargas));
    }

    [Fact]
    public void Disenar_NoAplica_CuandoElPesoPropioContrarrestaLaSubpresionMayorada()
    {
        var proyecto = new ProyectoTanque(GeoFlotOk, MatFlot);
        var cargas = CargasGravitacionales.Calcular(proyecto);
        var dAreal = cargas.PttTotalKN / (GeoFlotOk.BAnchoM * GeoFlotOk.LLargoM);
        var qMayoradoEsperado = 1.4 * Flotabilidad.GammaAguaKNm3 * 1.0 - 0.9 * dAreal;
        Assert.True(qMayoradoEsperado <= 0, "El caso debe construirse sin gobernar localmente (q_neto_mayorado <= 0)");

        var r = DisenoLosaFondoSubpresion.Disenar(proyecto, cargas);

        AssertTol("QNetoMayoradoKNm2", r.QNetoMayoradoKNm2, qMayoradoEsperado, 1e-6);
        Assert.False(r.Aplica);
        Assert.Null(r.MxCaraSuperior);
        Assert.Null(r.MxCaraInferior);
        Assert.Null(r.CortanteX);
        Assert.Contains("no gobierna", r.Mensaje);
    }

    [Fact]
    public void Disenar_SiAplica_RecalculoIndependienteCompleto()
    {
        var proyecto = new ProyectoTanque(GeoFlotFail, MatFlot);
        var cargas = CargasGravitacionales.Calcular(proyecto);
        // Luz EJE A EJE para la flexión (PCA pág. 173); huella EXTERIOR para repartir el peso.
        var a = GeoFlotFail.BAnchoM - GeoFlotFail.EmEspesorMuroM;
        var b = GeoFlotFail.LLargoM - GeoFlotFail.EmEspesorMuroM;
        var rPlaca = b / a;
        var dAreal = cargas.PttTotalKN / (GeoFlotFail.BAnchoM * GeoFlotFail.LLargoM);

        var qMayoradoEsperado = 1.4 * Flotabilidad.GammaAguaKNm3 * GeoFlotFail.HmAlturaSueloSobreMuroM - 0.9 * dAreal;
        var qServicioEsperado = Flotabilidad.GammaAguaKNm3 * GeoFlotFail.HmAlturaSueloSobreMuroM - dAreal;
        Assert.True(qMayoradoEsperado > 0, "El caso debe gobernar localmente (q_neto_mayorado > 0)");

        var r = DisenoLosaFondoSubpresion.Disenar(proyecto, cargas);
        Assert.True(r.Aplica);
        AssertTol("QNetoMayoradoKNm2 = 1.4*g*a*h - 0.9*Ptt/Area", r.QNetoMayoradoKNm2, qMayoradoEsperado, 1e-6);
        AssertTol("QNetoServicioKNm2 = g*h - Ptt/Area", r.QNetoServicioKNm2, qServicioEsperado, 1e-6);

        // Recalculo independiente de los momentos mayorada/servicio con PlacasRectangulares directo.
        var mayorada = PlacasRectangulares.Calcular(rPlaca, qMayoradoEsperado, a);
        var servicio = PlacasRectangulares.Calcular(rPlaca, qServicioEsperado, a);

        // Bajo subpresión, el campo "positivo" es tracción en la cara SUPERIOR (inversión de caras).
        AssertTol("MxCaraSuperior.MuKNm", r.MxCaraSuperior!.MuKNm, mayorada.MxPosGobernanteKNmM, 1e-9);
        AssertTol("MxCaraInferior.MuKNm", r.MxCaraInferior!.MuKNm, mayorada.MxNegGobernanteKNmM, 1e-9);
        AssertTol("MyCaraSuperior.MuKNm", r.MyCaraSuperior!.MuKNm, mayorada.MyPosGobernanteKNmM, 1e-9);
        AssertTol("MyCaraInferior.MuKNm", r.MyCaraInferior!.MuKNm, mayorada.MyNegGobernanteKNmM, 1e-9);
        AssertTol("MxCaraSuperior.MsKNm", r.MxCaraSuperior.MsKNm!.Value, servicio.MxPosGobernanteKNmM, 1e-9);

        // d por cara: superior formada (50mm), inferior contra suelo (75mm) -- inversión respecto a DisenoPlacas.
        var dSup = RecubrimientosNSR10.CalcularDEfectivo(GeoFlotFail.EfEspesorFondoM, RecubrimientosNSR10.RecubrimientoFormadoM, CatalogoBarras.DiametroPredeterminadoBarraMm);
        var dInf = RecubrimientosNSR10.CalcularDEfectivo(GeoFlotFail.EfEspesorFondoM, RecubrimientosNSR10.RecubrimientoContraSueloM, CatalogoBarras.DiametroPredeterminadoBarraMm);
        AssertTol("d cara superior (formada)", r.MxCaraSuperior.DEfectivoM, dSup, 1e-9);
        AssertTol("d cara inferior (contra suelo)", r.MxCaraInferior.DEfectivoM, dInf, 1e-9);

        // Cortante: Vu coincide con los cortantes mayorados de la placa.
        AssertTol("CortanteX.VuKN", r.CortanteX!.VuKN, mayorada.VxKNm, 1e-9);
        AssertTol("CortanteY.VuKN", r.CortanteY!.VuKN, mayorada.VyKNm, 1e-9);
    }

    [Fact]
    public void Disenar_CuantiaMinimaRetraccionTemperatura_PorDireccion()
    {
        // Cruce normativo C.23-C.7.12.2.1 (2026-08-29): la cuantía mínima de retracción/temperatura
        // de la losa de fondo es FUNCIÓN de la distancia entre juntas en la dirección de cada
        // refuerzo (aquí a=Ancho(B), b=Largo(L) -- misma convención que DisenoPlacas.DisenarPlacaFondo),
        // no la genérica 0.0018. Se verifica como cota inferior.
        var proyecto = new ProyectoTanque(GeoFlotFail, MatFlot);
        var cargas = CargasGravitacionales.Calcular(proyecto);
        var g = proyecto.Geometria;

        var r = DisenoLosaFondoSubpresion.Disenar(proyecto, cargas);
        Assert.True(r.Aplica);

        var minX = CatalogoBarras.CuantiaMinimaRetracionTemperatura(g.BAnchoM, proyecto.Materiales.FyMPa);
        var minY = CatalogoBarras.CuantiaMinimaRetracionTemperatura(g.LLargoM, proyecto.Materiales.FyMPa);
        // Notas opcionales C.23-C.7.12.2.1 (directiva 2026-08-30): la cara INFERIOR (contra el suelo)
        // admite reducción del 50 %; la SUPERIOR (formada) usa la cuantía completa.
        var minSuperiorX = CatalogoBarras.AjustarCuantiaMinimaRetracionTemperatura(minX, g.EfEspesorFondoM, caraInferiorContraSuelo: false);
        var minInferiorX = CatalogoBarras.AjustarCuantiaMinimaRetracionTemperatura(minX, g.EfEspesorFondoM, caraInferiorContraSuelo: true);
        var minSuperiorY = CatalogoBarras.AjustarCuantiaMinimaRetracionTemperatura(minY, g.EfEspesorFondoM, caraInferiorContraSuelo: false);
        var minInferiorY = CatalogoBarras.AjustarCuantiaMinimaRetracionTemperatura(minY, g.EfEspesorFondoM, caraInferiorContraSuelo: true);

        Assert.True(r.MxCaraSuperior!.Flexion.Rho >= minSuperiorX - 1e-9 && r.MxCaraInferior!.Flexion.Rho >= minInferiorX - 1e-9,
            "Subpresión Mx (a lo largo del Ancho B) debe respetar la cuantía mínima de la tabla (50% en cara inferior sobre suelo)");
        Assert.True(r.MyCaraSuperior!.Flexion.Rho >= minSuperiorY - 1e-9 && r.MyCaraInferior!.Flexion.Rho >= minInferiorY - 1e-9,
            "Subpresión My (a lo largo del Largo L) debe respetar la cuantía mínima de la tabla (50% en cara inferior sobre suelo)");

        Assert.True(minX >= DisenoFlexionCortanteFisuracion.CuantiaMinima - 1e-9,
            "El mínimo ambiental debe superar la genérica 0.0018");
    }

    [Fact]
    public void Disenar_RFueraDeRango_PropagaAjusteGeometrico()
    {
        var geo = GeoFlotFail with { BAnchoM = 20.0, LLargoM = 1.0 }; // r=L/B=0.05 << 0.5
        var proyecto = new ProyectoTanque(geo, MatFlot);
        var cargas = CargasGravitacionales.Calcular(proyecto);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => DisenoLosaFondoSubpresion.Disenar(proyecto, cargas));
        Assert.Contains("Caso 10, placa de fondo bajo subpresión", ex.Message);
        Assert.Contains("Ajuste la geometría", ex.Message);
    }

    [Fact]
    public void Envolver_NoAplica_CoincideConGravitacional()
    {
        var proyecto = new ProyectoTanque(GeoFlotOk, MatFlot);
        var cargas = CargasGravitacionales.Calcular(proyecto);
        var gravitacional = DisenoPlacas.DisenarPlacaFondo(proyecto, cargas, cvKNm2: 0.0);
        var sub = DisenoLosaFondoSubpresion.Disenar(proyecto, cargas);

        var e = DisenoLosaFondoSubpresion.Envolver(gravitacional, sub);

        AssertCaraCoincide(e.MxCaraInferior, gravitacional.MxPositivo);
        AssertCaraCoincide(e.MxCaraSuperior, gravitacional.MxNegativo);
        AssertCaraCoincide(e.MyCaraInferior, gravitacional.MyPositivo);
        AssertCaraCoincide(e.MyCaraSuperior, gravitacional.MyNegativo);
        Assert.False(e.CortanteX.GobernaSubpresion);
        Assert.False(e.CortanteY.GobernaSubpresion);
    }

    [Fact]
    public void Envolver_SiAplica_EligeCandidatoConMayorAs()
    {
        var proyecto = new ProyectoTanque(GeoFlotFail, MatFlot);
        var cargas = CargasGravitacionales.Calcular(proyecto);
        var gravitacional = DisenoPlacas.DisenarPlacaFondo(proyecto, cargas, cvKNm2: 0.0);
        var sub = DisenoLosaFondoSubpresion.Disenar(proyecto, cargas);

        var e = DisenoLosaFondoSubpresion.Envolver(gravitacional, sub);

        AssertCaraElMayorAs(e.MxCaraInferior, gravitacional.MxPositivo, sub.MxCaraInferior!);
        AssertCaraElMayorAs(e.MxCaraSuperior, gravitacional.MxNegativo, sub.MxCaraSuperior!);
        AssertCaraElMayorAs(e.MyCaraInferior, gravitacional.MyPositivo, sub.MyCaraInferior!);
        AssertCaraElMayorAs(e.MyCaraSuperior, gravitacional.MyNegativo, sub.MyCaraSuperior!);

        AssertCortanteElMayorVu(e.CortanteX, gravitacional.CortanteX, sub.CortanteX!);
        AssertCortanteElMayorVu(e.CortanteY, gravitacional.CortanteY, sub.CortanteY!);
    }

    [Fact]
    public void Envolver_PropagaElDetalladoDelCasoGobernante()
    {
        // Observación del usuario (2026-08-29): la sección "DISEÑO FINAL DE LA LOSA DE FONDO" no
        // mostraba la separación del refuerzo. La envolvente debe propagar el detallado (Ø/s) del
        // caso gobernante (gravitacional o subpresión), no solo Mu/As/ρ/d.
        var proyecto = new ProyectoTanque(GeoFlotFail, MatFlot);
        var cargas = CargasGravitacionales.Calcular(proyecto);
        var gravitacional = DisenoPlacas.DisenarPlacaFondo(proyecto, cargas, cvKNm2: 0.0);
        var sub = DisenoLosaFondoSubpresion.Disenar(proyecto, cargas);
        var e = DisenoLosaFondoSubpresion.Envolver(gravitacional, sub);

        var caras = new[] { e.MxCaraInferior, e.MxCaraSuperior, e.MyCaraInferior, e.MyCaraSuperior };
        foreach (var cara in caras)
        {
            Assert.NotNull(cara.DiametroBarraMm);
            Assert.NotNull(cara.SeparacionM);
            // La separación comercial resuelta suministra exactamente el As requerido del caso gobernante.
            AssertTol($"As == área(Ø)/s (Ø{cara.DiametroBarraMm:0.#}, s={cara.SeparacionM * 1000:0})",
                CatalogoBarras.AreaBarraMm2(cara.DiametroBarraMm!.Value) / cara.SeparacionM!.Value, cara.AsRequeridoMm2, 1e-6);
        }
    }

    [Fact]
    public void EnvolverCampos_NoAplica_CoincideConGravitacionalCeldaACelda()
    {
        var proyecto = new ProyectoTanque(GeoFlotOk, MatFlot);
        var cargas = CargasGravitacionales.Calcular(proyecto);
        var placa = PlacasRectangulares.CalcularPlacaFondo(proyecto, cargas, cvKNm2: 0.0);
        var sub = DisenoLosaFondoSubpresion.Disenar(proyecto, cargas);

        var campos = DisenoLosaFondoSubpresion.EnvolverCampos(placa, sub);

        for (var fila = 0; fila < 6; fila++)
            for (var col = 0; col < 6; col++)
            {
                AssertTol($"MxCaraInferior[{fila},{col}]", campos.CampoMxCaraInferior[fila, col], placa.CampoMxPos[fila, col], 1e-9);
                AssertTol($"MxCaraSuperior[{fila},{col}]", campos.CampoMxCaraSuperior[fila, col], -placa.CampoMxNeg[fila, col], 1e-9);
                AssertTol($"MyCaraInferior[{fila},{col}]", campos.CampoMyCaraInferior[fila, col], placa.CampoMyPos[fila, col], 1e-9);
                AssertTol($"MyCaraSuperior[{fila},{col}]", campos.CampoMyCaraSuperior[fila, col], -placa.CampoMyNeg[fila, col], 1e-9);
            }
    }

    [Fact]
    public void EnvolverCampos_SiAplica_EsElMaximoEnMagnitudCeldaACelda()
    {
        var proyecto = new ProyectoTanque(GeoFlotFail, MatFlot);
        var cargas = CargasGravitacionales.Calcular(proyecto);
        var placa = PlacasRectangulares.CalcularPlacaFondo(proyecto, cargas, cvKNm2: 0.0);
        var sub = DisenoLosaFondoSubpresion.Disenar(proyecto, cargas);

        var campos = DisenoLosaFondoSubpresion.EnvolverCampos(placa, sub);

        for (var fila = 0; fila < 6; fila++)
            for (var col = 0; col < 6; col++)
            {
                // Cara inferior: magnitud gravitacional (CampoMxPos >=0) vs subpresión (-CampoMxCaraInferior <=0).
                var maxInf = Math.Max(placa.CampoMxPos[fila, col], -sub.CampoMxCaraInferior![fila, col]);
                AssertTol($"MxCaraInferior[{fila},{col}]", campos.CampoMxCaraInferior[fila, col], maxInf, 1e-9);
                // Cara superior: magnitud gravitacional (-CampoMxNeg >=0) vs subpresión (CampoMxCaraSuperior >=0).
                var maxSup = Math.Max(-placa.CampoMxNeg[fila, col], sub.CampoMxCaraSuperior![fila, col]);
                AssertTol($"MxCaraSuperior[{fila},{col}]", campos.CampoMxCaraSuperior[fila, col], maxSup, 1e-9);
            }
    }

    private static void AssertCaraCoincide(ResultadoEnvolventeCaraPlacaFondo e, ResultadoDisenoDireccionPlaca g)
    {
        Assert.False(e.GobernaSubpresion);
        AssertTol("MuKNm", e.MuKNm, g.MuKNm, 1e-9);
        AssertTol("AsRequeridoMm2", e.AsRequeridoMm2, g.Flexion.AsRequeridoMm2, 1e-9);
        AssertTol("Rho", e.Rho, g.Flexion.Rho, 1e-9);
        AssertTol("DEfectivoM", e.DEfectivoM, g.DEfectivoM, 1e-9);
    }

    private static void AssertCaraElMayorAs(ResultadoEnvolventeCaraPlacaFondo e, ResultadoDisenoDireccionPlaca g, ResultadoDisenoCaraLosaFondoSubpresion s)
    {
        var gobernaSub = s.Flexion.AsRequeridoMm2 > g.Flexion.AsRequeridoMm2;
        Assert.Equal(gobernaSub, e.GobernaSubpresion);
        var (mu, asReq, rho, d) = gobernaSub ? (s.MuKNm, s.Flexion.AsRequeridoMm2, s.Flexion.Rho, s.DEfectivoM) : (g.MuKNm, g.Flexion.AsRequeridoMm2, g.Flexion.Rho, g.DEfectivoM);
        AssertTol("MuKNm", e.MuKNm, mu, 1e-9);
        AssertTol("AsRequeridoMm2", e.AsRequeridoMm2, asReq, 1e-9);
        AssertTol("Rho", e.Rho, rho, 1e-9);
        AssertTol("DEfectivoM", e.DEfectivoM, d, 1e-9);
    }

    private static void AssertCortanteElMayorVu(ResultadoEnvolventeCortantePlacaFondo e, ResultadoDisenoCortantePlaca g, ResultadoDisenoCortanteLosaFondoSubpresion s)
    {
        var gobernaSub = s.VuKN > g.VuKN;
        Assert.Equal(gobernaSub, e.GobernaSubpresion);
        AssertTol("VuKN", e.VuKN, gobernaSub ? s.VuKN : g.VuKN, 1e-9);
    }

    private static void AssertTol(string nombre, double actual, double esperado, double atol)
        => Assert.True(Tolerancia.SonIguales(actual, esperado, atol, 0.0),
            Tolerancia.Diagnostico(nombre, actual, esperado, atol, 0.0));
}
