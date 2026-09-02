// ---------------------------------------------------------------------------
// Tanque.NSR10
// (c) Mauricio Javier Victoria Niño <hidratecsa@gmail.com> · CC BY-NC-SA 4.0
// Uso exclusivamente académico. El motor de cálculo (Tanque.Core) se distribuye
// como binario ofuscado, protegido por derechos de autor (ver LICENSE).
// ---------------------------------------------------------------------------
using System.Globalization;
using System.Text;
using Tanque.Core.Modulos;

namespace Tanque.Reportes;

/// <summary>
/// Fase4 del frente de interfaz (2026-08-31) -- exportación de grillas/resultados a CSV para
/// análisis en hojas de cálculo (ítem 2 del backlog Fase4). Produce UN único archivo CSV en
/// formato "largo" (tidy): una sola tabla con 12 columnas fijas donde cada fila es un dato
/// escalar ya etiquetado, listo para tablas dinámicas, filtros y funciones (MAX, MIN, SUMAR.SI,
/// etc.) en Excel/LibreOffice.
///
/// PRINCIPIO RECTOR -- igual que <see cref="ReporteHtml"/> y <see cref="ReporteResultados"/>:
/// este archivo NO calcula ni inventa NINGÚN valor; solo reutiliza los campos ya verificados de
/// los módulos del núcleo. Las grillas de momento provienen de
/// <see cref="DiagramaMomento.Calcular"/> (los mismos <see cref="CampoMomento"/> que dibuja la
/// pestaña "Diagramas" y el HTML -- el campo elástico estático PCA/Marcus, ver el caveat
/// documentado allí), y el detallado de barras proviene del catálogo único
/// <see cref="CatalogoBarras"/> vía los registros de diseño ya resueltos por
/// <see cref="DisenoMuros"/>/<see cref="DisenoPlacas"/>/<see cref="DisenoLosaFondoSubpresion"/>.
///
/// ESTRUCTURA -- columnas fijas (una sola tabla, formato largo):
/// <code>
/// Bloque | Elemento | Concepto | Detalle | Subdetalle | Fila | Columna | PosFila_m | PosCol_m | Valor | Unidad | Texto
/// </code>
/// - <c>Bloque</c>: agrupa las filas por naturaleza del dato: "Veredicto", "Momento" (grillas),
///   "Diseño" (refuerzo por dirección/cara), "Cortante", "Cargas", "Presiones", "Flotabilidad" y
///   "EspesorMínimo".
/// - <c>Valor</c>/<c>Unidad</c>: dato numérico con su unidad (formato invariante, punto decimal);
///   vacíos en las filas de texto. <c>Texto</c>: veredictos CUMPLE/NO CUMPLE, combinación
///   gobernante, cláusula normativa, etc.
/// - <c>Fila</c>/<c>Columna</c>: índices 0-based de la celda dentro de la grilla (solo "Momento").
/// - <c>PosFila_m</c>/<c>PosCol_m</c>: posición física en metros de esa celda a lo largo de la
///   luz de fila/columna del campo (solo "Momento"). Para muro Mx recorre la altura completa; el
///   resto recorre la semiluz correspondiente (ver <see cref="CampoMomento.LuzFilasM"/>/
///   <see cref="CampoMomento.LuzColumnasM"/>).
///
/// SALIDA DETERMINISTA -- sin fecha/hora ni nombre de proyecto (a diferencia de
/// <see cref="ReporteHtml"/>), y con fin de línea CRLF explícito (estándar CSV), de modo que el
/// mismo <see cref="ResultadoCalculoTanque"/> produce el MISMO archivo byte a byte en cualquier
/// plataforma -- verificable carácter a carácter en <c>tools/Tanque.Core.Verificacion</c>.
///
/// NOTA DE ALCANCE -- el modo sísmico no tiene campo distribuido (sus grillas son ceros por
/// diseño, ver <see cref="MurosRectangularesSismico"/>); las grillas exportadas son siempre el
/// campo elástico estático (misma convención que la pestaña "Diagramas"). El incremento sísmico
/// de momento SÍ queda reflejado en los valores gobernantes del bloque "Diseño" (Mu de muro con
/// su combinación gobernante), que es donde el diseño lo considera.
/// </summary>
public static class ExportadorCsv
{
    private static readonly CultureInfo Ci = CultureInfo.InvariantCulture;
    private const string Nl = "\r\n";

