// ---------------------------------------------------------------------------
// Tanque.NSR10
// (c) Mauricio Javier Victoria Niño <hidratecsa@gmail.com> · CC BY-NC-SA 4.0
// Uso exclusivamente académico. El motor de cálculo (Tanque.Core) se distribuye
// como binario ofuscado, protegido por derechos de autor (ver LICENSE).
// ---------------------------------------------------------------------------
using System.Text.Json.Nodes;
using Tanque.Core.Modulos;
using Xunit;

namespace Tanque.Core.Tests;

/// <summary>
/// Pruebas espejo de <see cref="PlacasRectangulares"/> (PCA/Marcus, Caso 10, 4 orillas Hinged).
/// Espejo fiel de las secciones "Placas rectangulares PCA/Marcus" y "MetodoInterpolacion" de
/// tools/Tanque.Core.Verificacion/Program.cs, trasladadas a xUnit para correr con "dotnet test".
///
/// FUENTE DE VERDAD: los coeficientes verificados aquí (Cs, Cmx, Cmy, Cmxy) son los del MANUAL
/// PCA (Caso 10), extraídos por doble lectura y almacenados en
/// referencia_normativa/pca_manual/coeficientes_caso3_muro_y_caso10_placa.json. Las "Tablas 26-29"
/// de la tesis NO son la referencia numérica: son la transcripción de la tesis de esos mismos
/// coeficientes del manual, usadas solo como oráculo histórico (confirmado por doble lectura).
/// Donde la tesis diverge del manual (celda Cmx[5,3] en r=1.0), Tanque.Core sigue al MANUAL. El
/// lookup se verifica con r crudo, INDEPENDIENTE de la luz eje a eje (corrección 2026-08-31).
/// Cubre: coincidencia celda a celda contra el manual PCA, interpolación/rango tabulado, el
/// discriminador del hallazgo 12 (carga de cubierta) y el método RedondearSuperior del backlog v2.
/// </summary>
public class PlacasRectangularesTests
{
    [Fact]
    public void Ejercicio1_PlacaFondo_r1_CoeficientesDeCortanteCoincidenConManual()
    {
        var caso = CasoOro.Cargar("ejercicio_1_tanque_lados_iguales.json");
        var apf = caso.Resultado("analisis_placa_fondo")!;
        var subpresion = apf["revision_flotacion"]!["subpresion_kNm2"]!.GetValue<double>();
        var aM = 4.5; // B=L=4.5 en este ejercicio -> r=1.0 exacto

        var resultado = PlacasRectangulares.Calcular(r: 1.0, qKNm2: subpresion, aM: aM);

        AssertTol("Cs bottom_edge_midpoint (r=1.0, vía Vx)", resultado.VxKNm / (subpresion * aM), 0.34, 0.001);
        AssertTol("Cs side_edge_maximum (r=1.0, vía Vy)", resultado.VyKNm / (subpresion * aM), 0.34, 0.001);
    }

    [Fact]
    public void Ejercicio1_PlacaFondo_r1_Las144CeldasDeMomentosCoincidenConManualPCA_Caso10()
    {
        var caso = CasoOro.Cargar("ejercicio_1_tanque_lados_iguales.json");
        var apf = caso.Resultado("analisis_placa_fondo")!;
        var subpresion = apf["revision_flotacion"]!["subpresion_kNm2"]!.GetValue<double>();
        var momentos = apf["momentos_kNm"]!;

        var resultado = PlacasRectangulares.Calcular(r: 1.0, qKNm2: subpresion, aM: 4.5);

        // Coeficientes del MANUAL PCA (Caso 10) re-expresados como momento: M = coef·q·a²/1000.
        // La tesis transcribe estos mismos coeficientes; la Única celda divergente de las 144
        // (36×4) es Cmx[5,3](r=1.0): el bytecode original fija 37 y el manual PCA publica 38;
        // Tanque.Core usa 38 (manual). Divergencia esperada y documentada.
        var excepcion = (fila: 5, col: 3);

        VerificarCampo("Mx_pos (manual PCA Caso 10)", resultado.CampoMxPos, momentos["positivo_X_tabla26"]!, excepcion);
        VerificarCampo("My_pos (manual PCA Caso 10)", resultado.CampoMyPos, momentos["positivo_Y_tabla27"]!, null);
        VerificarCampo("Mx_neg (manual PCA Caso 10)", resultado.CampoMxNeg, momentos["negativo_X_tabla28"]!, null);
        VerificarCampo("My_neg (manual PCA Caso 10)", resultado.CampoMyNeg, momentos["negativo_Y_tabla29"]!, null);
    }

