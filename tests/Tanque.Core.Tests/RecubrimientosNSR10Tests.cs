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
/// Pruebas espejo de <see cref="RecubrimientosNSR10"/> -- el cálculo de la profundidad efectiva "d"
/// directamente de la geometría real evaluada contra el recubrimiento normativo NSR-10 C.23-C.7.7.1
/// (Tabla C.23-C.7.7.1, la específica para estructuras ambientales, NO la genérica C.7.7).
/// Espejo de la verificación que ya existe en tools/Tanque.Core.Verificacion/Program.cs, trasladada
/// aquí a xUnit para que corra en el equipo del usuario con "dotnet test".
/// </summary>
public class RecubrimientosNSR10Tests
{
    [Fact]
    public void ConstantesNormativas_CoincidenConTablaC23_C7_7_1()
    {
        // 50mm para superficies FORMADAS expuestas a líquido/suelo/intemperie (diferente del 40mm
        // de la tabla genérica C.7.7 -- ver docstring de la clase).
        Assert.Equal(0.050, RecubrimientosNSR10.RecubrimientoFormadoM);
        // 75mm para concreto vaciado directamente contra el suelo en contacto permanente.
        Assert.Equal(0.075, RecubrimientosNSR10.RecubrimientoContraSueloM);
        // Diámetro supuesto por defecto para el predimensionamiento de "d".
        Assert.Equal(25.0, RecubrimientosNSR10.DiametroBarraSupuestoMm);
    }

    [Fact]
    public void CalcularDEfectivo_RestaRecubrimientoYMedioDiametro()
    {
        // d = espesor - recubrimiento - Ø/2. Con espesor 0.30m, recubrimiento formado 0.050m y
        // Ø=25mm (=0.025m), d = 0.30 - 0.050 - 0.0125 = 0.2375.
        var d = RecubrimientosNSR10.CalcularDEfectivo(0.30, RecubrimientosNSR10.RecubrimientoFormadoM);
        Assert.Equal(0.2375, d, 12);

        // Contra el suelo (75mm): d = 0.30 - 0.075 - 0.0125 = 0.2125.
        var dSuelo = RecubrimientosNSR10.CalcularDEfectivo(0.30, RecubrimientosNSR10.RecubrimientoContraSueloM);
        Assert.Equal(0.2125, dSuelo, 12);
    }

    [Fact]
    public void CalcularDEfectivo_DiametroDeBarraPersonalizado()
    {
        // Con Ø=12mm: d = 0.25 - 0.050 - 0.006 = 0.194.
        var d = RecubrimientosNSR10.CalcularDEfectivo(0.25, RecubrimientosNSR10.RecubrimientoFormadoM, 12.0);
        Assert.Equal(0.194, d, 12);
    }

    [Fact]
    public void CalcularDEfectivo_RechazaEntradasInvalidas()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RecubrimientosNSR10.CalcularDEfectivo(0.0, 0.050));
        Assert.Throws<ArgumentOutOfRangeException>(() => RecubrimientosNSR10.CalcularDEfectivo(-0.30, 0.050));
        Assert.Throws<ArgumentOutOfRangeException>(() => RecubrimientosNSR10.CalcularDEfectivo(0.30, 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => RecubrimientosNSR10.CalcularDEfectivo(0.30, 0.050, 0.0));
    }

    [Fact]
    public void CalcularDEfectivo_EspesorInsuficiente_LanzaConMensajeAutoexplicativo()
    {
        // Espesor 0.05m con recubrimiento 0.05m ya agota "d" (d = 0.05 - 0.05 - 0.0125 < 0).
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => RecubrimientosNSR10.CalcularDEfectivo(0.05, RecubrimientosNSR10.RecubrimientoFormadoM));
        Assert.Contains("insuficiente", ex.Message);
    }
}