    private static readonly string[] Cabecera =
    {
        "Bloque", "Elemento", "Concepto", "Detalle", "Subdetalle",
        "Fila", "Columna", "PosFila_m", "PosCol_m", "Valor", "Unidad", "Texto"
    };

    /// <summary>Genera el CSV completo (una sola tabla en formato largo) de grillas y resultados.</summary>
    public static string Generar(ResultadoCalculoTanque r)
    {
        var filas = new List<string[]>();
        AgregarInfo(filas);
        AgregarVeredicto(filas, r);
        AgregarMomentos(filas, r);
        AgregarDiseno(filas, r);
        AgregarCortantes(filas, r);
        AgregarResumen(filas, r);
        return Serializar(filas);
    }

    // Bloque "Info": datos del desarrollador del programa. Se añade al inicio del CSV (inmediatamente
    // después de la cabecera) para que la autoría quede consignada también en la exportación de datos.
    // Información de presentación: no altera ningún bloque técnico (las cifras verificadas quedan
    // intactas) -- usa el mismo esquema de 12 columnas y el escapado RFC 4180 del resto del archivo.
    private static void AgregarInfo(List<string[]> filas)
    {
        F(filas, "Info", "Desarrollador", "Nombre", texto: IdentidadDesarrollador.Nombre);
        F(filas, "Info", "Desarrollador", "Afiliación", texto: IdentidadDesarrollador.Afiliacion);
        F(filas, "Info", "Desarrollador", "Contacto", texto: IdentidadDesarrollador.Contacto);
        F(filas, "Info", "Desarrollador", "ORCID", texto: IdentidadDesarrollador.Orcid);
    }

    // --------------------------------------------------------------------------------------------
    // Serialización CSV (RFC 4180): se cita el campo si contiene coma, comillas o salto de línea.
    // --------------------------------------------------------------------------------------------
    private static string Serializar(List<string[]> filas)
    {
        var sb = new StringBuilder();
        EscribirFila(sb, Cabecera);
        foreach (var f in filas) EscribirFila(sb, f);
        return sb.ToString();
    }

    private static void EscribirFila(StringBuilder sb, IReadOnlyList<string> celdas)
    {
        for (var i = 0; i < celdas.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(Escapar(celdas[i]));
        }
        sb.Append(Nl);
    }

    private static string Escapar(string campo)
    {
        if (campo.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0)
            return '"' + campo.Replace("\"", "\"\"") + '"';
        return campo;
    }

    // Formato fijo a 6 decimales (invariante): limpia el ruido de punto flotante (p. ej.
    // 0.6000000000000001 → 0.6) sin perder precisión de ingeniería -- el doble ya verificado se
    // redondea SOLO en la presentación, nunca se altera el valor calculado.
    private static string Num(double? v) => v is double d ? d.ToString("0.######", Ci) : "";
    private static string Idx(int? v) => v is int i ? i.ToString(Ci) : "";
    private static string SiNo(bool cumple) => cumple ? "CUMPLE" : "NO CUMPLE";
    private static string SiNoN(bool? cumple) => cumple is bool c ? SiNo(c) : "";

    /// <summary>Anexa una fila con los 12 campos; los no usados quedan vacíos.</summary>
    private static void F(List<string[]> filas, string bloque, string elemento, string concepto,
        string detalle = "", string subdetalle = "", int? fila = null, int? col = null,
        double? posFilaM = null, double? posColM = null, double? valor = null,
        string unidad = "", string texto = "")
    {
        filas.Add(new[]
        {
            bloque, elemento, concepto, detalle, subdetalle,
            Idx(fila), Idx(col), Num(posFilaM), Num(posColM),
            Num(valor), unidad, texto
        });
    }

