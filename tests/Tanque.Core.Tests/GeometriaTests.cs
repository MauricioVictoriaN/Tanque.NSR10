// ---------------------------------------------------------------------------
// Tanque.NSR10
// (c) Mauricio Javier Victoria Niño <hidratecsa@gmail.com> · CC BY-NC-SA 4.0
// Uso exclusivamente académico. El motor de cálculo (Tanque.Core) se distribuye
// como binario ofuscado, protegido por derechos de autor (ver LICENSE).
// ---------------------------------------------------------------------------
using System;
using Tanque.Core.Dominio;
using Xunit;

namespace Tanque.Core.Tests;

/// <summary>
/// Pruebas espejo de la validación de dimensiones de <see cref="Geometria"/> (ítems "(g)/(h)" de
/// tools/Tanque.Core.Verificacion/Program.cs) -- B y L son dimensiones EXTERIORES en planta (cara
/// exterior a cara exterior de los muros; huella completa de la losa de fondo) y los claros
/// interiores B-2·em / L-2·em deben ser positivos. Confirmado por la docstring de
/// <see cref="Modulos.Flotabilidad"/> y por el peso neto de muros en
/// <see cref="Modulos.CargasGravitacionales"/> (corrección de esquinas B-2·em).
/// </summary>
public class GeometriaTests
{
    private static Geometria Base() => new(
        BAnchoM: 6.0, LLargoM: 8.0, HtAlturaM: 3.5, ConTapa: true,
        EmEspesorMuroM: 0.30, EfEspesorFondoM: 0.25, EtEspesorTapaM: 0.15,
        HLAlturaLiquidoM: 3.0, HmAlturaSueloSobreMuroM: 2.0, WextSobrecargaKNm2: 0.0);

    [Fact]
    public void DimensionesExteriores_ClarosInterioresPositivos()
    {
        var g = Base();
        g.Validar(); // no debe lanzar
        Assert.True(g.BAnchoM - 2 * g.EmEspesorMuroM > 0, "claro interior en B debe ser positivo");
        Assert.True(g.LLargoM - 2 * g.EmEspesorMuroM > 0, "claro interior en L debe ser positivo");
    }

    [Fact]
    public void AnchoExterior_ClaroInteriorNegativo_Lanza()
    {
        var g = Base() with { BAnchoM = 0.40 }; // B - 2·em = -0.20
        Assert.Throws<ArgumentException>(() => g.Validar());
    }

    [Fact]
    public void LargoExterior_ClaroInteriorNegativo_Lanza()
    {
        var g = Base() with { LLargoM = 0.40 }; // L - 2·em = -0.20
        Assert.Throws<ArgumentException>(() => g.Validar());
    }
}
