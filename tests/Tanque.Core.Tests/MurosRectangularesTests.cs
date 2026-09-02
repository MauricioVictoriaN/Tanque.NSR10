// ---------------------------------------------------------------------------
// Tanque.NSR10
// (c) Mauricio Javier Victoria Niño <hidratecsa@gmail.com> · CC BY-NC-SA 4.0
// Uso exclusivamente académico. El motor de cálculo (Tanque.Core) se distribuye
// como binario ofuscado, protegido por derechos de autor (ver LICENSE).
// ---------------------------------------------------------------------------
using System.Text.Json.Nodes;
using Tanque.Core.Dominio;
using Tanque.Core.Modulos;
using Xunit;

namespace Tanque.Core.Tests;

/// <summary>
/// Pruebas espejo de <see cref="MurosRectangulares"/> (PCA/Marcus, Caso 3, tope Free/fondo+lados
/// Fixed, carga triangular). Espejo de las secciones "Muros rectangulares PCA/Marcus" e
/// "interpolacion, rango tabulado y combinacion Marcus" de tools/Tanque.Core.Verificacion/Program.cs.
///
/// FUENTE DE VERDAD: los coeficientes verificados aquí (Cs, Cmx, Cmy, Cmxy) son los del MANUAL
/// PCA "Rectangular Concrete Tanks" (Caso 3), extraídos por doble lectura independiente y
/// almacenados en referencia_normativa/pca_manual/coeficientes_caso3_muro_y_caso10_placa.json (del
/// que se generaron programáticamente los arreglos de MurosRectangulares.cs). Las "Tablas 17/20/21"
/// de la tesis NO son la referencia numérica: son la transcripción de la tesis de esos mismos
/// coeficientes del manual, usadas aquí solo como un oráculo histórico de conveniencia (confirmado
/// por doble lectura). Donde la tesis diverge del manual (celda My[7,3] de su "Tabla 21"),
/// Tanque.Core sigue al MANUAL y la prueba lo declara explícitamente. El lookup se verifica con r
/// crudo (r=b/a), INDEPENDIENTE de la luz eje a eje (corrección de flexión 2026-08-31), por lo que
/// estas pruebas no cambian con dicha corrección.
/// </summary>
public class MurosRectangularesTests
{
    private static readonly JsonNode Caso = CasoOro.Cargar("ejercicio_1_tanque_lados_iguales.json").Resultado("analisis_muros")!;

    private static ProyectoTanque ProyectoEj1()
    {
        var caso = CasoOro.Cargar("ejercicio_1_tanque_lados_iguales.json");
        var g = new Geometria(
            BAnchoM: caso.Geo("B_ancho_m"), LLargoM: caso.Geo("L_largo_m"), HtAlturaM: caso.Geo("Ht_altura_m"),
            ConTapa: caso.GeoBool("con_tapa"), EmEspesorMuroM: caso.Geo("em_espesor_muro_m"),
            EfEspesorFondoM: caso.Geo("ef_espesor_fondo_m"), EtEspesorTapaM: 0.0,
            HLAlturaLiquidoM: caso.Geo("HL_altura_liquido_m"), HmAlturaSueloSobreMuroM: caso.Geo("Hm_altura_suelo_sobre_muro_m"),
            WextSobrecargaKNm2: caso.Geo("Wext_sobrecarga_kNm2"));
        var m = new Materiales(
            FcMPa: caso.Mat("fc_MPa"), FyMPa: caso.Mat("fy_MPa"), GammaSueloKNm3: caso.Mat("gamma_suelo_kNm3"),
            GammaConcretoKNm3: caso.Mat("gamma_concreto_kNm3"), GammaLiquidoKNm3: caso.Mat("gamma_liquido_kNm3"),
            PhiGradosAnguloFriccionSuelo: caso.Suelo("phi_grados"));
        return new ProyectoTanque(g, m);
    }