    // --------------------------------------------------------------------------------------------
    // Bloque "Veredicto" -- Fase1: el banner global y cada chequeo normativo individual.
    // --------------------------------------------------------------------------------------------
    private static void AgregarVeredicto(List<string[]> filas, ResultadoCalculoTanque r)
    {
        var v = Veredicto.Calcular(r);
        F(filas, "Veredicto", "Estructura", "Veredicto global", texto: SiNo(v.Cumple));
        foreach (var it in v.Items)
            F(filas, "Veredicto", it.Elemento, it.Concepto, detalle: it.Detalle, texto: SiNo(it.Cumple));
    }

    // --------------------------------------------------------------------------------------------
    // Bloque "Momento" -- grillas completas de los campos PCA/Marcus (re-muestreados por
    // DiagramaMomento, sin fórmulas nuevas): una fila por celda, con su posición física.
    // --------------------------------------------------------------------------------------------
    private static void AgregarMomentos(List<string[]> filas, ResultadoCalculoTanque r)
    {
        var diagramas = DiagramaMomento.Calcular(r);
        foreach (var campo in diagramas.Campos)
        {
            var filasN = campo.Valores.GetLength(0);
            var colsN = campo.Valores.GetLength(1);
            for (var f = 0; f < filasN; f++)
            {
                var posF = filasN > 1 ? f / (double)(filasN - 1) * campo.LuzFilasM : 0.0;
                for (var c = 0; c < colsN; c++)
                {
                    var posC = colsN > 1 ? c / (double)(colsN - 1) * campo.LuzColumnasM : 0.0;
                    F(filas, "Momento", campo.Elemento, campo.Direccion,
                        detalle: campo.Cara, subdetalle: campo.Condicion,
                        fila: f, col: c, posFilaM: posF, posColM: posC,
                        valor: campo.Valores[f, c], unidad: "kN·m/m");
                }
            }
        }
    }

    // --------------------------------------------------------------------------------------------
    // Bloque "Diseño" -- refuerzo resuelto por dirección/cara (Mu, Ms, d, ρ, As, Ø, s, fs, Sd).
    // --------------------------------------------------------------------------------------------
    private static void AgregarDiseno(List<string[]> filas, ResultadoCalculoTanque r)
    {
        if (r.DisenoCubierta is { } cubierta)
            AgregarPlaca(filas, "Cubierta", cubierta);

        if (r.EnvolventeFondo is { } envolvente)
            AgregarEnvolvente(filas, envolvente);
        else
            AgregarPlaca(filas, "Fondo", r.DisenoFondo);

        AgregarMuro(filas, "Muro longitudinal", r.DisenoMuroLongitudinal);
        AgregarMuro(filas, "Muro transversal", r.DisenoMuroTransversal);
    }

