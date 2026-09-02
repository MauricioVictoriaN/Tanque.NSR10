// ---------------------------------------------------------------------------
// Tanque.NSR10
// (c) Mauricio Javier Victoria Niño <hidratecsa@gmail.com> · CC BY-NC-SA 4.0
// Uso exclusivamente académico. El motor de cálculo (Tanque.Core) se distribuye
// como binario ofuscado, protegido por derechos de autor (ver LICENSE).
// ---------------------------------------------------------------------------
using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Tanque.Core.Dominio;
using Tanque.Core.Modulos;

namespace Tanque.Reportes;

/// <summary>
/// Fase3 del frente de interfaz (2026-08-30): memoria de cálculo como HTML autocontenido (un solo
/// archivo con CSS inline, abrible en cualquier navegador e imprimible a PDF) -- cero dependencias
/// nuevas. Compone, en orden:
/// (1) encabezado con los datos del proyecto + banner de veredicto global (Fase1,
///     <see cref="Veredicto.Calcular"/> -- incluye la tabla de cada verificación con CUMPLE/NO CUMPLE);
/// (2) las mismas secciones verificadas del reporte de texto
///     (<see cref="ReporteResultados.GenerarSeccionesAgrupadas"/> con <c>incluirDiagramas:false</c>,
///     para no duplicar las grillas ASCII -- los diagramas van como SVG abajo);
/// (3) los mapas de calor de momento como SVG inline (Fase2, <see cref="DiagramaMomento.Calcular"/> +
///     <see cref="MapaDeColor"/>, celdas con valor numérico y leyenda);
/// (4) pie con las citas normativas (NSR-10 Título C / ACI350-06 / ACI350.3-06 / ACI318).
///
/// Principio rector: este archivo NO calcula ni inventa nada -- toda cifra proviene de los registros
/// ya verificados por el núcleo; solo da formato (presentación). El mapeo valor→color se comparte
/// con la pestaña "Diagramas" de la UI (<see cref="MapaDeColor"/>), de modo que UI y reporte usan
/// exactamente la misma escala.
/// </summary>
public static class ReporteHtml
{
    private static readonly CultureInfo Ci = CultureInfo.InvariantCulture;
    private static string N(double v, string formato = "0.###") => v.ToString(formato, Ci);

    /// <summary>Genera el HTML completo de la memoria de cálculo.</summary>
    public static string Generar(ResultadoCalculoTanque r, bool incluirSvg = true)
    {
        var g = r.Proyecto.Geometria;
        var m = r.Proyecto.Materiales;
        var veredicto = Veredicto.Calcular(r);
        var secciones = ReporteResultados.GenerarSeccionesAgrupadas(r, incluirDiagramas: false);
        var diagramas = DiagramaMomento.Calcular(r);

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"es\"><head><meta charset=\"utf-8\"/>");
        sb.AppendLine("<title>Memoria de cálculo — Tanque rectangular de concreto reforzado</title>");
        sb.AppendLine("<style>" + Css() + "</style>");
        sb.AppendLine("</head><body>");

