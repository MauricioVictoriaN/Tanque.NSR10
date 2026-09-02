// ---------------------------------------------------------------------------
// Tanque.NSR10
// (c) Mauricio Javier Victoria Niño <hidratecsa@gmail.com> · CC BY-NC-SA 4.0
// Uso exclusivamente académico. El motor de cálculo (Tanque.Core) se distribuye
// como binario ofuscado, protegido por derechos de autor (ver LICENSE).
// ---------------------------------------------------------------------------
using System;
using System.Globalization;
using System.Text;
using Tanque.Core.Dominio;
using Tanque.Core.Modulos;

namespace Tanque.Reportes;

/// <summary>
/// Formatea los resultados de los módulos de Tanque.Core como texto plano de ancho fijo. Por
/// defecto solo reporta los valores gobernantes de placas/muros, no las grillas completas (36/66
/// celdas) -- consistente con el alcance "sin pulido visual" de la primera versión de la interfaz.
/// Backlog v2, interfaz/reportes (2026-08-27): las grillas completas SÍ están disponibles bajo
/// demanda (parámetro <c>incluirDiagramas</c> de <see cref="Placa"/>/<see cref="Muro"/>), como una
/// tabla de texto de ancho fijo -- ver <see cref="FormatGrilla"/>. Toda la información aquí
/// mostrada proviene directamente de los registros ya expuestos por los módulos -- este archivo no
/// calcula nada, solo da formato.
///
/// Backlog v3, Fase A (2026-08-28, hallazgo H3 del informe de auditoría externa del usuario): esta
/// clase vivía antes en <c>Tanque.App</c> (acoplada a la UI aunque nunca dependió de Avalonia) y se
/// movió, sin cambiar el contenido de ningún reporte producido, a esta biblioteca independiente
/// <c>Tanque.Reportes</c> -- ahora 100% verificable en el sandbox de la nube. Se agregó
/// <see cref="GenerarReporte"/>, que reemplaza la construcción del reporte completo que antes vivía
/// en <c>Tanque.App.MainWindow.EjecutarCalculo</c> (mismo texto, carácter por carácter, ahora a
/// partir de <see cref="ResultadoCalculoTanque"/> en vez de leer TextBox/ComboBox directamente).
/// </summary>
public static class ReporteResultados
{
    private static readonly CultureInfo Ci = CultureInfo.InvariantCulture;
    private static string Linea() => new string('-', 78);

    private static string N(double v, string formato = "0.###") => v.ToString(formato, Ci);
    private static string Cumple(bool cumple) => cumple ? "CUMPLE" : "NO CUMPLE";

    // Detallado de barra (diámetro fijo elegido por elemento + separación comercial). Muestra
    // "NO CUMPLE" y sugiere un diámetro mayor cuando el elegido no alcanza (ver
    // ResultadoDisenoFlexion.DetalladoInsuficiente); vacío solo cuando no hay detallado resuelto.
    private static string Detalle(ResultadoDisenoFlexion f)
        => DetalleCore(f.DiametroBarraMm, f.SeparacionM, f.DetalladoInsuficiente, f.DiametroSugeridoMm);

    // Núcleo común del texto de detallado: lo usan tanto los diseños directos (Detalle) como la
    // envolvente del fondo (FormatCaraEnvolvente), que expone los mismos cuatro campos aplanados.
    private static string DetalleCore(double? diametroBarraMm, double? separacionM, bool insuficiente, double? sugerido)
    {
        if (insuficiente)
        {
            var sugerencia = sugerido is double sug
                ? $"aumente el diámetro a Ø{sug:0.#} mm o mayor"
                : "revise el espesor o la configuración del elemento";
            return separacionM is double s
                ? $"  Ø{diametroBarraMm:0.#} mm @ {s * 1000:0} mm -- NO CUMPLE (control de fisuración): {sugerencia}"
                : $"  Ø{diametroBarraMm:0.#} mm -- NO CUMPLE: ni a la separación mínima suministra el acero requerido; {sugerencia}";
        }
        return diametroBarraMm is double d2 && separacionM is double s2
            ? $"  Ø{d2:0.#} mm @ {s2 * 1000:0} mm"
            : "";
    }

    // Backlog v2, interfaz/reportes (2026-08-27): etiquetas de posición para las grillas PCA/Marcus
    // completas (ver FormatGrilla). Ambos métodos (Caso 10 placas, Caso 3 muros) dividen cada
    // dirección de un cuarto de panel en décimos, desde el borde (0.0) hasta el centro/eje de
    // simetría (0.5) -- 6 puntos por dirección horizontal ("b" en ambos casos: ancho de la placa o
    // luz horizontal del muro). El Caso 3 de muro NO es simétrico verticalmente (tope libre, base
    // empotrada), así que su dirección "a" (altura) tabula los 11 puntos completos 0.0..1.0, no un
    // cuarto -- ver el docstring de MurosRectangulares.
    private static string[] EtiquetasBorde6(string sufijo) =>
    [
        $"0.0{sufijo}", $"0.1{sufijo}", $"0.2{sufijo}", $"0.3{sufijo}", $"0.4{sufijo}", $"0.5{sufijo}(c)"
    ];

    private static readonly string[] EtiquetasAlturaMuro11 =
    [
        "0.0a(tope)", "0.1a", "0.2a", "0.3a", "0.4a", "0.5a", "0.6a", "0.7a", "0.8a", "0.9a", "1.0a(base)"
    ];

    /// <summary>
    /// Backlog v2, interfaz/reportes (2026-08-27): imprime un campo completo (6×6 de placa u 11×6
    /// de muro) como tabla de texto de ancho fijo, celda por celda -- fila = posición en la
    /// dirección "a" (ver <paramref name="etiquetasFila"/>), columna = posición en la dirección "b".
    /// </summary>
    private static string FormatGrilla(string titulo, double[,] campo, string[] etiquetasFila, string[] etiquetasColumna)
    {
        const int anchoCelda = 9;
        const int anchoEtiqueta = 11;
        var sb = new StringBuilder();
        sb.AppendLine($"    {titulo}, kN·m/m (filas: posición en \"a\"; columnas: posición en \"b\"):");
        sb.Append(new string(' ', anchoEtiqueta));
        foreach (var col in etiquetasColumna) sb.Append(col.PadLeft(anchoCelda));
        sb.AppendLine();
        for (var fila = 0; fila < campo.GetLength(0); fila++)
        {
            sb.Append(etiquetasFila[fila].PadRight(anchoEtiqueta));
            for (var col = 0; col < campo.GetLength(1); col++)
                sb.Append(N(campo[fila, col], "0.00").PadLeft(anchoCelda));
            sb.AppendLine();
        }
        return sb.ToString();
    }