    private static void AgregarPlaca(List<string[]> filas, string elemento, ResultadoDisenoPlaca p)
    {
        EmitirDiseno(filas, elemento, "Mx+", "", p.MxPositivo.MuKNm, p.MxPositivo.DEfectivoM,
            p.MxPositivo.Flexion.Rho, p.MxPositivo.Flexion.AsRequeridoMm2,
            p.MxPositivo.MsKNm, p.MxPositivo.Flexion.DiametroBarraMm, p.MxPositivo.Flexion.SeparacionM,
            p.MxPositivo.Flexion.DetalladoInsuficiente, p.MxPositivo.Flexion.DiametroSugeridoMm,
            p.MxPositivo.Fisuracion.FsMPa, p.MxPositivo.Fisuracion.FsAdmisibleMPa, p.MxPositivo.Fisuracion.Cumple,
            p.MxPositivo.Servicio.Sd);

        EmitirDiseno(filas, elemento, "Mx-", "", p.MxNegativo.MuKNm, p.MxNegativo.DEfectivoM,
            p.MxNegativo.Flexion.Rho, p.MxNegativo.Flexion.AsRequeridoMm2,
            p.MxNegativo.MsKNm, p.MxNegativo.Flexion.DiametroBarraMm, p.MxNegativo.Flexion.SeparacionM,
            p.MxNegativo.Flexion.DetalladoInsuficiente, p.MxNegativo.Flexion.DiametroSugeridoMm,
            p.MxNegativo.Fisuracion.FsMPa, p.MxNegativo.Fisuracion.FsAdmisibleMPa, p.MxNegativo.Fisuracion.Cumple,
            p.MxNegativo.Servicio.Sd);

        EmitirDiseno(filas, elemento, "My+", "", p.MyPositivo.MuKNm, p.MyPositivo.DEfectivoM,
            p.MyPositivo.Flexion.Rho, p.MyPositivo.Flexion.AsRequeridoMm2,
            p.MyPositivo.MsKNm, p.MyPositivo.Flexion.DiametroBarraMm, p.MyPositivo.Flexion.SeparacionM,
            p.MyPositivo.Flexion.DetalladoInsuficiente, p.MyPositivo.Flexion.DiametroSugeridoMm,
            p.MyPositivo.Fisuracion.FsMPa, p.MyPositivo.Fisuracion.FsAdmisibleMPa, p.MyPositivo.Fisuracion.Cumple,
            p.MyPositivo.Servicio.Sd);

        EmitirDiseno(filas, elemento, "My-", "", p.MyNegativo.MuKNm, p.MyNegativo.DEfectivoM,
            p.MyNegativo.Flexion.Rho, p.MyNegativo.Flexion.AsRequeridoMm2,
            p.MyNegativo.MsKNm, p.MyNegativo.Flexion.DiametroBarraMm, p.MyNegativo.Flexion.SeparacionM,
            p.MyNegativo.Flexion.DetalladoInsuficiente, p.MyNegativo.Flexion.DiametroSugeridoMm,
            p.MyNegativo.Fisuracion.FsMPa, p.MyNegativo.Fisuracion.FsAdmisibleMPa, p.MyNegativo.Fisuracion.Cumple,
            p.MyNegativo.Servicio.Sd);
    }

    private static void AgregarMuro(List<string[]> filas, string elemento, ResultadoDisenoMuro m)
    {
        EmitirDiseno(filas, elemento, "Vert.+", m.VerticalPositivo.ComboGobernante,
            m.VerticalPositivo.MuKNm, m.DEfectivoM,
            m.VerticalPositivo.Flexion.Rho, m.VerticalPositivo.Flexion.AsRequeridoMm2,
            m.VerticalPositivo.MsKNm, m.VerticalPositivo.Flexion.DiametroBarraMm, m.VerticalPositivo.Flexion.SeparacionM,
            m.VerticalPositivo.Flexion.DetalladoInsuficiente, m.VerticalPositivo.Flexion.DiametroSugeridoMm,
            m.VerticalPositivo.Fisuracion?.FsMPa, m.VerticalPositivo.Fisuracion?.FsAdmisibleMPa, m.VerticalPositivo.Fisuracion?.Cumple,
            m.VerticalPositivo.Servicio?.Sd);

        EmitirDiseno(filas, elemento, "Vert.-", m.VerticalNegativo.ComboGobernante,
            m.VerticalNegativo.MuKNm, m.DEfectivoM,
            m.VerticalNegativo.Flexion.Rho, m.VerticalNegativo.Flexion.AsRequeridoMm2,
            m.VerticalNegativo.MsKNm, m.VerticalNegativo.Flexion.DiametroBarraMm, m.VerticalNegativo.Flexion.SeparacionM,
            m.VerticalNegativo.Flexion.DetalladoInsuficiente, m.VerticalNegativo.Flexion.DiametroSugeridoMm,
            m.VerticalNegativo.Fisuracion?.FsMPa, m.VerticalNegativo.Fisuracion?.FsAdmisibleMPa, m.VerticalNegativo.Fisuracion?.Cumple,
            m.VerticalNegativo.Servicio?.Sd);

        EmitirDiseno(filas, elemento, "Horiz.+", m.HorizontalPositivo.ComboGobernante,
            m.HorizontalPositivo.MuKNm, m.DEfectivoM,
            m.HorizontalPositivo.Flexion.Rho, m.HorizontalPositivo.Flexion.AsRequeridoMm2,
            m.HorizontalPositivo.MsKNm, m.HorizontalPositivo.Flexion.DiametroBarraMm, m.HorizontalPositivo.Flexion.SeparacionM,
            m.HorizontalPositivo.Flexion.DetalladoInsuficiente, m.HorizontalPositivo.Flexion.DiametroSugeridoMm,
            m.HorizontalPositivo.Fisuracion?.FsMPa, m.HorizontalPositivo.Fisuracion?.FsAdmisibleMPa, m.HorizontalPositivo.Fisuracion?.Cumple,
            m.HorizontalPositivo.Servicio?.Sd);

        EmitirDiseno(filas, elemento, "Horiz.-", m.HorizontalNegativo.ComboGobernante,
            m.HorizontalNegativo.MuKNm, m.DEfectivoM,
            m.HorizontalNegativo.Flexion.Rho, m.HorizontalNegativo.Flexion.AsRequeridoMm2,
            m.HorizontalNegativo.MsKNm, m.HorizontalNegativo.Flexion.DiametroBarraMm, m.HorizontalNegativo.Flexion.SeparacionM,
            m.HorizontalNegativo.Flexion.DetalladoInsuficiente, m.HorizontalNegativo.Flexion.DiametroSugeridoMm,
            m.HorizontalNegativo.Fisuracion?.FsMPa, m.HorizontalNegativo.Fisuracion?.FsAdmisibleMPa, m.HorizontalNegativo.Fisuracion?.Cumple,
            m.HorizontalNegativo.Servicio?.Sd);
    }