        // Encabezado.
        sb.AppendLine("<header>");
        sb.AppendLine("<h1>Memoria de cálculo — Tanque rectangular de concreto reforzado</h1>");
        sb.AppendLine("<p class=\"meta\">Generado: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") +
            " · Referencia: NSR-10 Título C (C.23) / ACI350-06 / ACI350.3-06 / ACI318</p>");
        sb.AppendLine("<p class=\"meta\">Desarrollador: " + Esc(IdentidadDesarrollador.Firma) + "</p>");
        sb.AppendLine("<table class=\"datos\">");
        sb.AppendLine("<tr><th>Geometría</th><td>" + Esc("B = " + N(g.BAnchoM) + " m × L = " + N(g.LLargoM) +
            " m (dimensiones EXTERIORES; claros interiores B-2·em = " + N(g.BAnchoM - 2 * g.EmEspesorMuroM) + " m, L-2·em = " +
            N(g.LLargoM - 2 * g.EmEspesorMuroM) + " m; alturas desde la cara externa de la losa de fondo; altura interior del muro Ht-ef-et = " + N(g.HtAlturaM - g.EfEspesorFondoM - g.EtEspesorTapaM) + " m) · Ht = " + N(g.HtAlturaM) + " m · espesores: muro " + N(g.EmEspesorMuroM) + " m, fondo " +
            N(g.EfEspesorFondoM) + " m" + (g.ConTapa ? ", tapa " + N(g.EtEspesorTapaM) + " m" : "")) + "</td></tr>");
        sb.AppendLine("<tr><th>Materiales</th><td>" + Esc("f'c = " + N(m.FcMPa) + " MPa · fy = " + N(m.FyMPa) +
            " MPa · γc = " + N(m.GammaConcretoKNm3) + " kN/m³ · γl = " + N(m.GammaLiquidoKNm3) +
            " kN/m³ · γs = " + N(m.GammaSueloKNm3) + " kN/m³ · φ = " + N(m.PhiGradosAnguloFriccionSuelo) + "°" +
            (m.GammaSueloSaturadoKNm3 is double gss ? " · γs,sat = " + N(gss) + " kN/m³" : "")) + "</td></tr>");
        sb.AppendLine("<tr><th>Tipo de tanque</th><td>" + Esc(EtiquetaTipo(g.Tipo)) +
            (g.Tipo == TipoTanque.EnterradoConNivelFreatico && g.AlturaNivelFreaticoM is double nf
                ? Esc(" · nivel freático = " + N(nf) + " m") : "") + "</td></tr>");
        sb.AppendLine("<tr><th>Cargas</th><td>" + Esc("HL = " + N(g.HLAlturaLiquidoM) + " m · Hm = " +
            N(g.HmAlturaSueloSobreMuroM) + " m · Wext = " + N(g.WextSobrecargaKNm2) + " kN/m²") + "</td></tr>");
        sb.AppendLine("</table>");
        sb.AppendLine(VeredictoHtml(veredicto));
        sb.AppendLine("</header>");

        // Cuerpo: secciones verificadas del reporte de texto (sin grillas ASCII; los mapas de
        // calor SVG van en su propia sección al final).
        sb.AppendLine("<main>");
        foreach (var s in secciones)
        {
            sb.AppendLine("<section>");
            sb.AppendLine("<h2>" + Esc(s.Grupo) + "</h2>");
            sb.AppendLine("<pre>" + Esc(s.Texto.TrimEnd()) + "</pre>");
            sb.AppendLine("</section>");
        }

        // Diagramas de momento (SVG inline).
        if (incluirSvg)
        {
            sb.AppendLine("<section>");
            sb.AppendLine("<h2>Diagramas de momento (campo PCA/Marcus — mapa de calor)</h2>");
            sb.AppendLine("<p class=\"nota\"><strong>Cómo leer los mapas:</strong> cada mapa muestra el campo " +
                "de momentos de UNA cara (la del título), en kN·m/m por celda, del análisis elástico estático " +
                "PCA/Marcus ya verificado. Rojo (+) = sagging → tracción en esta cara (el acero va en esa " +
                "cara); azul (−) = hogging → tracción en la cara opuesta; blanco = momento ≈ 0. La celda con " +
                "borde oscuro es el momento gobernante que usa el diseño. Muro: filas = altura (tope libre → " +
                "base empotrada), columnas = semiluz (borde → centro 0.5·b). Placa: ambos ejes = semiluz " +
                "(borde → centro). El diseño de refuerzo del reporte considera además el incremento sísmico " +
                "cuando aplica.</p>");
            foreach (var grupo in diagramas.Campos.GroupBy(c => c.Elemento))
            {
                sb.AppendLine("<h3>" + Esc(grupo.Key) + "</h3>");
                foreach (var campo in grupo)
                {
                    var titulo = string.IsNullOrEmpty(campo.Condicion)
                        ? campo.Direccion + " · " + campo.Cara
                        : campo.Direccion + " · " + campo.Cara + " · " + campo.Condicion;
                    sb.AppendLine("<figure><figcaption>" + Esc(titulo) + "</figcaption>");
                    sb.AppendLine(SvgMapa(campo));
                    sb.AppendLine("</figure>");
                }
            }
            sb.AppendLine("</section>");
        }
        sb.AppendLine("</main>");

        // Pie con citas normativas.
        sb.AppendLine("<footer>");
        sb.AppendLine("<p>Verificación normativa: NSR-10 Título C (Capítulo C.23 — tanques) / ACI350-06 / " +
            "ACI350.3-06 (análisis sísmico hidrodinámico de Housner) / ACI318 (flexión, cortante y " +
            "fisuración). Métodos: placas PCA/Marcus (Caso10), muros PCA (Caso3), empuje de Rankine, " +
            "Mononobe-Okabe.</p>");
        sb.AppendLine("<p>Memoria generada por Tanque.Core — la norma es la única fuente de verdad; toda " +
            "cifra proviene de los módulos de cálculo verificados del núcleo.</p>");
        sb.AppendLine("<p>Desarrollador: " + Esc(IdentidadDesarrollador.Firma) + "</p>");
        sb.AppendLine("</footer>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    // Escapa texto para HTML (evita inyección y caracteres rotos).
    private static string Esc(string s) => s
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("\"", "&quot;");

    private static string EtiquetaTipo(TipoTanque t) => t switch
    {
        TipoTanque.Superficial => "Superficial",
        TipoTanque.EnterradoSinNivelFreatico => "Enterrado, sin nivel freático",
        TipoTanque.EnterradoConNivelFreatico => "Enterrado, con nivel freático",
        _ => t.ToString()
    };

    // Banner de veredicto + tabla completa de verificaciones (Fase1).
    private static string VeredictoHtml(ResultadoVeredicto v)
    {
        var sb = new StringBuilder();
        var cls = v.Cumple ? "cumple" : "nocumple";
        sb.AppendLine($"<div class=\"veredicto {cls}\">");
        sb.AppendLine(v.Cumple
            ? "<strong>CUMPLE</strong> — todas las verificaciones normativas satisfacen la norma."
            : $"<strong>NO CUMPLE</strong> — {v.Items.Count(i => !i.Cumple)} verificación(es) no satisfacen la norma.");
        sb.AppendLine("<table><tr><th>Elemento</th><th>Concepto</th><th>Resultado</th><th>Detalle</th></tr>");
        foreach (var it in v.Items)
        {
            var resultado = it.Cumple ? "<span class=\"ok\">CUMPLE</span>" : "<span class=\"fail\">NO CUMPLE</span>";
            sb.AppendLine($"<tr><td>{Esc(it.Elemento)}</td><td>{Esc(it.Concepto)}</td><td>{resultado}</td><td>{Esc(it.Detalle)}</td></tr>");
        }
        sb.AppendLine("</table></div>");
        return sb.ToString();
    }

    // Un campo completo como SVG: celdas coloreadas + valor numérico + etiquetas orientadas + leyenda.
    private static string SvgMapa(CampoMomento campo)
    {
        var vals = campo.Valores;
        int filas = vals.GetLength(0);
        int cols = vals.GetLength(1);
        if (filas == 0 || cols == 0) return "";

        // Escala simétrica anclada en cero (blanco = 0 SIEMPRE) + celda gobernante (máx |M|).
        var (min, max) = MapaDeColor.RangoSimetrico(vals);
        var (gf, gc, _) = OrientacionMapa.CeldaGobernante(vals);

        const double cw = 34, ch = 15, lblIzq = 48, lblSup = 16, leyenda = 22;
        double w = lblIzq + cols * cw;
        double h = lblSup + filas * ch + leyenda;

        var sb = new StringBuilder();
        sb.AppendLine($"<svg width=\"{w:0}\" height=\"{h:0}\" xmlns=\"http://www.w3.org/2000/svg\" font-family=\"Segoe UI, Arial, sans-serif\">");

        // Etiquetas de fila (posición en "a") y columna (posición en "b"), con el borde físico.
        for (var f = 0; f < filas; f++)
        {
            var etiqueta = OrientacionMapa.EtiquetaFila(campo, f, filas);
            sb.AppendLine($"<text x=\"{lblIzq - 4}\" y=\"{lblSup + f * ch + ch / 2 + 3}\" font-size=\"8\" text-anchor=\"end\" fill=\"#475569\">{Esc(etiqueta)}</text>");
        }
        for (var c = 0; c < cols; c++)
        {
            var etiqueta = OrientacionMapa.EtiquetaColumna(campo, c, cols);
            sb.AppendLine($"<text x=\"{lblIzq + c * cw + cw / 2}\" y=\"{lblSup - 4}\" font-size=\"8\" text-anchor=\"middle\" fill=\"#475569\">{Esc(etiqueta)}</text>");
        }

        // Celdas: la gobernante se resalta con borde oscuro y su valor en negrita/blanco.
        for (var f = 0; f < filas; f++)
            for (var c = 0; c < cols; c++)
            {
                double v = vals[f, c];
                var (r, g, b) = MapaDeColor.Color(v, min, max);
                bool gob = f == gf && c == gc;
                double x = lblIzq + c * cw, y = lblSup + f * ch;
                var stroke = gob ? "#111827" : "#e5e7eb";
                var ancho = gob ? "1.5" : "1";
                var peso = gob ? " font-weight=\"bold\"" : "";
                var fg = gob ? "#ffffff" : "#111827";
                sb.AppendLine($"<rect x=\"{x:0.#}\" y=\"{y:0.#}\" width=\"{cw}\" height=\"{ch}\" fill=\"rgb({r},{g},{b})\" stroke=\"{stroke}\" stroke-width=\"{ancho}\"/>");
                sb.AppendLine($"<text x=\"{x + cw / 2}\" y=\"{y + ch / 2 + 3}\" font-size=\"8\"{peso} text-anchor=\"middle\" fill=\"{fg}\">{Esc(N(v, "0.##"))}</text>");
            }

        // Leyenda (barra de gradiente) + etiquetas semánticas hogging / 0 / sagging.
        const int seg = 20;
        double lx = lblIzq, ly = lblSup + filas * ch + 4;
        double lw = cols * cw;
        for (var i = 0; i < seg; i++)
        {
            double v = min + (max - min) * i / (seg - 1);
            var (r, g, b) = MapaDeColor.Color(v, min, max);
            sb.AppendLine($"<rect x=\"{lx + i * (lw / seg):0.#}\" y=\"{ly}\" width=\"{lw / seg + 0.5:0.#}\" height=\"8\" fill=\"rgb({r},{g},{b})\"/>");
        }
        sb.AppendLine($"<text x=\"{lx}\" y=\"{ly + 16}\" font-size=\"8\" fill=\"#475569\">-{Esc(N(max, "0.#"))} (hogging)</text>");
        sb.AppendLine($"<text x=\"{lx + lw / 2}\" y=\"{ly + 16}\" font-size=\"8\" text-anchor=\"middle\" fill=\"#475569\">0</text>");
        sb.AppendLine($"<text x=\"{lx + lw}\" y=\"{ly + 16}\" font-size=\"8\" text-anchor=\"end\" fill=\"#475569\">+{Esc(N(max, "0.#"))} (sagging)</text>");
        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    // CSS autocontenido, amigable para impresión a PDF.
    private static string Css() => @"
body { font-family: 'Segoe UI', Arial, sans-serif; margin: 24px; color: #1f2937; }
h1 { color: #0f172a; border-bottom: 3px solid #1d4ed8; padding-bottom: 8px; font-size: 22px; }
h2 { color: #1e40af; border-bottom: 1px solid #bfdbfe; padding-bottom: 4px; margin-top: 28px; font-size: 17px; }
h3 { color: #374151; margin: 14px 0 4px; font-size: 14px; }
.meta { color: #475569; font-size: 12px; }
.nota { color: #475569; font-size: 12px; }
pre { font-family: Consolas, 'Courier New', monospace; font-size: 12px; background: #f8fafc; border: 1px solid #e2e8f0; padding: 12px; white-space: pre-wrap; }
table.datos { border-collapse: collapse; margin: 8px 0; }
table.datos th { text-align: left; color: #475569; padding-right: 12px; vertical-align: top; font-size: 12px; }
table.datos td { font-size: 12px; }
.veredicto { padding: 12px 16px; border-radius: 6px; margin: 16px 0; font-size: 13px; }
.veredicto.cumple { background: #dcfce7; border: 1px solid #16a34a; }
.veredicto.nocumple { background: #fee2e2; border: 1px solid #dc2626; }
.veredicto table { border-collapse: collapse; margin-top: 8px; width: 100%; }
.veredicto th, .veredicto td { border: 1px solid #d1d5db; padding: 4px 8px; font-size: 12px; text-align: left; }
.ok { color: #15803d; font-weight: 600; }
.fail { color: #b91c1c; font-weight: 600; }
figure { margin: 10px 0; page-break-inside: avoid; }
figcaption { font-size: 12px; font-weight: 600; color: #374151; margin-bottom: 4px; }
footer { margin-top: 32px; border-top: 1px solid #e2e8f0; padding-top: 8px; font-size: 11px; color: #6b7280; }
@media print { body { margin: 12mm; } section { page-break-inside: avoid; } h2 { page-break-after: avoid; } svg { page-break-inside: avoid; } }
";
}
