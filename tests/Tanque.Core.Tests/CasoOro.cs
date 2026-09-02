// ---------------------------------------------------------------------------
// Tanque.NSR10
// (c) Mauricio Javier Victoria Niño <hidratecsa@gmail.com> · CC BY-NC-SA 4.0
// Uso exclusivamente académico. El motor de cálculo (Tanque.Core) se distribuye
// como binario ofuscado, protegido por derechos de autor (ver LICENSE).
// ---------------------------------------------------------------------------
using System.Text.Json.Nodes;

namespace Tanque.Core.Tests;

/// <summary>
/// Carga un caso de prueba de referencia (golden file) de casos_prueba/*.json.
///
/// Se usa System.Text.Json.Nodes (acceso dinámico) en vez de DTOs fuertemente tipados a
/// propósito: cada JSON contiene el oráculo completo de un ejercicio (geometría, cargas,
/// presiones, sismo, análisis de muros y placas, diseño final -- ver casos_prueba/README.md),
/// pero Tanque.Core todavía solo implementa el módulo de cargas gravitacionales (Fase 1,
/// arranque de scaffold). Definir DTOs completos ahora obligaría a mantenerlos sincronizados
/// con cada módulo nuevo antes de que exista código que los consuma. Cuando se implemente cada
/// módulo adicional, el patrón recomendado es añadir un método de acceso tipado aquí (como
/// <see cref="PesoTanqueKN"/>) en vez de tipar el árbol completo de una vez.
/// </summary>
public sealed class CasoOro
{
    private readonly JsonNode _root;

    public string Id { get; }

    private CasoOro(JsonNode root, string id)
    {
        _root = root;
        Id = id;
    }

    public static CasoOro Cargar(string rutaRelativaCasosPrueba)
    {
        var baseDir = AppContext.BaseDirectory;
        var ruta = Path.Combine(baseDir, "casos_prueba", rutaRelativaCasosPrueba);
        if (!File.Exists(ruta))
            throw new FileNotFoundException(
                $"No se encontró el caso de prueba '{rutaRelativaCasosPrueba}' en '{ruta}'. " +
                "Verifique que el .csproj copia casos_prueba/*.json al directorio de salida.", ruta);

        var texto = File.ReadAllText(ruta);
        var root = JsonNode.Parse(texto) ?? throw new InvalidDataException($"JSON vacío o inválido: {ruta}");
        var id = root["id"]?.GetValue<string>() ?? rutaRelativaCasosPrueba;
        return new CasoOro(root, id);
    }

    private JsonNode Entradas => _root["entradas"] ?? throw new InvalidDataException($"[{Id}] falta 'entradas'");
    private JsonNode ResultadosEsperados => _root["resultados_esperados"] ?? throw new InvalidDataException($"[{Id}] falta 'resultados_esperados'");

    public double Geo(string campo) => Entradas["geometria"]![campo]!.GetValue<double>();
    public bool GeoBool(string campo) => Entradas["geometria"]![campo]!.GetValue<bool>();
    public double Mat(string campo) => Entradas["materiales"]![campo]!.GetValue<double>();
    public double Suelo(string campo) => Entradas["suelo"]![campo]!.GetValue<double>();
    public double Sismo(string campo) => Entradas["sismo"]![campo]!.GetValue<double>();

    /// <summary>Acceso genérico a cualquier sección de resultados_esperados por nombre, o null si no existe.</summary>
    public JsonNode? Resultado(string nombreSeccion) => ResultadosEsperados[nombreSeccion];

    /// <summary>Sección resultados_esperados.presiones_diseno, o null si el caso no la contiene.</summary>
    public JsonNode? PresionesDiseno => ResultadosEsperados["presiones_diseno"];

    /// <summary>
    /// Sección resultados_esperados.peso_tanque_kN, o null si el caso no la contiene.
    /// Nota: en ejercicio_1_tanque_lados_iguales.json esta sección fue corregida en esta sesión
    /// -- el valor original estaba transcrito de la Tabla 14 de la tesis sin verificación
    /// independiente y no coincidía con la fórmula confirmada por IL. Ver el campo
    /// "_correccion_esta_sesion" dentro del propio JSON y RUTA_TRABAJO_PROXIMAS_SESIONES.md.
    /// </summary>
    public JsonNode? PesoTanqueKN => ResultadosEsperados["peso_tanque_kN"];
}