    private static void AgregarEnvolvente(List<string[]> filas, ResultadoEnvolventePlacaFondo e)
    {
        EmitirCaraEnvolvente(filas, "Mx inf.", e.MxCaraInferior);
        EmitirCaraEnvolvente(filas, "Mx sup.", e.MxCaraSuperior);
        EmitirCaraEnvolvente(filas, "My inf.", e.MyCaraInferior);
        EmitirCaraEnvolvente(filas, "My sup.", e.MyCaraSuperior);
    }

    private static void EmitirCaraEnvolvente(List<string[]> filas, string detalle, ResultadoEnvolventeCaraPlacaFondo c)
    {
        // La envolvente del fondo no expone Sd (no hay revisión de servicio separada: el caso de
        // carga gobernante ya trae su propio diseño) -- por eso Sd queda vacío en estas filas.
        EmitirDiseno(filas, "Fondo", detalle, c.GobernaSubpresion ? "Subpresión" : "Gravitacional",
            c.MuKNm, c.DEfectivoM, c.Rho, c.AsRequeridoMm2,
            c.MsKNm, c.DiametroBarraMm, c.SeparacionM, c.DetalladoInsuficiente, c.DiametroSugeridoMm,
            c.FsMPa, c.FsAdmisibleMPa, c.FisuracionCumple, sd: null);
    }

    /// <summary>
    /// Emite, para una dirección/cara de diseño, el conjunto FIJO de conceptos escalares (una fila
    /// por concepto) -- mismo conjunto siempre, para que tablas dinámicas/filtros trabajen sobre
    /// columnas homogéneas. Los conceptos nulos (p. ej. Ms/fs bajo combinación gobernante sísmica)
    /// quedan con Valor vacío, nunca se omiten silenciosamente.
    /// </summary>
    private static void EmitirDiseno(List<string[]> filas, string elemento, string detalle, string subdetalle,
        double muKNm, double dM, double rho, double asReqMm2,
        double? msKNm, double? diametroMm, double? separacionM, bool detalladoInsuficiente, double? diametroSugeridoMm,
        double? fsMPa, double? fsAdmMPa, bool? fisuraCumple, double? sd)
    {
        Valor("Mu", muKNm, "kN·m/m");
        Valor("Ms", msKNm, "kN·m/m");
        Valor("d", dM, "m");
        Valor("Rho", rho, "");
        Valor("As", asReqMm2, "mm²/m");
        Valor("Diametro", diametroMm, "mm");
        Valor("Separacion", separacionM, "m");
        Valor("fs", fsMPa, "MPa");
        Valor("fs_adm", fsAdmMPa, "MPa");
        Valor("Sd", sd, "");
        Texto("Fisuración", SiNoN(fisuraCumple));
        Texto("Detallado", detalladoInsuficiente ? "NO CUMPLE" : "CUMPLE");
        Valor("DiametroSugerido", diametroSugeridoMm, "mm");

        void Valor(string concepto, double? v, string unidad)
            => F(filas, "Diseño", elemento, concepto, detalle: detalle, subdetalle: subdetalle, valor: v, unidad: unidad);

        void Texto(string concepto, string texto)
            => F(filas, "Diseño", elemento, concepto, detalle: detalle, subdetalle: subdetalle, texto: texto);
    }