    private static void VerificarCampo(string nombre, double[,] campo, JsonNode tablaJson, (int fila, int col)? excepcion)
    {
        var arr = tablaJson.AsArray();
        for (var fila = 0; fila < 6; fila++)
        {
            var filaJson = arr[fila]!.AsArray();
            for (var col = 0; col < 6; col++)
            {
                var esperado = filaJson[col]!.GetValue<int>();
                var actual = (int)Math.Round(campo[fila, col], MidpointRounding.AwayFromZero);
                if (excepcion is { } ex && ex.fila == fila && ex.col == col)
                {
                    // Divergencia esperada y documentada (anomalía Cmx[5,3] r=1.0) -- no cuenta como fallo.
                    Assert.NotEqual(esperado, actual);
                    continue;
                }
                Assert.True(actual == esperado,
                    $"{nombre}[{fila},{col}]: esperado={esperado}, actual={actual} (celda={campo[fila, col]:0.###})");
            }
        }
    }

    [Fact]
    public void InterpolacionYRangoTabulado()
    {
        // Extremos tabulados no lanzan.
        Assert.NotNull(PlacasRectangulares.Calcular(PlacasRectangulares.RMaximo, 10.0, 3.0));
        Assert.NotNull(PlacasRectangulares.Calcular(PlacasRectangulares.RMinimo, 10.0, 3.0));

        // Fuera de rango lanza (no extrapola sin respaldo normativo).
        Assert.Throws<ArgumentOutOfRangeException>(() => PlacasRectangulares.Calcular(4.5, 10.0, 3.0));

        // Interpolación exacta en un punto tabulado (r=1.5 -> Cs bottom_edge_midpoint=0.42).
        var r15 = PlacasRectangulares.Calcular(1.5, 1.0, 1.0);
        AssertTol("Cs bottom_edge_midpoint(r=1.5)", r15.VxKNm, 0.42, 1e-9);

        // Punto medio interpolado entre r=1.0 (0.34) y r=1.25 (0.39): en r=1.125 -> 0.365.
        var rMedio = PlacasRectangulares.Calcular(1.125, 1.0, 1.0);
        AssertTol("Cs interpolado en r=1.125", rMedio.VxKNm, 0.365, 1e-9);
    }

    [Fact]
    public void CargaDisenoCubierta_Hallazgo12_Corregido()
    {
        // D, CV, CG elegidos para que C3 sea gobernante: distingue el valor corregido (1.2*D) del
        // bug del original (literal Decimal "1.2D" = constante 1.2 sin multiplicar por D).
        var d = 10.0; var cv = 0.1; var cg = 3.0;
        var u = PlacasRectangulares.CalcularCargaDisenoCubierta(d, cv, cg);
        var c1 = 1.4 * d; var c2 = 1.2 * d + 1.6 * cv; var c3 = 1.2 * d + cv + cg;
        AssertTol("U cubierta = max(1.4D, 1.2D+1.6CV, 1.2D+CV+CG)", u, Math.Max(c1, Math.Max(c2, c3)), 1e-9);
        Assert.NotEqual(Math.Max(c1, Math.Max(c2, 1.2 + cv + cg)), u);
    }

    [Fact]
    public void RedondearSuperior_UsaTabuladoSuperior_NoInterpola()
    {
        var redondeado = PlacasRectangulares.Calcular(1.125, 1.0, 1.0, MetodoInterpolacion.RedondearSuperior);
        AssertTol("RedondearSuperior r=1.125 usa r=1.25 (0.39)", redondeado.VxKNm, 0.39, 1e-9);

        var interpolado = PlacasRectangulares.Calcular(1.125, 1.0, 1.0, MetodoInterpolacion.Interpolar);
        Assert.NotEqual(redondeado.VxKNm, interpolado.VxKNm);

        // Coincidencia exacta con tabulado: ambos métodos concuerdan.
        var exacto = PlacasRectangulares.Calcular(1.25, 1.0, 1.0, MetodoInterpolacion.RedondearSuperior);
        AssertTol("RedondearSuperior r=1.25 exacto", exacto.VxKNm, 0.39, 1e-9);
    }

    private static void AssertTol(string nombre, double actual, double esperado, double atol)
        => Assert.True(Tolerancia.SonIguales(actual, esperado, atol, 0.0),
            Tolerancia.Diagnostico(nombre, actual, esperado, atol, 0.0));
}