    [Fact]
    public void Ejercicio1_r1_5_CortanteCoincideConManualPCA_Caso3_AmbasCondiciones()
    {
        var proyecto = ProyectoEj1();
        var presiones = PresionesLaterales.Calcular(proyecto);
        var HL = proyecto.Geometria.HLAlturaLiquidoM;
        var Hm = proyecto.Geometria.HmAlturaSueloSobreMuroM;
        var L = proyecto.Geometria.LLargoM;

        // Condición 1 (interior/líquido).
        var q1 = MurosRectangulares.CalcularCargaMuroInterior(presiones.PhMaximaKNm2);
        AssertTol("q1 = 1.4 x Ph_maxima", q1, 41.202, 0.01);
        AssertTol("r condicion 1 (=L/HL)", L / HL, 1.5, 1e-9);
        var r1 = MurosRectangulares.Calcular(L / HL, L / HL, q1, HL, true);
        AssertTol("V fondo punto medio (cond 1)", r1.VBottomKNm, 49.44, 0.02);
        AssertTol("V lateral maximo (cond 1)", r1.VSideMaxKNm, 32.13, 0.02);
        AssertTol("V lateral punto medio (cond 1)", r1.VSideMidKNm, 32.13, 0.02);

        // Condición 2 (exterior/suelo): el Ka CORREGIDO (hallazgo 1) debe dar un valor distinto del
        // publicado por la tesis (23.44 kN, calculado con el Ka incorrecto 0.254366).
        var q2 = MurosRectangulares.CalcularCargaMuroExterior(presiones.Ps2MaximaKNm2);
        var r2 = MurosRectangulares.Calcular(L / Hm, L / Hm, q2, Hm, true);
        Assert.NotEqual(23.44, Math.Round(r2.VBottomKNm, 2));

        // Discriminador: recalculado con el Ka incorrecto, sí coincide con 23.44.
        var q2ConBug = 1.6 * 0.254366 * proyecto.Materiales.GammaSueloKNm3 * Hm;
        var r2ConBug = MurosRectangulares.Calcular(L / Hm, L / Hm, q2ConBug, Hm, true);
        AssertTol("V fondo cond 2 con Ka incorrecto (hallazgo 1)", r2ConBug.VBottomKNm, 23.44, 0.05);
    }

    [Fact]
    public void Ejercicio1_r1_5_Las66CeldasDeMomentosCoincidenConManualPCA_Caso3()
    {
        var proyecto = ProyectoEj1();
        var presiones = PresionesLaterales.Calcular(proyecto);
        var HL = proyecto.Geometria.HLAlturaLiquidoM;
        var L = proyecto.Geometria.LLargoM;

        var q1 = MurosRectangulares.CalcularCargaMuroInterior(presiones.PhMaximaKNm2);
        var resultado = MurosRectangulares.Calcular(L / HL, L / HL, q1, HL, true);
        var mom = Caso["momentos_kNm"]!;

        // Coeficientes del MANUAL PCA (Caso 3) re-expresados como momento: M = coef·q·a²/1000.
        // La tesis transcribe estos mismos coeficientes; su "Tabla 21" tiene UN error de
        // transcripción en [7,3] (publica 2.6 donde el manual da 2.967) -- ver excepción abajo.
        VerificarCampoMuro("Mx (manual PCA Caso 3)", resultado.CampoMx, mom["condicion_1_Mx_tabla20"]!, null);
        VerificarCampoMuro("My (manual PCA Caso 3)", resultado.CampoMy, mom["condicion_1_My_tabla21"]!, (7, 3));
    }

    private static void VerificarCampoMuro(string nombre, double[,] campo, JsonNode tablaJson, (int fila, int col)? excepcion)
    {
        var arr = tablaJson.AsArray();
        for (var fila = 0; fila < 11; fila++)
        {
            var filaJson = arr[fila]!.AsArray();
            for (var col = 0; col < 6; col++)
            {
                var esperado = filaJson[col]!.GetValue<double>();
                var actual = campo[fila, col];
                if (excepcion is { } ex && ex.fila == fila && ex.col == col)
                {
                    // Discrepancia aislada de TRANSCRIPCIÓN DE LA TESIS (su "Tabla 21" publica 2.6
                    // donde el manual PCA da 2.967); Tanque.Core sigue al manual.
                    Assert.True(Math.Abs(actual - esperado) > 0.02,
                        $"{nombre}[{fila},{col}] se esperaba divergencia documentada, pero coinciden ({actual} vs {esperado})");
                    continue;
                }
                Assert.True(Math.Abs(actual - esperado) <= 0.02,
                    $"{nombre}[{fila},{col}]: esperado={esperado}, actual={actual}, diff={actual - esperado:0.###}");
            }
        }
    }