    // --------------------------------------------------------------------------------------------
    // Bloque "Cortante" -- Vu/Vc/CUMPLE por ubicación (bordes de placa, fondo/laterales de muro).
    // --------------------------------------------------------------------------------------------
    private static void AgregarCortantes(List<string[]> filas, ResultadoCalculoTanque r)
    {
        if (r.DisenoCubierta is { } cubierta)
        {
            CortantePlaca(filas, "Cubierta", "Borde a", cubierta.CortanteX.VuKN, cubierta.CortanteX.Cortante.VcKN, cubierta.CortanteX.Cortante.Cumple);
            CortantePlaca(filas, "Cubierta", "Borde b", cubierta.CortanteY.VuKN, cubierta.CortanteY.Cortante.VcKN, cubierta.CortanteY.Cortante.Cumple);
        }

        if (r.EnvolventeFondo is { } envolvente)
        {
            CortanteEnvolvente(filas, envolvente.CortanteX, "Borde a");
            CortanteEnvolvente(filas, envolvente.CortanteY, "Borde b");
        }
        else
        {
            CortantePlaca(filas, "Fondo", "Borde a", r.DisenoFondo.CortanteX.VuKN, r.DisenoFondo.CortanteX.Cortante.VcKN, r.DisenoFondo.CortanteX.Cortante.Cumple);
            CortantePlaca(filas, "Fondo", "Borde b", r.DisenoFondo.CortanteY.VuKN, r.DisenoFondo.CortanteY.Cortante.VcKN, r.DisenoFondo.CortanteY.Cortante.Cumple);
        }

        CortanteMuro(filas, "Muro longitudinal", r.DisenoMuroLongitudinal);
        CortanteMuro(filas, "Muro transversal", r.DisenoMuroTransversal);
    }

    private static void CortantePlaca(List<string[]> filas, string elemento, string ubicacion, double vu, double vc, bool cumple)
    {
        F(filas, "Cortante", elemento, "Vu", detalle: ubicacion, valor: vu, unidad: "kN/m");
        F(filas, "Cortante", elemento, "Vc", detalle: ubicacion, valor: vc, unidad: "kN/m");
        F(filas, "Cortante", elemento, "Cumple", detalle: ubicacion, texto: SiNo(cumple));
    }

    private static void CortanteEnvolvente(List<string[]> filas, ResultadoEnvolventeCortantePlacaFondo c, string ubicacion)
    {
        var subdetalle = c.GobernaSubpresion ? "Subpresión" : "Gravitacional";
        F(filas, "Cortante", "Fondo", "Vu", detalle: ubicacion, subdetalle: subdetalle, valor: c.VuKN, unidad: "kN/m");
        F(filas, "Cortante", "Fondo", "Vc", detalle: ubicacion, subdetalle: subdetalle, valor: c.VcKN, unidad: "kN/m");
        F(filas, "Cortante", "Fondo", "Cumple", detalle: ubicacion, subdetalle: subdetalle, texto: SiNo(c.Cumple));
    }

