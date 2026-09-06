// ---------------------------------------------------------------------------
// Tanque.NSR10
// (c) Mauricio Javier Victoria Niño <hidratecsa@gmail.com> · CC BY-NC-SA 4.0
// Uso exclusivamente académico. El motor de cálculo (Tanque.Core) se distribuye
// como binario ofuscado, protegido por derechos de autor (ver LICENSE).
// ---------------------------------------------------------------------------
namespace Tanque.Reportes;

/// <summary>
/// Texto del aviso legal del programa: Descargo de Responsabilidad (Disclaimer) y Términos y
/// Condiciones de Uso (EULA). Se muestra en la ventana "Acerca de / Aviso legal" de la interfaz
/// (acceso permanente "Ayuda / Acerca de"). Información legal/informática de presentación: no
/// participa en ningún cálculo del núcleo ni altera las cifras verificadas.
/// </summary>
public static class DisclaimerAndEula
{
    /// <summary>Título del aviso.</summary>
    public const string Title =
        "AVISO DE SOFTWARE ACADÉMICO — EXCLUSIÓN DE GARANTÍAS Y LIBERACIÓN DE RESPONSABILIDAD";

    /// <summary>Cuerpo completo del aviso (Disclaimer + EULA).</summary>
    public const string Text =
        "1. Naturaleza Académica y de Investigación: Este software (en adelante, \"el Programa\") es un " +
        "prototipo computacional desarrollado exclusivamente con fines académicos, de investigación y como " +
        "complemento técnico de una publicación científica en formato de preprint (engrXiv). El Programa tiene " +
        "como objetivo demostrar la viabilidad de la automatización y verificación de métodos tabulados (como los " +
        "de la PCA) bajo las normas NSR-10 y ACI 350.3, pero no constituye un producto comercial terminado.\n\n" +
        "2. Suministro \"Tal Cual\" (As Is): El Programa, su código fuente, bases de datos incorporadas y suites de " +
        "verificación se proporcionan de forma gratuita, \"tal cual\", con todos sus defectos y según su " +
        "disponibilidad actual. El autor no otorga ninguna garantía, expresa o implícita, respecto a la exactitud " +
        "matemática absoluta, la ausencia de errores en las rutinas de interpolación bilineal, la exhaustividad de " +
        "los análisis sísmicos o geotécnicos, o su idoneidad para proyectos de ingeniería reales o comerciales.\n\n" +
        "3. Prohibición de Uso Comercial o Directo en Obra sin Validación Externa: El Programa está diseñado para su " +
        "uso en la docencia, la investigación y la auditoría de métodos de cálculo. Queda terminantemente prohibido " +
        "utilizar los resultados directos de este software para la construcción, licenciamiento o ejecución de obras " +
        "civiles sin que medie una verificación manual, independiente y exhaustiva por parte de un Ingeniero Civil " +
        "Estructural debidamente matriculado, quien asumirá la total responsabilidad civil, penal y profesional del " +
        "diseño.\n\n" +
        "4. Exclusión Total de Responsabilidad: En ningún caso el autor, los investigadores independientes, las " +
        "instituciones académicas asociadas o los revisores del manuscrito serán responsables ante el usuario o " +
        "terceras partes por cualquier reclamo, daño o perjuicio. Esto incluye, de forma enunciativa pero no " +
        "limitativa: colapsos o fallas estructurales, pérdidas materiales o económicas, lesiones personales, muerte, " +
        "o cualquier daño incidental o consecuente derivado del uso, mal uso, o la imposibilidad de usar este " +
        "Programa, incluso si se hubiera advertido formalmente de la posibilidad de tales fallos.\n\n" +
        "5. Licencia y Atribución: Este programa se distribuye en un repositorio público (descarga y consulta libres) " +
        "(a) el código accesible (interfaz, documentación, ejemplos y datos de prueba) se publica bajo CC BY-NC-SA 4.0 " +
        "(atribución, sin fines comerciales, compartir-igual); (b) el motor de cálculo Tanque.Core se distribuye como " +
        "binario compilado/ofuscado, protegido por derechos de autor, y queda prohibido su uso comercial, la ingeniería " +
        "inversa y la modificación (su fuente se entrega a revisores académicos previa solicitud); (c) el manuscrito se " +
        "publica bajo CC BY 4.0. Cualquier publicación, derivación documental o uso académico debe incluir la citación " +
        "bibliográfica formal del preprint correspondiente (engrXiv) y la autoría aquí indicada.";
}
