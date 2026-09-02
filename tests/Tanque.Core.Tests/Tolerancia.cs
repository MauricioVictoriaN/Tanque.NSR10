// ---------------------------------------------------------------------------
// Tanque.NSR10
// (c) Mauricio Javier Victoria Niño <hidratecsa@gmail.com> · CC BY-NC-SA 4.0
// Uso exclusivamente académico. El motor de cálculo (Tanque.Core) se distribuye
// como binario ofuscado, protegido por derechos de autor (ver LICENSE).
// ---------------------------------------------------------------------------
namespace Tanque.Core.Tests;

/// <summary>
/// Política de tolerancia para comparar resultados de Tanque.Core contra los casos de prueba
/// de referencia (banco de pruebas en casos_prueba/, valores transcritos de la tesis fuente del
/// programa original). Ver RUTA_TRABAJO_PROXIMAS_SESIONES.md, pendiente "Definir la
/// política de tolerancia de pruebas".
///
/// Contexto: los resultados publicados en la tesis están redondeados -- casi siempre a 2
/// decimales, en ocasiones a enteros para magnitudes pequeñas (ver notas en
/// analisis_placa_fondo.momentos_kNm del banco de pruebas). Una tolerancia puramente absoluta
/// sería demasiado laxa para valores grandes (cientos de kN) y una puramente relativa sería
/// demasiado estricta para valores cercanos a cero. Se usa la fórmula estándar de "isclose":
///
///     |actual - esperado| &lt;= toleranciaAbsoluta + toleranciaRelativa * |esperado|
///
/// con toleranciaAbsoluta = 0.01 (coincide con el redondeo a 2 decimales de la mayoría de las
/// tablas de la tesis -- una diferencia de hasta una unidad en el último decimal publicado no
/// debe hacer fallar una prueba) y toleranciaRelativa = 0.001 (0.1%, para permitir el redondeo
/// acumulado de cálculos encadenados de varios pasos -- p.ej. Ka redondeado alimentando Ps2,
/// que a su vez alimenta una combinación de carga -- sin ocultar un error real, que típicamente
/// se manifiesta como una discrepancia de varios puntos porcentuales o más, no de fracciones de
/// punto porcentual).
/// </summary>
public static class Tolerancia
{
    public const double AbsolutaPorDefecto = 0.01;
    public const double RelativaPorDefecto = 0.001;

    public static bool SonIguales(double actual, double esperado,
        double toleranciaAbsoluta = AbsolutaPorDefecto,
        double toleranciaRelativa = RelativaPorDefecto)
    {
        var limite = toleranciaAbsoluta + toleranciaRelativa * Math.Abs(esperado);
        return Math.Abs(actual - esperado) <= limite;
    }

    /// <summary>Mensaje de diagnóstico legible para usar en aserciones fallidas.</summary>
    public static string Diagnostico(string nombre, double actual, double esperado,
        double toleranciaAbsoluta = AbsolutaPorDefecto,
        double toleranciaRelativa = RelativaPorDefecto)
    {
        var limite = toleranciaAbsoluta + toleranciaRelativa * Math.Abs(esperado);
        var diff = actual - esperado;
        return $"{nombre}: esperado={esperado}, actual={actual}, diferencia={diff:+0.######;-0.######}, " +
               $"límite=±{limite:0.######}";
    }
}