    [Fact]
    public void Capitulo3_Rectangular_CoincideConTablaManual_Y_SimetriaDeEsquina()
    {
        // b/a=3.0, c/a=2.0 (combo tabulado del Capítulo 3, Caso 3, pág. 3-29). q=30 kN/m², a=3 m
        // → escala = q·a²/1000 = 0.27. Verifica el lookup (b/a,c/a) contra la tabla manual
        // CONFIRMADA (transcripción manual + OCR + simetría de esquina, sesión 2026-09-01).
        const double escala = 30.0 * 3.0 * 3.0 / 1000.0; // = 0.27
        var largo = MurosRectangulares.Calcular(3.0, 2.0, 30.0, 3.0, esLadoLargo: true);
        var corto = MurosRectangulares.Calcular(3.0, 2.0, 30.0, 3.0, esLadoLargo: false);

        // Mx base (BOT) del lado largo: 0,-38,-80,-109,-124,-129 (manual PCA Cap. 3 Caso 3, 3,2).
        var mxBase = new[] { 0.0, -38, -80, -109, -124, -129 };
        for (var c = 0; c < 6; c++) AssertTol($"Mx base[{c}]", largo.CampoMx[10, c], mxBase[c] * escala, 1e-9);

        // My TOP del lado largo: -55,-20,9,20,23,24 (signo verificado por píxeles: -55 y -20 son
        // negativos, contradiciendo el ancla previo erróneo +55,+20).
        var myTop = new[] { -55.0, -20, 9, 20, 23, 24 };
        for (var c = 0; c < 6; c++) AssertTol($"My top[{c}]", largo.CampoMy[0, c], myTop[c] * escala, 1e-9);

        // Simetría de esquina: My(largo,CORNER) == My(corto,CORNER) y Mxy(largo,CORNER) == Myz(corto,CORNER).
        for (var r = 0; r < 11; r++)
        {
            AssertTol($"My corner[{r}]", largo.CampoMy[r, 0], corto.CampoMy[r, 0], 1e-9);
            AssertTol($"torsor corner[{r}]", largo.CampoMxy[r, 0], corto.CampoMxy[r, 0], 1e-9);
        }

        // La migración SÍ cambia el momento para planta rectangular: Cap. 3 (3,2) ≠ Cap. 2 (r=3).
        var cap2 = MurosRectangulares.Calcular(3.0, 3.0, 30.0, 3.0, esLadoLargo: true);
        Assert.True(Math.Abs(cap2.CampoMx[10, 5] - largo.CampoMx[10, 5]) > 0.01,
            "Cap. 3 (3,2) y Cap. 2 (r=3) deben diferir en la base del muro largo");
    }

    [Fact]
    public void InterpolacionYRangoTabulado()
    {
        Assert.NotNull(MurosRectangulares.Calcular(MurosRectangulares.RMaximo, MurosRectangulares.RMaximo, 10.0, 3.0, true));
        Assert.NotNull(MurosRectangulares.Calcular(MurosRectangulares.RMinimo, MurosRectangulares.RMinimo, 10.0, 3.0, true));
        Assert.Throws<ArgumentOutOfRangeException>(() => MurosRectangulares.Calcular(4.5, 4.5, 10.0, 3.0, true));

        var r20 = MurosRectangulares.Calcular(2.0, 2.0, 1.0, 1.0, true);
        AssertTol("Cs bottom_edge_midpoint(r=2.0)", r20.VBottomKNm, 0.45, 1e-9);

        var rMedio = MurosRectangulares.Calcular(1.125, 1.125, 1.0, 1.0, true);
        AssertTol("Cs interpolado r=1.125", rMedio.VBottomKNm, 0.34, 1e-9);

        var rMedioRedondeado = MurosRectangulares.Calcular(1.125, 1.125, 1.0, 1.0, true, MetodoInterpolacion.RedondearSuperior);
        AssertTol("RedondearSuperior r=1.125", rMedioRedondeado.VBottomKNm, 0.36, 1e-9);
        Assert.NotEqual(rMedioRedondeado.VBottomKNm, rMedio.VBottomKNm);

        var exacto = MurosRectangulares.Calcular(2.0, 2.0, 1.0, 1.0, true, MetodoInterpolacion.RedondearSuperior);
        AssertTol("RedondearSuperior r=2.0 exacto", exacto.VBottomKNm, 0.45, 1e-9);
    }

    [Fact]
    public void CombinacionMarcus_CamposPosNoNegativos_NegNoPositivos_GobernanteEsMaximoReal()
    {
        var m = MurosRectangulares.Calcular(1.5, 1.5, 30.0, 3.0, true);
        var todosPosOk = true; var todosNegOk = true;
        double maxPosReal = double.NegativeInfinity, minNegReal = double.PositiveInfinity;
        for (var i = 0; i < 11; i++)
            for (var j = 0; j < 6; j++)
            {
                if (m.CampoMxPos[i, j] < 0) todosPosOk = false;
                if (m.CampoMxNeg[i, j] > 0) todosNegOk = false;
                maxPosReal = Math.Max(maxPosReal, m.CampoMxPos[i, j]);
                minNegReal = Math.Min(minNegReal, m.CampoMxNeg[i, j]);
            }
        Assert.True(todosPosOk, "CampoMxPos tiene celdas negativas");
        Assert.True(todosNegOk, "CampoMxNeg tiene celdas positivas");
        AssertTol("MxPosGobernante = maximo real de 66 celdas", m.MxPosGobernanteKNmM, maxPosReal, 1e-9);
        AssertTol("MxNegGobernante = magnitud del minimo real", m.MxNegGobernanteKNmM, -minNegReal, 1e-9);
    }

    private static void AssertTol(string nombre, double actual, double esperado, double atol)
        => Assert.True(Tolerancia.SonIguales(actual, esperado, atol, 0.0),
            Tolerancia.Diagnostico(nombre, actual, esperado, atol, 0.0));
}