    private static void CortanteMuro(List<string[]> filas, string elemento, ResultadoDisenoMuro m)
    {
        CortanteMuroUno(filas, elemento, "Fondo", m.CortanteFondo);
        CortanteMuroUno(filas, elemento, "Lateral máx.", m.CortanteLateralMaximo);
        CortanteMuroUno(filas, elemento, "Lateral medio", m.CortanteLateralMedio);
    }

    private static void CortanteMuroUno(List<string[]> filas, string elemento, string ubicacion, ResultadoDisenoCortanteMuro c)
    {
        F(filas, "Cortante", elemento, "Vu", detalle: ubicacion, subdetalle: c.ComboGobernante, valor: c.VuKN, unidad: "kN/m");
        F(filas, "Cortante", elemento, "Vc", detalle: ubicacion, subdetalle: c.ComboGobernante, valor: c.Cortante.VcKN, unidad: "kN/m");
        F(filas, "Cortante", elemento, "Cumple", detalle: ubicacion, subdetalle: c.ComboGobernante, texto: SiNo(c.Cortante.Cumple));
    }

    // --------------------------------------------------------------------------------------------
    // Bloque "Resumen" -- cargas, presiones, espesor mínimo y flotabilidad (escalares ya
    // verificados por cada módulo), para que el CSV sea una instantánea autocontenida.
    // --------------------------------------------------------------------------------------------
    private static void AgregarResumen(List<string[]> filas, ResultadoCalculoTanque r)
    {
        var c = r.Cargas;
        F(filas, "Cargas", "Muros", "Pm1", detalle: "par tipo B", valor: c.Pm1ParMurosTipoBKN, unidad: "kN");
        F(filas, "Cargas", "Muros", "Pm2", detalle: "par tipo L", valor: c.Pm2ParMurosTipoLKN, unidad: "kN");
        F(filas, "Cargas", "Cubierta", "Pt", valor: c.PtCubiertaKN, unidad: "kN");
        F(filas, "Cargas", "Fondo", "Pf", valor: c.PfFondoKN, unidad: "kN");
        F(filas, "Cargas", "Estructura", "Ptt", detalle: "peso total", valor: c.PttTotalKN, unidad: "kN");
        F(filas, "Cargas", "Cubierta", "W1", detalle: "uniforme (D)", valor: c.W1UniformeKNm2, unidad: "kN/m²");

        var pr = r.Presiones;
        F(filas, "Presiones", "Suelo", "Ka", valor: pr.Ka, unidad: "");
        F(filas, "Presiones", "Líquido", "Ph_máx", valor: pr.PhMaximaKNm2, unidad: "kN/m²");
        F(filas, "Presiones", "Suelo", "Ps2_máx", valor: pr.Ps2MaximaKNm2, unidad: "kN/m²");

        var em = r.EspesorMinimoMuro;
        F(filas, "EspesorMínimo", "Muros", "em_real", valor: em.EspesorRealM, unidad: "m");
        F(filas, "EspesorMínimo", "Muros", "em_mínimo", valor: em.EspesorMinimoAplicableM, unidad: "m");
        F(filas, "EspesorMínimo", "Muros", "Cumple", texto: SiNo(em.Cumple));
        F(filas, "EspesorMínimo", "Muros", "Cláusula", texto: em.ClausulaAplicada);

        if (r.Flotabilidad is { } fl)
        {
            F(filas, "Flotabilidad", "Estructura", "Peso_propio", valor: fl.PesoPropioKN, unidad: "kN");
            F(filas, "Flotabilidad", "Estructura", "Subpresión", valor: fl.SubpresionKN, unidad: "kN");
            F(filas, "Flotabilidad", "Estructura", "FS", valor: fl.FS, unidad: "");
            F(filas, "Flotabilidad", "Estructura", "Cumple", texto: SiNo(fl.Cumple));

            if (r.Sobreancho is { } sa)
            {
                F(filas, "Flotabilidad", "Estructura", "Sobreancho_requerido", valor: sa.SobreanchoRequeridoM, unidad: "m");
                F(filas, "Flotabilidad", "Estructura", "FS_con_sobreancho", valor: sa.FSConProyeccion, unidad: "");
            }
        }
    }
}
