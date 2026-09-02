// ---------------------------------------------------------------------------
// Tanque.NSR10
// (c) Mauricio Javier Victoria Niño <hidratecsa@gmail.com> · CC BY-NC-SA 4.0
// Uso exclusivamente académico. El motor de cálculo (Tanque.Core) se distribuye
// como binario ofuscado, protegido por derechos de autor (ver LICENSE).
// ---------------------------------------------------------------------------
using Tanque.Core.Modulos;
using Xunit;

namespace Tanque.Core.Tests;

/// <summary>
/// Pruebas espejo de las secciones "Placas rectangulares -- MetodoInterpolacion.RedondearSuperior"
/// y "Muros rectangulares -- interpolacion..." (bloque RedondearSuperior) de
/// tools/Tanque.Core.Verificacion/Program.cs -- mismos datos y valores esperados. Ver el docstring
/// de Modulos/MetodoInterpolacion.cs para el detalle de alcance (backlog v2). Cubre también
/// MurosRectangularesSismico (Capítulo 3, grilla bilineal escalonada), extendido a
/// RedondearSuperior en 2026-08-30.
/// </summary>
public class MetodoInterpolacionTests
{
    [Fact]
    public void Placas_RedondearSuperior_EnPuntoNoTabulado_UsaElTabuladoSuperior()
    {
        // r=1.125 entre 1.00 (Cs=0.34) y 1.25 (Cs=0.39) -- RedondearSuperior debe usar 0.39.
        var r = PlacasRectangulares.Calcular(1.125, 1.0, 1.0, MetodoInterpolacion.RedondearSuperior);
        Assert.Equal(0.39, r.VxKNm, precision: 9);
    }

    [Fact]
    public void Placas_RedondearSuperior_DifiereDeInterpolar_EnPuntoNoTabulado()
    {
        var redondeado = PlacasRectangulares.Calcular(1.125, 1.0, 1.0, MetodoInterpolacion.RedondearSuperior);
        var interpolado = PlacasRectangulares.Calcular(1.125, 1.0, 1.0, MetodoInterpolacion.Interpolar);
        Assert.NotEqual(redondeado.VxKNm, interpolado.VxKNm, precision: 6);
    }

    [Fact]
    public void Placas_RedondearSuperior_EnValorTabuladoExacto_CoincideConInterpolar()
    {
        var redondeado = PlacasRectangulares.Calcular(1.25, 1.0, 1.0, MetodoInterpolacion.RedondearSuperior);
        var interpolado = PlacasRectangulares.Calcular(1.25, 1.0, 1.0, MetodoInterpolacion.Interpolar);
        Assert.Equal(interpolado.VxKNm, redondeado.VxKNm, precision: 9);
        Assert.Equal(0.39, redondeado.VxKNm, precision: 9);
    }

    [Fact]
    public void Muros_RedondearSuperior_EnPuntoNoTabulado_UsaElTabuladoSuperior()
    {
        // r=1.125 entre 1.00 (Cs=0.32) y 1.25 (Cs=0.36) -- RedondearSuperior debe usar 0.36.
        var r = MurosRectangulares.Calcular(1.125, 1.125, 1.0, 1.0, true, MetodoInterpolacion.RedondearSuperior);
        Assert.Equal(0.36, r.VBottomKNm, precision: 9);
    }

    [Fact]
    public void Muros_RedondearSuperior_EnValorTabuladoExacto_CoincideConInterpolar()
    {
        var redondeado = MurosRectangulares.Calcular(2.0, 2.0, 1.0, 1.0, true, MetodoInterpolacion.RedondearSuperior);
        Assert.Equal(0.45, redondeado.VBottomKNm, precision: 9);
    }

    [Fact]
    public void Interpolar_EsElValorPorDefecto_LlamadoresExistentesNoCambianDeComportamiento()
    {
        // El valor por defecto de MetodoInterpolacion en Calcular(...) debe seguir siendo
        // Interpolar -- si esto cambiara, todo el codigo existente que no pasa el parametro
        // explicitamente cambiaria de comportamiento silenciosamente.
        var sinParametro = PlacasRectangulares.Calcular(1.125, 1.0, 1.0);
        var conInterpolarExplicito = PlacasRectangulares.Calcular(1.125, 1.0, 1.0, MetodoInterpolacion.Interpolar);
        Assert.Equal(conInterpolarExplicito.VxKNm, sinParametro.VxKNm, precision: 9);
    }

    [Fact]
    public void MurosSismico_RedondearSuperior_DifiereDeInterpolar_EnPuntoNoTabulado()
    {
        // b/a=1.25 no es tabulado (1.0 y 1.5 sí); c/a=0.5 sí lo es en ambas filas. RedondearSuperior
        // debe usar la fila de b/a=1.5 (redondeo superior), NO interpolar entre 1.0 y 1.5.
        var redondeado = MurosRectangularesSismico.Calcular(1.25, 0.5, 1.0, 1.0, MetodoInterpolacion.RedondearSuperior);
        var interpolado = MurosRectangularesSismico.Calcular(1.25, 0.5, 1.0, 1.0, MetodoInterpolacion.Interpolar);
        Assert.NotEqual(interpolado.LadoLargo.MxPosGobernanteKNmM, redondeado.LadoLargo.MxPosGobernanteKNmM, precision: 6);
    }

    [Fact]
    public void MurosSismico_RedondearSuperior_EnPuntoTabuladoExacto_CoincideConInterpolar()
    {
        var redondeado = MurosRectangularesSismico.Calcular(2.0, 0.5, 1.0, 1.0, MetodoInterpolacion.RedondearSuperior);
        var interpolado = MurosRectangularesSismico.Calcular(2.0, 0.5, 1.0, 1.0, MetodoInterpolacion.Interpolar);
        Assert.Equal(interpolado.LadoLargo.MxPosGobernanteKNmM, redondeado.LadoLargo.MxPosGobernanteKNmM, precision: 9);
    }
}