    public static string CargasGravitacionales(ResultadoCargasGravitacionales r)
    {
        var sb = new StringBuilder();
        sb.AppendLine("CARGAS GRAVITACIONALES (Formulario 3)");
        sb.AppendLine(Linea());
        sb.AppendLine($"  Par de muros tipo B (Pm1): {N(r.Pm1ParMurosTipoBKN)} kN");
        sb.AppendLine($"  Par de muros tipo L (Pm2): {N(r.Pm2ParMurosTipoLKN)} kN");
        sb.AppendLine($"  Placa de cubierta (Pt):    {N(r.PtCubiertaKN)} kN");
        sb.AppendLine($"  Placa de fondo (Pf):       {N(r.PfFondoKN)} kN");
        sb.AppendLine($"  Peso total del tanque:     {N(r.PttTotalKN)} kN");
        sb.AppendLine($"  Carga uniforme de cubierta (W1, D): {N(r.W1UniformeKNm2)} kN/m²");
        sb.AppendLine();
        return sb.ToString();
    }

    public static string PresionesLaterales(ResultadoPresionesLaterales r)
    {
        var sb = new StringBuilder();
        sb.AppendLine("PRESIONES LATERALES (Formulario 4)");
        sb.AppendLine(Linea());
        sb.AppendLine($"  Ka (Rankine, corregido):        {N(r.Ka, "0.#####")}");
        sb.AppendLine($"  Ph máxima (líquido, en el fondo): {N(r.PhMaximaKNm2)} kN/m²");
        sb.AppendLine($"  Ps2 máxima (suelo, en el fondo):  {N(r.Ps2MaximaKNm2)} kN/m²");
        sb.AppendLine();
        return sb.ToString();
    }

    private static string FormatMuroSismico(ResultadoFuerzaSismicaMuro m)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"  Dirección del sismo: {N(m.DireccionSismoM)} m (L/HL o B/HL = {N(m.RelacionDireccionSismoHL)})");
        sb.AppendLine($"  Wi (impulsivo): {N(m.WiPesoImpulsivoKN)} kN    Wc (convectivo): {N(m.WcPesoConvectivoKN)} kN");
        sb.AppendLine($"  hi: {N(m.HiAlturaCentroideImpulsivoM)} m       hc: {N(m.HcAlturaCentroideConvectivoM)} m");
        sb.AppendLine($"  Ti: {N(m.TiPeriodoImpulsivoS)} s     Tc: {N(m.TcPeriodoConvectivoS)} s");
        sb.AppendLine($"  Sds: {N(m.Sds)}   S1: {N(m.S1)}   Ci: {N(m.Ci)}   Cc: {N(m.Cc)}");
        sb.AppendLine($"  Ri: {N(m.Ri)}   Rc: {N(m.Rc)}");
        sb.AppendLine($"  Pw (inercial del muro): {N(m.PwInercialMuroKN)} kN   Pr (sobrecarga cubierta): {N(m.PrSobrecargaCubiertaKN)} kN");
        sb.AppendLine($"  Pi (impulsiva): {N(m.PiImpulsivaKN)} kN   Pc (convectiva): {N(m.PcConvectivaKN)} kN");
        sb.AppendLine($"  Presión impulsiva  -- fondo: {N(m.PresionImpulsiva.FondoKNm2)} kN/m²   superficie: {N(m.PresionImpulsiva.SuperficieKNm2)} kN/m²");
        sb.AppendLine($"  Presión convectiva -- fondo: {N(m.PresionConvectiva.FondoKNm2)} kN/m²   superficie: {N(m.PresionConvectiva.SuperficieKNm2)} kN/m²");
        return sb.ToString();
    }

    public static string FuerzaSismicaHidrodinamica(ResultadoFuerzaSismicaHidrodinamica r)
    {
        var sb = new StringBuilder();
        sb.AppendLine("FUERZA SÍSMICA HIDRODINÁMICA -- Housner/ACI 350.3 (Formulario 5)");
        sb.AppendLine(Linea());
        sb.AppendLine(" Muro longitudinal (usa L):");
        sb.Append(FormatMuroSismico(r.MuroLongitudinal));
        sb.AppendLine(" Muro transversal (usa B):");
        sb.Append(FormatMuroSismico(r.MuroTransversal));
        sb.AppendLine();
        return sb.ToString();
    }

    public static string FuerzaDinamicaSuelo(ResultadoFuerzaDinamicaSuelo r)
    {
        var sb = new StringBuilder();
        sb.AppendLine("FUERZA DINÁMICA DE SUELO -- Mononobe-Okabe (Formulario 6)");
        sb.AppendLine(Linea());
        sb.AppendLine($"  θ: {N(r.ThetaGrados)}°   ψ: {N(r.Psi)}");
        sb.AppendLine($"  Ka (estático, Rankine corregido): {N(r.Ka, "0.#####")}");
        sb.AppendLine($"  Kae (sísmico): {N(r.Kae, "0.#####")}   Keq = Kae-Ka: {N(r.Keq, "0.#####")}");
        sb.AppendLine($"  Qae (presión dinámica de diseño): {N(r.QaeKNm2)} kN/m²");
        sb.AppendLine();
        return sb.ToString();
    }

    private static string FormatDireccion(string etiqueta, ResultadoDisenoDireccionPlaca d)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"  {etiqueta}: Mu={N(d.MuKNm)} kN·m/m  d={N(d.DEfectivoM, "0.000")} m  " +
                       $"ρ={N(d.Flexion.Rho, "0.00000")}  As req.={N(d.Flexion.AsRequeridoMm2)} mm²/m" + Detalle(d.Flexion));
        sb.AppendLine($"    Servicio: Ms={N(d.MsKNm)} kN·m/m  fs={N(d.Fisuracion.FsMPa)} MPa " +
                       $"(fs,adm={N(d.Fisuracion.FsAdmisibleMPa)} MPa, {Cumple(d.Fisuracion.Cumple)})  " +
                       $"Sd={N(d.Servicio.Sd, "0.000")}  Mu,servicio={N(d.Servicio.MuServicioKNm)} kN·m/m");
        return sb.ToString();
    }

    private static string FormatCortantePlaca(string etiqueta, ResultadoDisenoCortantePlaca c)
    {
        return $"  {etiqueta}: Vu={N(c.VuKN)} kN/m  Vc={N(c.Cortante.VcKN)} kN/m " +
               $"(d={N(c.DEfectivoM, "0.000")} m, {Cumple(c.Cortante.Cumple)})\n";
    }

    public static string Placa(string titulo, ResultadoPlacaRectangular calculo, ResultadoDisenoPlaca diseno, bool incluirDiagramas = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine(titulo);
        sb.AppendLine(Linea());
        sb.AppendLine($"  r = b/a = {N(calculo.R, "0.000")}");
        sb.AppendLine($"  Mx+ = {N(calculo.MxPosGobernanteKNmM)}  Mx- = {N(calculo.MxNegGobernanteKNmM)}  " +
                       $"My+ = {N(calculo.MyPosGobernanteKNmM)}  My- = {N(calculo.MyNegGobernanteKNmM)} kN·m/m");
        sb.AppendLine($"  Vx = {N(calculo.VxKNm)} kN/m   Vy = {N(calculo.VyKNm)} kN/m");
        sb.AppendLine(" Diseño de refuerzo (Formularios 12-14):");
        sb.Append(FormatDireccion("Mx+ (inferior)", diseno.MxPositivo));
        sb.Append(FormatDireccion("Mx- (superior)", diseno.MxNegativo));
        sb.Append(FormatDireccion("My+ (inferior)", diseno.MyPositivo));
        sb.Append(FormatDireccion("My- (superior)", diseno.MyNegativo));
        sb.Append(FormatCortantePlaca("Cortante en x", diseno.CortanteX));
        sb.Append(FormatCortantePlaca("Cortante en y", diseno.CortanteY));
        if (incluirDiagramas)
        {
            sb.AppendLine(" Diagramas de momento por celda (grilla PCA/Marcus Caso 10, cuarto de panel por simetría doble):");
            var ea = EtiquetasBorde6("a");
            var eb = EtiquetasBorde6("b");
            sb.Append(FormatGrilla("Mx+ (cara inferior)", calculo.CampoMxPos, ea, eb));
            sb.Append(FormatGrilla("Mx- (cara superior, signo preservado)", calculo.CampoMxNeg, ea, eb));
            sb.Append(FormatGrilla("My+ (cara inferior)", calculo.CampoMyPos, ea, eb));
            sb.Append(FormatGrilla("My- (cara superior, signo preservado)", calculo.CampoMyNeg, ea, eb));
            sb.AppendLine("  Cortante: el método PCA/Marcus solo tabula valores puntuales en el borde (no un campo distribuido) -- ver Vx/Vy arriba.");
        }
        sb.AppendLine();
        return sb.ToString();
    }

    private static string FormatCondicionMuro(string etiqueta, ResultadoMuroRectangular? m)
    {
        var sb = new StringBuilder();
        if (m is null)
        {
            sb.AppendLine($"  {etiqueta} -- NO APLICA (sin suelo contra el muro, Hm=0 -- ver la nota 'Condición exterior OMITIDA' más abajo).");
            return sb.ToString();
        }
        sb.AppendLine($"  {etiqueta} -- r={N(m.R, "0.000")}");
        sb.AppendLine($"    Mx+ = {N(m.MxPosGobernanteKNmM)}  Mx- = {N(m.MxNegGobernanteKNmM)}  " +
                       $"My+ = {N(m.MyPosGobernanteKNmM)}  My- = {N(m.MyNegGobernanteKNmM)} kN·m/m");
        sb.AppendLine($"    V fondo = {N(m.VBottomKNm)}  V lateral máx. = {N(m.VSideMaxKNm)}  " +
                       $"V lateral medio = {N(m.VSideMidKNm)} kN/m");
        return sb.ToString();
    }

    private static string FormatDireccionMuro(string etiqueta, ResultadoDisenoDireccionMuro d)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"  {etiqueta}: Mu={N(d.MuKNm)} kN·m/m  [{d.ComboGobernante}]  " +
                       $"ρ={N(d.Flexion.Rho, "0.00000")}  As req.={N(d.Flexion.AsRequeridoMm2)} mm²/m" + Detalle(d.Flexion));
        if (d.MsKNm is null || d.Fisuracion is null || d.Servicio is null)
        {
            sb.AppendLine("    Control de fisuración/servicio: OMITIDO (la combinación gobernante es sísmica; el control de fisuración solo se evalúa bajo combinaciones estáticas de servicio).");
        }
        else
        {
            sb.AppendLine($"    Servicio: Ms={N(d.MsKNm.Value)} kN·m/m  fs={N(d.Fisuracion.FsMPa)} MPa " +
                           $"(fs,adm={N(d.Fisuracion.FsAdmisibleMPa)} MPa, {Cumple(d.Fisuracion.Cumple)})  " +
                           $"Sd={N(d.Servicio.Sd, "0.000")}  Mu,servicio={N(d.Servicio.MuServicioKNm)} kN·m/m");
        }
        return sb.ToString();
    }

    private static string FormatCortanteMuro(string etiqueta, ResultadoDisenoCortanteMuro c)
    {
        return $"  {etiqueta}: Vu={N(c.VuKN)} kN/m  [{c.ComboGobernante}]  Vc={N(c.Cortante.VcKN)} kN/m ({Cumple(c.Cortante.Cumple)})\n";
    }

    public static string Muro(string titulo, ResultadoMuroPorCondiciones estatico, ResultadoDisenoMuro diseno, bool incluirDiagramas = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine(titulo);
        sb.AppendLine(Linea());
        sb.AppendLine(" Análisis estático (Caso 3 PCA, NSR-10 B.2.4-1/B.2.4-2):");
        sb.Append(FormatCondicionMuro("Interior (líquido)", estatico.Interior));
        sb.Append(FormatCondicionMuro("Exterior (suelo)", estatico.Exterior));
        sb.AppendLine();
        sb.AppendLine($" Diseño de refuerzo -- d efectivo = {N(diseno.DEfectivoM, "0.000")} m, " +
                       $"sismo {(diseno.SismoIncluido ? "INCLUIDO" : "OMITIDO")}" +
                       (diseno.SismoIncluido ? "" : $" ({diseno.MotivoSismoOmitido})"));
        if (diseno.MotivoExteriorOmitido is not null)
            sb.AppendLine($" Condición exterior OMITIDA: {diseno.MotivoExteriorOmitido}");
        if (diseno.NotaAproximacionSismicaInterior is not null)
            sb.AppendLine($" Aproximación conservadora (sísmico interior): {diseno.NotaAproximacionSismicaInterior}");
        if (diseno.NotaAproximacionSismicaExterior is not null)
            sb.AppendLine($" Aproximación conservadora (sísmico exterior): {diseno.NotaAproximacionSismicaExterior}");
        sb.Append(FormatDireccionMuro("Vertical + (Mx+, cara interior)", diseno.VerticalPositivo));
        sb.Append(FormatDireccionMuro("Vertical - (Mx-, cara exterior)", diseno.VerticalNegativo));
        sb.Append(FormatDireccionMuro("Horizontal + (My+)", diseno.HorizontalPositivo));
        sb.Append(FormatDireccionMuro("Horizontal - (My-)", diseno.HorizontalNegativo));
        sb.Append(FormatCortanteMuro("Cortante en el fondo", diseno.CortanteFondo));
        sb.Append(FormatCortanteMuro("Cortante lateral máximo", diseno.CortanteLateralMaximo));
        sb.Append(FormatCortanteMuro("Cortante lateral medio", diseno.CortanteLateralMedio));
        if (incluirDiagramas)
        {
            sb.AppendLine(" Diagramas de momento por celda (grilla PCA/Marcus Caso 3 -- filas: tope libre..base empotrada; columnas: borde..centro):");
            var eb = EtiquetasBorde6("b");
            void ImprimirCondicion(string etiqueta, ResultadoMuroRectangular? m)
            {
                if (m is null)
                {
                    sb.AppendLine($"  -- {etiqueta}: NO APLICA (ver más arriba).");
                    return;
                }
                sb.AppendLine($"  -- {etiqueta}:");
                sb.Append(FormatGrilla("Mx+ (cara interior)", m.CampoMxPos, EtiquetasAlturaMuro11, eb));
                sb.Append(FormatGrilla("Mx- (cara exterior, signo preservado)", m.CampoMxNeg, EtiquetasAlturaMuro11, eb));
                sb.Append(FormatGrilla("My+", m.CampoMyPos, EtiquetasAlturaMuro11, eb));
                sb.Append(FormatGrilla("My- (signo preservado)", m.CampoMyNeg, EtiquetasAlturaMuro11, eb));
            }
            ImprimirCondicion("Interior (líquido)", estatico.Interior);
            ImprimirCondicion("Exterior (suelo)", estatico.Exterior);
            sb.AppendLine("  Cortante: el método PCA/Marcus solo tabula valores puntuales (fondo/lateral máx./lateral medio) -- ver arriba; no hay campo distribuido.");
        }
        sb.AppendLine();
        return sb.ToString();
    }

    /// <summary>Backlog v2, ítem 7 (2026-08-26): NSR-10 C.23-C.14.6.</summary>
    public static string EspesorMinimoMuro(ResultadoEspesorMinimoMuro r)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ESPESOR MÍNIMO DE MURO (NSR-10 C.23-C.14.6)");
        sb.AppendLine(Linea());
        sb.AppendLine($"  Espesor real (em): {N(r.EspesorRealM, "0.000")} m   Mínimo aplicable: {N(r.EspesorMinimoAplicableM, "0.000")} m  -- {Cumple(r.Cumple)}");
        sb.AppendLine($"  Cláusula aplicada: {r.ClausulaAplicada}");
        if (!r.Cumple) sb.AppendLine($"  Déficit de espesor: {N(r.DeficitM, "0.000")} m -- aumente em o revise la altura del muro.");
        sb.AppendLine();
        return sb.ToString();
    }

    /// <summary>
    /// Backlog v2, ítem 2/6 (2026-08-26): ACI 350.4R-04 §3.1.2, solo TipoTanque.EnterradoConNivelFreatico.
    /// <paramref name="sobreancho"/> (segunda iteración del mismo ítem): se pasa cuando el usuario
    /// proveyó γsuelo,sat y el resultado no cumple.
    /// </summary>
    public static string Flotabilidad(ResultadoFlotabilidad r, ResultadoSobreanchoFlotabilidad? sobreancho = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("VERIFICACIÓN DE FLOTABILIDAD (ACI 350.4R-04 §3.1.2)");
        sb.AppendLine(Linea());
        sb.AppendLine($"  Peso propio (sin mayorar): {N(r.PesoPropioKN)} kN   Subpresión (nivel freático): {N(r.SubpresionKN)} kN");
        sb.AppendLine($"  FS = {N(r.FS, "0.000")}  (mínimo exigido: {N(Tanque.Core.Modulos.Flotabilidad.FactorSeguridadMinimo, "0.00")})  -- {Cumple(r.Cumple)}");
        if (!r.Cumple)
        {
            sb.AppendLine($"  Déficit de peso: {N(r.DeficitPesoKN)} kN.");
            if (sobreancho is null)
            {
                sb.AppendLine("  Sobreancho automático de losa: no calculado -- provea γsuelo,sat (peso unitario saturado del " +
                              "suelo de relleno, sección \"Materiales\") para que el reporte sugiera cuánto ensanchar la losa de fondo.");
            }
            else if (!sobreancho.EsPosible)
            {
                sb.AppendLine($"  Sobreancho automático de losa: NO ES POSIBLE -- {sobreancho.Mensaje}");
            }
            else
            {
                sb.AppendLine($"  Sobreancho automático de losa: ensanche {N(sobreancho.SobreanchoRequeridoM!.Value, "0.###")} m " +
                               "en cada uno de los 4 lados (valor límite exacto -- redondee hacia arriba a un valor práctico de detallado).");
                sb.AppendLine($"    Área añadida: {N(sobreancho.AreaProyeccionM2!.Value)} m²   " +
                               $"Peso de concreto añadido: {N(sobreancho.PesoConcretoProyeccionKN!.Value)} kN   " +
                               $"Peso de suelo sobre la proyección: {N(sobreancho.PesoSueloSobreProyeccionKN!.Value)} kN");
                sb.AppendLine($"    Con la proyección: peso propio {N(sobreancho.PesoPropioConProyeccionKN!.Value)} kN, " +
                               $"subpresión {N(sobreancho.SubpresionConProyeccionKN!.Value)} kN, " +
                               $"FS = {N(sobreancho.FSConProyeccion!.Value, "0.000")}.");
            }
        }
        sb.AppendLine();
        return sb.ToString();
    }

    private static string FormatCaraLosaFondoSubpresion(string etiqueta, ResultadoDisenoCaraLosaFondoSubpresion c)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"  {etiqueta}: Mu={N(c.MuKNm)} kN·m/m  d={N(c.DEfectivoM, "0.000")} m  " +
                       $"ρ={N(c.Flexion.Rho, "0.00000")}  As req.={N(c.Flexion.AsRequeridoMm2)} mm²/m" + Detalle(c.Flexion));
        if (c.MsKNm is null || c.Fisuracion is null || c.Servicio is null)
        {
            sb.AppendLine("    Control de fisuración/servicio: OMITIDO (a nivel de servicio el peso propio ya contrarresta la subpresión).");
        }
        else
        {
            sb.AppendLine($"    Servicio: Ms={N(c.MsKNm.Value)} kN·m/m  fs={N(c.Fisuracion.FsMPa)} MPa " +
                           $"(fs,adm={N(c.Fisuracion.FsAdmisibleMPa)} MPa, {Cumple(c.Fisuracion.Cumple)})  " +
                           $"Sd={N(c.Servicio.Sd, "0.000")}  Mu,servicio={N(c.Servicio.MuServicioKNm)} kN·m/m");
        }
        return sb.ToString();
    }

    private static string FormatCortanteLosaFondoSubpresion(string etiqueta, ResultadoDisenoCortanteLosaFondoSubpresion c)
    {
        return $"  {etiqueta}: Vu={N(c.VuKN)} kN/m  Vc={N(c.Cortante.VcKN)} kN/m " +
               $"(d={N(c.DEfectivoM, "0.000")} m, {Cumple(c.Cortante.Cumple)})\n";
    }

    /// <summary>
    /// Backlog v2, punto 3 (2026-08-27): diseño local de la losa de fondo bajo subpresión de agua
    /// freática. Independiente y complementario del reporte gravitacional ya impreso (PLACA DE
    /// FONDO); el refuerzo final de cada cara debe ser el mayor entre ambos reportes.
    /// </summary>
    public static string LosaFondoSubpresion(ResultadoDisenoLosaFondoSubpresion r, bool incluirDiagramas = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine("LOSA DE FONDO BAJO SUBPRESIÓN");
        sb.AppendLine(Linea());
        sb.AppendLine($"  Presión neta mayorada (1.4×subpresión-0.9×peso propio/área): {N(r.QNetoMayoradoKNm2)} kN/m²   " +
                       $"Presión neta de servicio: {N(r.QNetoServicioKNm2)} kN/m²");
        if (!r.Aplica)
        {
            sb.AppendLine($"  {r.Mensaje}");
        }
        else
        {
            sb.AppendLine($"  {r.Mensaje}");
            sb.Append(FormatCaraLosaFondoSubpresion("Mx, cara superior (formada -- antes \"Mx+\")", r.MxCaraSuperior!));
            sb.Append(FormatCaraLosaFondoSubpresion("Mx, cara inferior (contra suelo -- antes \"Mx-\")", r.MxCaraInferior!));
            sb.Append(FormatCaraLosaFondoSubpresion("My, cara superior (formada -- antes \"My+\")", r.MyCaraSuperior!));
            sb.Append(FormatCaraLosaFondoSubpresion("My, cara inferior (contra suelo -- antes \"My-\")", r.MyCaraInferior!));
            sb.Append(FormatCortanteLosaFondoSubpresion("Cortante en x", r.CortanteX!));
            sb.Append(FormatCortanteLosaFondoSubpresion("Cortante en y", r.CortanteY!));
            if (incluirDiagramas)
            {
                sb.AppendLine(" Diagramas de momento por celda -- caso de subpresión mayorado (grilla PCA/Marcus Caso 10, cuarto de panel por simetría doble; signo preservado, ver la cabecera de cada campo):");
                var ea = EtiquetasBorde6("a");
                var eb = EtiquetasBorde6("b");
                sb.Append(FormatGrilla("Mx cara superior (\"positivo\" bajo subpresión -- formada)", r.CampoMxCaraSuperior!, ea, eb));
                sb.Append(FormatGrilla("Mx cara inferior (\"negativo\" bajo subpresión -- contra suelo, signo preservado)", r.CampoMxCaraInferior!, ea, eb));
                sb.Append(FormatGrilla("My cara superior (\"positivo\" bajo subpresión -- formada)", r.CampoMyCaraSuperior!, ea, eb));
                sb.Append(FormatGrilla("My cara inferior (\"negativo\" bajo subpresión -- contra suelo, signo preservado)", r.CampoMyCaraInferior!, ea, eb));
                sb.AppendLine("  Cortante: el método PCA/Marcus solo tabula valores puntuales en el borde (no un campo distribuido) -- ver Cortante en x/y arriba.");
            }
        }
        sb.AppendLine();
        return sb.ToString();
    }

    private static string FormatCaraEnvolvente(string etiqueta, ResultadoEnvolventeCaraPlacaFondo c)
    {
        var sb = new StringBuilder();
        var origen = c.GobernaSubpresion ? "gobierna SUBPRESIÓN" : "gobierna GRAVITACIONAL";
        sb.AppendLine($"  {etiqueta} [{origen}]: Mu={N(c.MuKNm)} kN·m/m  d={N(c.DEfectivoM, "0.000")} m  " +
                       $"ρ={N(c.Rho, "0.00000")}  As req.={N(c.AsRequeridoMm2)} mm²/m" +
                       DetalleCore(c.DiametroBarraMm, c.SeparacionM, c.DetalladoInsuficiente, c.DiametroSugeridoMm));
        if (c.MsKNm is null || c.FsMPa is null || c.FsAdmisibleMPa is null || c.FisuracionCumple is null)
        {
            sb.AppendLine("    Control de fisuración/servicio: OMITIDO (ver el caso gobernante arriba).");
        }
        else
        {
            sb.AppendLine($"    Servicio: Ms={N(c.MsKNm.Value)} kN·m/m  fs={N(c.FsMPa.Value)} MPa " +
                           $"(fs,adm={N(c.FsAdmisibleMPa.Value)} MPa, {Cumple(c.FisuracionCumple.Value)})");
        }
        return sb.ToString();
    }

    private static string FormatCortanteEnvolvente(string etiqueta, ResultadoEnvolventeCortantePlacaFondo c)
    {
        var origen = c.GobernaSubpresion ? "gobierna SUBPRESIÓN" : "gobierna GRAVITACIONAL";
        return $"  {etiqueta} [{origen}]: Vu={N(c.VuKN)} kN/m  Vc={N(c.VcKN)} kN/m " +
               $"(d={N(c.DEfectivoM, "0.000")} m, {Cumple(c.Cumple)})\n";
    }

    /// <summary>
    /// Backlog v2, punto 3, ampliado (2026-08-27): diseño final de la losa de fondo, envolviendo el
    /// reporte gravitacional (PLACA DE FONDO) y el de subpresión (arriba) cara por cara.
    /// </summary>
    public static string EnvolventePlacaFondo(ResultadoEnvolventePlacaFondo r, ResultadoEnvolventeCamposPlacaFondo? campos = null, bool incluirDiagramas = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine("DISEÑO FINAL DE LA LOSA DE FONDO (envolvente gravitacional + subpresión)");
        sb.AppendLine(Linea());
        sb.Append(FormatCaraEnvolvente("Cara inferior (contra suelo), Mx", r.MxCaraInferior));
        sb.Append(FormatCaraEnvolvente("Cara superior (formada), Mx", r.MxCaraSuperior));
        sb.Append(FormatCaraEnvolvente("Cara inferior (contra suelo), My", r.MyCaraInferior));
        sb.Append(FormatCaraEnvolvente("Cara superior (formada), My", r.MyCaraSuperior));
        sb.Append(FormatCortanteEnvolvente("Cortante en x", r.CortanteX));
        sb.Append(FormatCortanteEnvolvente("Cortante en y", r.CortanteY));
        if (incluirDiagramas && campos is not null)
        {
            sb.AppendLine(" Diagramas de momento por celda -- ENVOLVENTE del diseño final (grilla PCA/Marcus Caso 10, cuarto de panel por simetría doble; cada celda es el máximo en magnitud entre el caso gravitacional y el de subpresión -- el As de refuerzo a usar sigue siendo el valor gobernante ya mostrado arriba, no este diagrama):");
            var ea = EtiquetasBorde6("a");
            var eb = EtiquetasBorde6("b");
            sb.Append(FormatGrilla("Mx cara inferior (contra suelo)", campos.CampoMxCaraInferior, ea, eb));
            sb.Append(FormatGrilla("Mx cara superior (formada)", campos.CampoMxCaraSuperior, ea, eb));
            sb.Append(FormatGrilla("My cara inferior (contra suelo)", campos.CampoMyCaraInferior, ea, eb));
            sb.Append(FormatGrilla("My cara superior (formada)", campos.CampoMyCaraSuperior, ea, eb));
            sb.AppendLine("  Cortante: el método PCA/Marcus solo tabula valores puntuales en el borde (no un campo distribuido) -- ver Cortante en x/y arriba.");
        }
        sb.AppendLine();
        return sb.ToString();
    }

    /// <summary>
    /// Backlog v3, Fase A (2026-08-28): reemplaza la construcción del reporte completo que antes
    /// vivía en <c>Tanque.App.MainWindow.EjecutarCalculo</c> -- produce EXACTAMENTE el mismo texto,
    /// carácter por carácter, ahora a partir del resultado agregado de
    /// <see cref="CalculadorTanque.Calcular"/> en vez de leer TextBox/ComboBox y orquestar los
    /// módulos directamente en el code-behind de la UI (hallazgos H1/H3 del informe de auditoría
    /// externa del usuario). La UI (MainWindow.EjecutarCalculo) queda reducida a: leer los
    /// controles → construir <see cref="ProyectoTanque"/>/<see cref="ParametrosCalculoTanque"/> →
    /// llamar a <see cref="CalculadorTanque.Calcular"/> → llamar a este método → mostrar el texto.
    /// </summary>
    public static string GenerarReporte(ResultadoCalculoTanque r)
    {
        var g = r.Proyecto.Geometria;
        var conTapa = g.ConTapa;
        var tipoTanque = g.Tipo;
        var incluirDiagramas = r.Parametros.IncluirDiagramas;

        var sb = new StringBuilder();
        sb.AppendLine("TANQUE.CORE -- REPORTE DE CÁLCULO");
        sb.AppendLine($"Generado: {DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine($"Desarrollador: {IdentidadDesarrollador.Nombre}");
        sb.AppendLine($"Afiliación: {IdentidadDesarrollador.Afiliacion}");
        sb.AppendLine($"Contacto: {IdentidadDesarrollador.Contacto} · ORCID: {IdentidadDesarrollador.Orcid}");
        sb.AppendLine(new string('=', 78));
        sb.AppendLine();

        // 1. Cargas gravitacionales (Formulario 3)
        sb.Append(CargasGravitacionales(r.Cargas));

        // 2. Presiones laterales (Formulario 4)
        sb.Append(PresionesLaterales(r.Presiones));

        // 3. Sismo hidrodinámico (F.5) y dinámico de suelo (F.6) -- opcionales
        if (r.Parametros.Sismo is not null)
        {
            sb.Append(FuerzaSismicaHidrodinamica(r.SismoHidro!));
            sb.Append(FuerzaDinamicaSuelo(r.SismoSuelo!));
        }
        else
        {
            sb.AppendLine("ANÁLISIS SÍSMICO -- OMITIDO (casilla \"Incluir análisis sísmico\" desmarcada).");
            sb.AppendLine();
        }

        // 4. Placas rectangulares PCA/Marcus + diseño (Formularios 7, 10-11)
        if (conTapa)
        {
            sb.Append(Placa("PLACA DE CUBIERTA (Formulario 7)", r.PlacaCubierta!, r.DisenoCubierta!, incluirDiagramas));
        }
        else
        {
            sb.AppendLine("PLACA DE CUBIERTA -- OMITIDA (el tanque no tiene tapa).");
            sb.AppendLine();
        }

        // Corrección (2026-08-27, a pedido del usuario): los diagramas de momento/cortante de la losa
        // de fondo solo se imprimen aquí (caso puramente gravitacional) cuando NO hay envolvente de
        // subpresión que los reemplace más abajo -- ver DISEÑO FINAL DE LA LOSA DE FONDO.
        sb.Append(Placa("PLACA DE FONDO (Formularios 10-11)", r.PlacaFondo, r.DisenoFondo,
            incluirDiagramas && tipoTanque != TipoTanque.EnterradoConNivelFreatico));

        // 5. Muros rectangulares PCA/Marcus (estático F.8-9, sísmico aumentado) + diseño final
        sb.Append(Muro("MURO LONGITUDINAL (Formulario 8, span L)", r.MuroLongitudinalEstatico, r.DisenoMuroLongitudinal, incluirDiagramas));
        sb.Append(Muro("MURO TRANSVERSAL (Formulario 9, span B)", r.MuroTransversalEstatico, r.DisenoMuroTransversal, incluirDiagramas));

        // 6. Espesor mínimo de muro (NSR-10 C.23-C.14.6) -- siempre se verifica.
        sb.Append(EspesorMinimoMuro(r.EspesorMinimoMuro));

        // 7. Flotabilidad + losa de fondo bajo subpresión + envolvente final -- solo
        // TipoTanque.EnterradoConNivelFreatico.
        if (tipoTanque == TipoTanque.EnterradoConNivelFreatico)
        {
            sb.Append(Flotabilidad(r.Flotabilidad!, r.Sobreancho));
            sb.Append(LosaFondoSubpresion(r.LosaFondoSubpresion!, incluirDiagramas));
            sb.Append(EnvolventePlacaFondo(r.EnvolventeFondo!, r.CamposEnvolventeFondo, incluirDiagramas));
        }
        else
        {
            // Corrección (2026-08-27, a raíz de reporte del usuario): aviso explícito de la omisión,
            // nunca silenciosa.
            sb.AppendLine("VERIFICACIÓN DE FLOTABILIDAD / LOSA DE FONDO BAJO SUBPRESIÓN / ENVOLVENTE -- OMITIDAS " +
                          $"(tipo de tanque actual: \"{tipoTanque}\"; estas tres verificaciones solo aplican " +
                          "cuando \"Tipo de tanque\" = \"Enterrado, con nivel freático\" -- selecciónelo arriba " +
                          "y provea la altura del nivel freático para activarlas).");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Backlog v3, Fase B (2026-08-28): una sección con nombre de grupo, para presentar el reporte
    /// en secciones plegables (<c>Expander</c>) en la interfaz en vez de un único bloque de texto
    /// corrido. Ver <see cref="GenerarSeccionesAgrupadas"/>.
    /// </summary>
    public sealed record SeccionReporte(string Grupo, string Texto);

    /// <summary>
    /// Backlog v3, Fase B (2026-08-28, decidida por el usuario): reorganiza "Datos de entrada" y
    /// "Resultados" en secciones plegables agrupadas por la misma taxonomía "GRUPOS" (sismo,
    /// hidrostático, tierras, dinámico, envolventes, diseño de muros, diseño de losas, armaduras)
    /// que el informe externo de auditoría identificó en el manual del programa de referencia
    /// comercial "Módulo Tanques" (ver informe_consolidado_tanque_2026-08-28.md).
    ///
    /// Este método NO calcula ni formatea nada nuevo -- llama exactamente a los mismos métodos de
    /// formato, en el mismo orden y con los mismos argumentos, que <see cref="GenerarReporte"/>;
    /// solo particiona la misma secuencia de <c>Append</c> en tramos con nombre de grupo en vez de
    /// concatenarla en un único <see cref="StringBuilder"/>. <see cref="GenerarReporte"/> y todos
    /// sus métodos de formato internos quedan intactos, byte a byte -- el reporte plano
    /// (exportación .txt) no cambia en absoluto.
    ///
    /// Agrupamiento aplicado (partición contigua y en el mismo orden que <see cref="GenerarReporte"/>,
    /// así que concatenar <see cref="SeccionReporte.Texto"/> de todas las secciones en orden
    /// reconstruye EXACTAMENTE el mismo texto que produce <see cref="GenerarReporte"/> -- verificado
    /// en <c>tools/Tanque.Core.Verificacion</c>):
    /// <list type="bullet">
    /// <item>"Datos generales" -- encabezado + Cargas Gravitacionales (Formulario 3).</item>
    /// <item>"Hidrostático / Tierras" -- Presiones Laterales (Formulario 4); ambos comparten un
    /// único cálculo (Rankine) y no se separan para no reformatear un método ya verificado.</item>
    /// <item>"Sismo" -- Fuerza Sísmica Hidrodinámica (Formulario 5), o el aviso de omisión si la
    /// casilla "Incluir análisis sísmico" está desmarcada.</item>
    /// <item>"Dinámico" -- Fuerza Dinámica de Suelo (Formulario 6); presente solo cuando el sismo
    /// está incluido (si está omitido, el aviso ya quedó bajo "Sismo", sin duplicarlo).</item>
    /// <item>"Diseño de losas" -- placa de cubierta (o su aviso de omisión) + placa de fondo,
    /// incluyendo su diseño de refuerzo (As, ρ, cortante).</item>
    /// <item>"Diseño de muros" -- muro longitudinal + muro transversal + espesor mínimo de muro,
    /// incluyendo su diseño de refuerzo (As, ρ, cortante).</item>
    /// <item>"Envolventes" -- flotabilidad + losa de fondo bajo subpresión + diseño final envolvente
    /// de la losa de fondo, o el aviso de omisión conjunto si el tipo de tanque no tiene nivel
    /// freático.</item>
    /// </list>
    /// No existe un cálculo de "armaduras" (detallado de barras) separado en <c>Tanque.Core</c> --
    /// el refuerzo (As, ρ) se calcula y se muestra dentro de cada elemento (losas/muros), no como un
    /// módulo aparte, así que no se inventa aquí una sección "Armaduras" vacía o duplicada.
    /// </summary>
    public static IReadOnlyList<SeccionReporte> GenerarSeccionesAgrupadas(ResultadoCalculoTanque r)
        => GenerarSeccionesAgrupadas(r, r.Parametros.IncluirDiagramas);

    /// <summary>
    /// Igual que <see cref="GenerarSeccionesAgrupadas(ResultadoCalculoTanque)"/> pero con control
    /// explícito de las grillas ASCII de momento. Fase3 del frente de interfaz (2026-08-30): el
    /// reporte HTML llama con <c>false</c> porque sus diagramas van como SVG inline. El
    /// comportamiento por defecto queda idéntico (misma partición y mismo texto, byte a byte) --
    /// verificado por la herramienta de verificación.
    /// </summary>
    public static IReadOnlyList<SeccionReporte> GenerarSeccionesAgrupadas(ResultadoCalculoTanque r, bool incluirDiagramas)
    {
        var g = r.Proyecto.Geometria;
        var conTapa = g.ConTapa;
        var tipoTanque = g.Tipo;
        var secciones = new List<SeccionReporte>();

        var sbGeneral = new StringBuilder();
        sbGeneral.AppendLine("TANQUE.CORE -- REPORTE DE CÁLCULO");
        sbGeneral.AppendLine($"Generado: {DateTime.Now:yyyy-MM-dd HH:mm}");
        sbGeneral.AppendLine($"Desarrollador: {IdentidadDesarrollador.Nombre}");
        sbGeneral.AppendLine($"Afiliación: {IdentidadDesarrollador.Afiliacion}");
        sbGeneral.AppendLine($"Contacto: {IdentidadDesarrollador.Contacto} · ORCID: {IdentidadDesarrollador.Orcid}");
        sbGeneral.AppendLine(new string('=', 78));
        sbGeneral.AppendLine();
        sbGeneral.Append(CargasGravitacionales(r.Cargas));
        secciones.Add(new SeccionReporte("Datos generales", sbGeneral.ToString()));

        secciones.Add(new SeccionReporte("Hidrostático / Tierras", PresionesLaterales(r.Presiones)));

        if (r.Parametros.Sismo is not null)
        {
            secciones.Add(new SeccionReporte("Sismo", FuerzaSismicaHidrodinamica(r.SismoHidro!)));
            secciones.Add(new SeccionReporte("Dinámico", FuerzaDinamicaSuelo(r.SismoSuelo!)));
        }
        else
        {
            var sbOmitido = new StringBuilder();
            sbOmitido.AppendLine("ANÁLISIS SÍSMICO -- OMITIDO (casilla \"Incluir análisis sísmico\" desmarcada).");
            sbOmitido.AppendLine();
            secciones.Add(new SeccionReporte("Sismo", sbOmitido.ToString()));
        }

        var sbLosas = new StringBuilder();
        if (conTapa)
        {
            sbLosas.Append(Placa("PLACA DE CUBIERTA (Formulario 7)", r.PlacaCubierta!, r.DisenoCubierta!, incluirDiagramas));
        }
        else
        {
            sbLosas.AppendLine("PLACA DE CUBIERTA -- OMITIDA (el tanque no tiene tapa).");
            sbLosas.AppendLine();
        }
        sbLosas.Append(Placa("PLACA DE FONDO (Formularios 10-11)", r.PlacaFondo, r.DisenoFondo,
            incluirDiagramas && tipoTanque != TipoTanque.EnterradoConNivelFreatico));
        secciones.Add(new SeccionReporte("Diseño de losas", sbLosas.ToString()));

        var sbMuros = new StringBuilder();
        sbMuros.Append(Muro("MURO LONGITUDINAL (Formulario 8, span L)", r.MuroLongitudinalEstatico, r.DisenoMuroLongitudinal, incluirDiagramas));
        sbMuros.Append(Muro("MURO TRANSVERSAL (Formulario 9, span B)", r.MuroTransversalEstatico, r.DisenoMuroTransversal, incluirDiagramas));
        sbMuros.Append(EspesorMinimoMuro(r.EspesorMinimoMuro));
        secciones.Add(new SeccionReporte("Diseño de muros", sbMuros.ToString()));

        var sbEnvolventes = new StringBuilder();
        if (tipoTanque == TipoTanque.EnterradoConNivelFreatico)
        {
            sbEnvolventes.Append(Flotabilidad(r.Flotabilidad!, r.Sobreancho));
            sbEnvolventes.Append(LosaFondoSubpresion(r.LosaFondoSubpresion!, incluirDiagramas));
            sbEnvolventes.Append(EnvolventePlacaFondo(r.EnvolventeFondo!, r.CamposEnvolventeFondo, incluirDiagramas));
        }
        else
        {
            sbEnvolventes.AppendLine("VERIFICACIÓN DE FLOTABILIDAD / LOSA DE FONDO BAJO SUBPRESIÓN / ENVOLVENTE -- OMITIDAS " +
                          $"(tipo de tanque actual: \"{tipoTanque}\"; estas tres verificaciones solo aplican " +
                          "cuando \"Tipo de tanque\" = \"Enterrado, con nivel freático\" -- selecciónelo arriba " +
                          "y provea la altura del nivel freático para activarlas).");
            sbEnvolventes.AppendLine();
        }
        secciones.Add(new SeccionReporte("Envolventes", sbEnvolventes.ToString()));

        return secciones;
    }
}
