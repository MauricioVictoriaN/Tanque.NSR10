// ---------------------------------------------------------------------------
// Tanque.NSR10
// (c) Mauricio Javier Victoria Niño <hidratecsa@gmail.com> · CC BY-NC-SA 4.0
// Uso exclusivamente académico. El motor de cálculo (Tanque.Core) se distribuye
// como binario ofuscado, protegido por derechos de autor (ver LICENSE).
// ---------------------------------------------------------------------------
namespace Tanque.Reportes;

/// <summary>
/// Identidad del desarrollador/autor del programa Tanque.NSR10 (Tanque.Core).
/// Se muestra en la pestaña "Datos de entrada" de la interfaz y en el encabezado/pie de todos los
/// reportes generados por el programa: reporte de texto (<see cref="ReporteResultados"/>), memoria
/// HTML (<see cref="ReporteHtml"/>) y exportación CSV (<see cref="ExportadorCsv"/>, bloque "Info").
/// Es información de autoría/presentación: no participa en ningún cálculo del núcleo ni altera la
/// salida técnica de los módulos -- se añade solo al formatear, de modo que la verificación
/// normativa/las cifras verificadas quedan intactas.
/// </summary>
public static class IdentidadDesarrollador
{
    /// <summary>Nombre completo del desarrollador.</summary>
    public const string Nombre = "Mauricio Javier Victoria Niño";

    /// <summary>Afiliación (institución/rol y ciudad-país).</summary>
    public const string Afiliacion = "Independent Researcher, Cali, Colombia";

    /// <summary>Correo electrónico de contacto.</summary>
    public const string Contacto = "hidratecsa@gmail.com";

    /// <summary>Identificador ORCID.</summary>
    public const string Orcid = "0009-0003-4328-5691";

    /// <summary>Firma compacta de una línea (nombre · afiliación · contacto · ORCID).</summary>
    public static string Firma =>
        $"{Nombre} · {Afiliacion} · {Contacto} · ORCID: {Orcid}";
}
