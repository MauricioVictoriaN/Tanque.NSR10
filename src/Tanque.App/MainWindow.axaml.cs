// ---------------------------------------------------------------------------
// Tanque.NSR10
// (c) Mauricio Javier Victoria Niño <hidratecsa@gmail.com> · CC BY-NC-SA 4.0
// Uso exclusivamente académico. El motor de cálculo (Tanque.Core) se distribuye
// como binario ofuscado, protegido por derechos de autor (ver LICENSE).
// ---------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Tanque.Core.Dominio;
using Tanque.Core.Modulos;
using Tanque.Reportes;

namespace Tanque.App;

/// <summary>
/// Interfaz mínima de escritorio (Avalonia) sobre Tanque.Core -- expone, en una sola ventana con
/// dos pestañas ("Datos de entrada" / "Resultados"), los módulos de cálculo y de integración de la
/// biblioteca (ver RUTA_TRABAJO_PROXIMAS_SESIONES.md). Deliberadamente NO incluye
/// persistencia de proyectos, generación de PDF, ni pulido visual -- es el primer paso mínimo que
/// deja el núcleo de cálculo utilizable desde una interfaz real, no la interfaz final.
///
/// Diseño deliberadamente sin data binding: los campos de entrada son TextBox/ComboBox con
/// x:Name, leídos directamente al presionar "Calcular" (ver <see cref="Leer"/>), sin ViewModel ni
/// {Binding}. Compilada, ejecutada y validada visualmente por el usuario en su propio equipo el
/// 2026-08-25 (con los valores por defecto del formulario, antes de los campos del backlog v2
/// añadidos esta sesión).
///
/// BACKLOG V2 (2026-08-26) -- campos y verificaciones nuevos añadidos a este formulario, escritos
/// y revisados manualmente contra la API real de Tanque.Core (misma disciplina que el resto del
/// archivo) pero NO recompilados en un equipo con NuGet real después del cambio (el sandbox de
/// sesión en la nube sigue sin poder restaurar este proyecto, ver Tanque.App.csproj) -- riesgo
/// documentado explícitamente, no oculto: `CmbTipoTanque`/`TxtAlturaNivelFreatico` (TipoTanque,
/// activa la verificación de flotabilidad), `CmbMetodoInterpolacion` (interpolar vs. redondeo
/// conservador en Placas/Muros estáticos), y los reportes de `EspesoresMinimos`/`Flotabilidad`.
///
/// BACKLOG V3, FASE A (2026-08-28, hallazgos H1/H3 del informe de auditoría externa del usuario):
/// <see cref="EjecutarCalculo"/> ya NO orquesta los ocho módulos de cálculo -- solo lee los
/// controles, arma <see cref="Geometria"/>/<see cref="Materiales"/>/<see cref="ParametrosCalculoTanque"/>
/// y delega todo el cálculo a <see cref="CalculadorTanque.Calcular"/> (Tanque.Core) y todo el
/// formato del reporte a <see cref="ReporteResultados.GenerarReporte"/> (la nueva biblioteca
/// <c>Tanque.Reportes</c>, que reemplaza al antiguo <c>Tanque.App.ReporteResultados</c>). Extracción
/// mecánica, verificada carácter por carácter contra el reporte que producía el código anterior --
/// ver <c>tools/Tanque.Core.Verificacion</c>, módulo 12, y
/// informe_consolidado_tanque_2026-08-28.md. **Confirmado por el usuario en su propio
/// equipo, 2026-08-28**: build limpio, `dotnet test` 67/67, y la aplicación real generando reportes
/// correctos.
///
/// BACKLOG V3, FASE B (2026-08-28, decidida por el usuario: secciones agrupadas Expander): ambas
/// pestañas se reorganizaron en secciones plegables por grupo, siguiendo la misma taxonomía
/// "GRUPOS" (sismo, hidrostático, tierras, dinámico, envolventes, diseño de muros, diseño de losas,
/// armaduras) que el informe externo de auditoría identificó en el manual del programa de
/// referencia comercial "Módulo Tanques" -- sin rehacer la navegación de 2 pestañas ni introducir
/// ViewModel/data binding, tal como decidió el usuario. En "Datos de entrada" los x:Name de todos
/// los controles no cambiaron (solo se reordenaron dentro de <c>Expander</c>), así que
/// <see cref="Leer"/>/<see cref="EjecutarCalculo"/> no requirieron ningún cambio por eso. En
/// "Resultados", el único <c>TxtResultados</c> se reemplazó por <c>PanelResultados</c> (contenedor
/// vacío) más <see cref="MostrarResultadosAgrupados"/>, que construye un <c>Expander</c> por grupo a
/// partir de <see cref="ReporteResultados.GenerarSeccionesAgrupadas"/> (nueva, en
/// <c>Tanque.Reportes</c>) -- el reporte de texto plano (exportar .txt) sigue siendo exactamente el
/// mismo, <see cref="ReporteResultados.GenerarReporte"/>, sin ningún cambio. La partición por grupos
/// se verificó reconstruyendo el mismo texto carácter por carácter contra
/// <see cref="ReporteResultados.GenerarReporte"/> -- ver <c>tools/Tanque.Core.Verificacion</c>,
/// módulo 12 (700/700 aserciones). Pendiente explícito de la próxima sesión con acceso al equipo
/// del usuario: `dotnet run` y validar visualmente que "Datos de entrada" se ve igual que antes
/// (solo con Expander plegables) y que "Resultados" ahora muestra un Expander por grupo con el
/// mismo contenido de siempre.
/// </summary>
public partial class MainWindow : Window
{
    // Backlog v2 (2026-08-27, interfaz/reportes): último reporte generado por BtnCalcular_Click,
    // conservado para que BtnExportar_Click pueda escribirlo a un archivo .txt sin depender de
    // releer los TextBox de la pestaña "Resultados" (backlog v3, Fase B: ahora un TextBox de solo
    // lectura por grupo/Expander, ver MostrarResultadosAgrupados; antes un único TxtResultados) y
    // para poder distinguir "aún no se ha calculado nada" de un reporte vacío.
    private string? _ultimoReporte;

    // Fase3 del frente de interfaz (2026-08-30): último resultado completo calculado, conservado
    // para que BtnExportarHtml_Click pueda generar el reporte HTML profesional
    // (ReporteHtml.Generar) sin releer los TextBox/ComboBox de entrada.
    private ResultadoCalculoTanque? _ultimoResultado;

    // Fase4 (2026-08-31, ítems 1 y 3): validación en vivo -- mapa caja→validador y el tooltip
    // descriptivo original de cada caja (para restaurarlo cuando la caja vuelve a ser válida).
    private readonly Dictionary<TextBox, Func<string, string?>> _validadores = new();
    private readonly Dictionary<TextBox, string?> _tooltipsOriginales = new();

    public MainWindow()
    {
        InitializeComponent();
        ConfigurarIdentidadDesarrollador();
        ConfigurarComboDiametros();
        ConfigurarValidacionEnVivo();
        ConfigurarAccesibilidad();
    }

    /// <summary>
    /// Rellena el bloque "Desarrollador / Autor del programa" de la pestaña "Datos de entrada" con
    /// la identidad del autor (fuente única: <see cref="IdentidadDesarrollador"/> de Tanque.Reportes)
    /// -- la misma que aparece en el encabezado/pie de todos los reportes. Información de autoría,
    /// sin ningún efecto en el cálculo.
    /// </summary>
    private void ConfigurarIdentidadDesarrollador()
    {
        DevNombre.Text = IdentidadDesarrollador.Nombre;
        DevInfo.Text =
            $"{IdentidadDesarrollador.Afiliacion} · {IdentidadDesarrollador.Contacto} · ORCID: {IdentidadDesarrollador.Orcid}";
    }

    /// <summary>
    /// Puebla los cuatro selectores de diámetro de barra (cubierta / fondo / muro longitudinal /
    /// muro transversal) DESDE el catálogo único de Tanque.Core
    /// (<see cref="CatalogoBarras.DiametrosComercialesMm"/>, No.4 a No.10) -- fuente única de
    /// verdad: la UI no puede ofrecer un diámetro que el núcleo no valide (NSR-10
    /// C.23-C.7.12.2.2 excluye la No.3). Preselecciona No.5 (15.9 mm), valor de partida
    /// profesional documentado en el catálogo.
    /// </summary>
    private void ConfigurarComboDiametros()
    {
        var combos = new[] { CmbDiametroCubierta, CmbDiametroFondo, CmbDiametroMuroLong, CmbDiametroMuroTrans };
        var indicePredeterminado = Array.IndexOf(CatalogoBarras.DiametrosComercialesMm, CatalogoBarras.DiametroPredeterminadoBarraMm);
        foreach (var combo in combos)
        {
            combo.Items.Clear();
            foreach (var db in CatalogoBarras.DiametrosComercialesMm)
                combo.Items.Add(new ComboBoxItem { Content = CatalogoBarras.DescripcionBarra(db) });
            combo.SelectedIndex = Math.Max(0, indicePredeterminado);
        }
    }

    private async void BtnCalcular_Click(object? sender, RoutedEventArgs e)
    {
        OcultarVeredicto();
        TxtEstado.Foreground = Brushes.DimGray;
        TxtEstado.Text = "Calculando...";
        BtnCalcular.IsEnabled = false;
        try
        {
            // Lee los controles en el hilo de la UI (los controles Avalonia son de un solo hilo);
            // la validación de dominio (Geometria.Validar/Materiales.Validar) corre aquí y lanza con
            // el mensaje completo ante cualquier insumo inválido.
            var entrada = LeerEntradaTanque();

            // Fase4 (2026-08-31, ítem 2): cálculo + reporte en un hilo de fondo (Task.Run) para no
            // bloquear la UI; la continuación del await vuelve al hilo de la UI (SynchronizationContext
            // de Avalonia) para actualizar pestañas/banner.
            var (resultado, reporte) = await Task.Run(() => EjecutarCalculo(entrada));

            _ultimoReporte = reporte;
            _ultimoResultado = resultado;
            MostrarResultadosAgrupados(resultado);
            MostrarVeredicto(Veredicto.Calcular(resultado));
            MostrarDiagramas(resultado);
            TabPrincipal.SelectedIndex = 1;
            TxtEstado.Foreground = Brushes.DarkGreen;
            TxtEstado.Text = "Cálculo completado -- ver la pestaña \"Resultados\".";
        }
        catch (Exception ex)
        {
            OcultarVeredicto();
            TxtEstado.Foreground = Brushes.Firebrick;
            TxtEstado.Text = $"Error: {ex.Message}";
        }
        finally
        {
            BtnCalcular.IsEnabled = true;
        }
    }

    /// <summary>
    /// Fase 1 del frente de interfaz (2026-08-30): muestra el veredicto global CUMPLE/NO CUMPLE
    /// (<see cref="Veredicto.Calcular"/>) en el banner del encabezado. El veredicto es la
    /// conjunción de las señales normativas YA verificadas por cada módulo (espesor C.23-C.14.6,
    /// detallado Ø/s, fisuración fs≤fs,adm, cortante Vu≤Vc, flotabilidad FS≥1.25) -- no introduce
    /// ningún chequeo nuevo ni contradice el reporte detallado (una sola fuente de verdad). El
    /// banner resume los elementos/conceptos que fallan; el detalle numérico de cada uno está en la
    /// pestaña "Resultados".
    /// </summary>
    private void MostrarVeredicto(ResultadoVeredicto veredicto)
    {
        BannerVeredictoBorder.IsVisible = true;
        if (veredicto.Cumple)
        {
            BannerVeredictoBorder.Background = Brushes.DarkGreen;
            BannerVeredicto.Text = "CUMPLE — todas las verificaciones normativas satisfacen la norma.";
        }
        else
        {
            BannerVeredictoBorder.Background = Brushes.DarkRed;
            var fallos = veredicto.Items.Where(i => !i.Cumple).Select(i => $"{i.Elemento} · {i.Concepto}");
            BannerVeredicto.Text = $"NO CUMPLE — {veredicto.Items.Count(i => !i.Cumple)} verificación(es) no satisfacen la norma: {string.Join("  |  ", fallos)}. Ver la pestaña \"Resultados\" para el detalle numérico.";
        }
    }

    /// <summary>Oculta el banner de veredicto (antes de calcular y ante cualquier error).</summary>
    private void OcultarVeredicto()
    {
        BannerVeredictoBorder.IsVisible = false;
    }

    /// <summary>
    /// Fase 2 del frente de interfaz (2026-08-30): rellena la pestaña "Diagramas", agrupada por
    /// elemento, con tres niveles de contenido: (1) el CORTANTE gobernante puntual (opción (a)
    /// aprobada: PCA/Marcus no tabula campo distribuido de cortante, solo los valores gobernantes
    /// ya diseñados); (2) los MAPAS DE CALOR del campo completo de momento (<see cref="ConstruirMapaCalor"/>),
    /// con el valor numérico por celda -- la vista 2D que enriquece el análisis; y (3) las CURVAS de
    /// la faja gobernante (<see cref="DiagramaMomentoControl"/>) como complemento 1D. Los datos
    /// vienen de <see cref="DiagramaMomento.Calcular"/> -- no se calcula nada aquí ni en la UI.
    /// </summary>
    private void MostrarDiagramas(ResultadoCalculoTanque resultado)
    {
        PanelDiagramas.Children.Clear();
        var diagramas = DiagramaMomento.Calcular(resultado);

        // Nota de orientación para usuarios no expertos (presentación pura, sin fórmulas): explica
        // en lenguaje llano qué significa el color, qué cara está a tracción y qué es la celda
        // resaltada. Se muestra una sola vez al inicio de la pestaña.
        PanelDiagramas.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(239, 246, 255)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(147, 197, 253)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 6),
            Margin = new Thickness(0, 0, 0, 10),
            Child = new TextBlock
            {
                Text = "¿Cómo leer estos diagramas? Los mapas de calor muestran el campo de momentos (kN·m/m por celda) del análisis PCA/Marcus. Rojo (+) = sagging → la cara del título está a tracción (el acero va en esa cara); azul (−) = hogging → la tracción está en la cara opuesta; blanco = momento ≈0. La celda con borde oscuro es el momento gobernante que usa el diseño. Muro: filas = altura (tope libre → base empotrada), columnas = semiluz (borde → centro). Placa: ambos ejes = semiluz (borde → centro).",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(30, 64, 175))
            }
        });

        // Agrupa por elemento para una lectura ordenada: cortante → mapas de calor → curvas.
        var elementos = diagramas.Campos.Select(c => c.Elemento).Distinct().ToList();

        foreach (var elemento in elementos)
        {
            PanelDiagramas.Children.Add(new TextBlock
            {
                Text = elemento,
                FontWeight = FontWeight.Bold,
                FontSize = 16,
                Margin = new Thickness(0, 8, 0, 4)
            });

            // Cortante gobernante puntual.
            var corts = diagramas.Cortantes.Where(c => c.Elemento == elemento).ToList();
            if (corts.Count > 0)
            {
                PanelDiagramas.Children.Add(new TextBlock
                {
                    Text = "Cortante gobernante (Vu ≤ Vc)",
                    FontWeight = FontWeight.SemiBold,
                    FontSize = 13,
                    Margin = new Thickness(0, 4, 0, 2)
                });
                foreach (var c in corts)
                {
                    PanelDiagramas.Children.Add(new TextBlock
                    {
                        Text = $"{c.Ubicacion}: Vu = {c.VuKNm:0.##} kN/m, Vc = {c.VcKNm:0.##} kN/m — {(c.Cumple ? "CUMPLE" : "NO CUMPLE")}",
                        Foreground = c.Cumple ? Brushes.DarkGreen : Brushes.Firebrick,
                        Margin = new Thickness(0, 0, 0, 2)
                    });
                }
            }

            // Mapas de calor: campo completo, con el valor por celda.
            var campos = diagramas.Campos.Where(c => c.Elemento == elemento).ToList();
            if (campos.Count > 0)
            {
                PanelDiagramas.Children.Add(new TextBlock
                {
                    Text = "Mapas de momento (kN·m/m por celda)",
                    FontWeight = FontWeight.SemiBold,
                    FontSize = 13,
                    Margin = new Thickness(0, 8, 0, 2)
                });
                foreach (var campo in campos)
                {
                    var titulo = string.IsNullOrEmpty(campo.Condicion)
                        ? $"{campo.Direccion} · {campo.Cara}"
                        : $"{campo.Direccion} · {campo.Cara} · {campo.Condicion}";
                    PanelDiagramas.Children.Add(new TextBlock
                    {
                        Text = titulo,
                        FontWeight = FontWeight.Bold,
                        FontSize = 12,
                        Margin = new Thickness(0, 6, 0, 2)
                    });
                    PanelDiagramas.Children.Add(ConstruirMapaCalor(campo));
                }
            }

            // Curvas de la faja gobernante (complemento 1D del mapa de calor).
            var curvas = diagramas.Curvas.Where(c => c.Elemento == elemento).ToList();
            if (curvas.Count > 0)
            {
                PanelDiagramas.Children.Add(new TextBlock
                {
                    Text = "Faja gobernante (curva de momento)",
                    FontWeight = FontWeight.SemiBold,
                    FontSize = 13,
                    Margin = new Thickness(0, 8, 0, 2)
                });
                foreach (var curva in curvas)
                {
                    var pico = curva.Puntos.Max(p => Math.Abs(p.MomentoKNmM));
                    var titulo = string.IsNullOrEmpty(curva.Condicion)
                        ? curva.Direccion
                        : $"{curva.Direccion} · {curva.Condicion}";
                    PanelDiagramas.Children.Add(new TextBlock
                    {
                        Text = $"{titulo} — Máx |M| = {pico:0.##} kN·m/m · faja {curva.LuzM:0.###} m",
                        Foreground = Brushes.DimGray,
                        FontSize = 11,
                        Margin = new Thickness(0, 4, 0, 2)
                    });
                    PanelDiagramas.Children.Add(new DiagramaMomentoControl
                    {
                        Curva = curva,
                        Width = 520,
                        Height = 170
                    });
                }
            }
        }
    }

    /// <summary>
    /// Construye el mapa de calor de un <see cref="CampoMomento"/> como un <see cref="Grid"/> de
    /// celdas (<see cref="TextBlock"/> con fondo coloreado y el valor numérico), usando la escala
    /// <see cref="MapaDeColor"/> ANCLADA EN CERO (<see cref="MapaDeColor.RangoSimetrico"/>: cero
    /// siempre en blanco, sagging hacia rojo, hogging hacia azul, con el mismo significado en todos
    /// los mapas). Además imprime la orientación física de los ejes y resalta la celda gobernante
    /// (máx |M|, la que usa el diseño) con un borde oscuro (<see cref="OrientacionMapa"/>). Solo usa
    /// controles estándar -- sin librerías externas ni dibujo a mano. No calcula nada: pinta el
    /// campo ya verificado por el núcleo.
    /// </summary>
    private Control ConstruirMapaCalor(CampoMomento campo)
    {
        var vals = campo.Valores;
        int filas = vals.GetLength(0);
        int cols = vals.GetLength(1);

        // Escala simétrica anclada en cero: blanco = 0 SIEMPRE, +sagging → rojo, −hogging → azul.
        var (min, max) = MapaDeColor.RangoSimetrico(vals);
        var (gf, gc, gVal) = OrientacionMapa.CeldaGobernante(vals);

        var grid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); // etiquetas de columna
        for (int f = 0; f < filas; f++) grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); // leyenda
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto)); // etiquetas de fila
        for (int c = 0; c < cols; c++) grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        // Etiquetas de columna con el borde físico (borde … centro).
        for (int c = 0; c < cols; c++)
        {
            var tb = new TextBlock
            {
                Text = OrientacionMapa.EtiquetaColumna(campo, c, cols),
                FontSize = 10,
                Foreground = Brushes.DimGray,
                Margin = new Thickness(4, 0, 4, 2),
                TextAlignment = TextAlignment.Center
            };
            Grid.SetRow(tb, 0);
            Grid.SetColumn(tb, c + 1);
            grid.Children.Add(tb);
        }

        // Celdas con su etiqueta de fila (tope/base para muro, borde/centro para losa).
        for (int f = 0; f < filas; f++)
        {
            var lbl = new TextBlock
            {
                Text = OrientacionMapa.EtiquetaFila(campo, f, filas),
                FontSize = 10,
                Foreground = Brushes.DimGray,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Right
            };
            Grid.SetRow(lbl, f + 1);
            Grid.SetColumn(lbl, 0);
            grid.Children.Add(lbl);

            for (int c = 0; c < cols; c++)
            {
                double v = vals[f, c];
                var (r, g, b) = MapaDeColor.Color(v, min, max);
                bool gobernante = f == gf && c == gc;
                var celda = new TextBlock
                {
                    Text = v.ToString("0.##", CultureInfo.InvariantCulture),
                    FontSize = 10,
                    Foreground = gobernante ? Brushes.White : Brushes.Black,
                    FontWeight = gobernante ? FontWeight.Bold : FontWeight.Normal,
                    Background = new SolidColorBrush(Color.FromRgb(r, g, b)),
                    TextAlignment = TextAlignment.Center,
                    MinWidth = 46,
                    MinHeight = 20,
                    Padding = new Thickness(2)
                };

                // La celda gobernante (máx |M|) se resalta con borde oscuro y texto en blanco.
                if (gobernante)
                {
                    var borde = new Border
                    {
                        Child = celda,
                        BorderBrush = Brushes.Black,
                        BorderThickness = new Thickness(2),
                        Margin = new Thickness(0.5)
                    };
                    Grid.SetRow(borde, f + 1);
                    Grid.SetColumn(borde, c + 1);
                    grid.Children.Add(borde);
                }
                else
                {
                    celda.Margin = new Thickness(0.5);
                    Grid.SetRow(celda, f + 1);
                    Grid.SetColumn(celda, c + 1);
                    grid.Children.Add(celda);
                }
            }
        }

        // Leyenda semántica: significado del color + el valor gobernante (resaltado en el mapa).
        var leyenda = new TextBlock
        {
            Text = $"Rojo (+) = sagging · azul (−) = hogging · blanco = 0 — refuerzo de esta cara donde el color es más intenso\n" +
                   $"Escala: -{max:0.#} → 0 → +{max:0.#} kN·m/m · filas {campo.LuzFilasM:0.###} m · columnas {campo.LuzColumnasM:0.###} m\n" +
                   $"Gobernante (celda con borde): M = {gVal:0.##} kN·m/m en ({OrientacionMapa.PosicionFila(campo, gf, filas):0.##} m, {OrientacionMapa.PosicionColumna(campo, gc, cols):0.##} m)",
            FontSize = 10,
            Foreground = Brushes.DimGray,
            Margin = new Thickness(0, 4, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetRow(leyenda, filas + 1);
        Grid.SetColumn(leyenda, 0);
        Grid.SetColumnSpan(leyenda, cols + 1);
        grid.Children.Add(leyenda);

        return grid;
    }

    /// <summary>
    /// Backlog v3, Fase B (2026-08-28, decidida por el usuario): reconstruye la pestaña
    /// "Resultados" como un <see cref="Expander"/> por grupo (ver
    /// <see cref="ReporteResultados.GenerarSeccionesAgrupadas"/>), reemplazando el contenido de
    /// <c>PanelResultados</c> cada vez que se calcula. Construcción puramente procedural (sin
    /// data binding/ViewModel, consistente con el resto del archivo) -- cada sección se muestra en
    /// un TextBox de solo lectura con el mismo estilo monoespaciado que tenía el TextBox único
    /// anterior. El reporte de texto plano exportado por "Exportar informe (.txt)" no usa este
    /// método -- sigue leyendo <c>reporte</c> (ReporteResultados.GenerarReporte) directamente.
    ///
    /// Cada sección se envuelve en su propio <see cref="ScrollViewer"/> con scroll horizontal
    /// propio (para las grillas de diagramas, que son más anchas que la ventana, sin envolver
    /// texto -- <see cref="Avalonia.Media.TextWrapping.NoWrap"/>) y scroll vertical deshabilitado,
    /// para que el TextBox crezca a su altura natural y sea el ScrollViewer exterior de la pestaña
    /// (declarado en MainWindow.axaml) el que controla el único scroll vertical de la página --
    /// evita anidar dos barras de desplazamiento verticales una dentro de otra.
    /// </summary>
    private void MostrarResultadosAgrupados(ResultadoCalculoTanque resultado)
    {
        PanelResultados.Children.Clear();
        var secciones = ReporteResultados.GenerarSeccionesAgrupadas(resultado);
        foreach (var seccion in secciones)
        {
            var caja = new TextBox
            {
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = Avalonia.Media.TextWrapping.NoWrap,
                FontFamily = new FontFamily("Consolas,Cascadia Mono,Menlo,monospace"),
                FontSize = 12,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Text = seccion.Texto,
            };
            var scroll = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = caja,
            };
            var expander = new Expander
            {
                Header = seccion.Grupo,
                IsExpanded = true,
                Content = scroll,
            };
            PanelResultados.Children.Add(expander);
        }
    }

    // Backlog v2 (2026-08-27, interfaz/reportes): exporta el último reporte calculado a un archivo
    // de texto plano (.txt), elegido por el usuario, usando el selector de archivos nativo de
    // Avalonia 11 (IStorageProvider -- Tanque.App.csproj usa Avalonia 11.1.3, que ya no incluye el
    // SaveFileDialog clásico). Deliberadamente texto plano, sin dependencias nuevas de NuGet (a
    // pedido explícito del usuario, que prefirió .txt sobre PDF/Word para esta primera versión).
    private async void BtnExportar_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_ultimoReporte))
        {
            TxtEstado.Foreground = Brushes.Firebrick;
            TxtEstado.Text = "No hay ningún reporte para exportar -- presione \"Calcular\" primero.";
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null)
        {
            TxtEstado.Foreground = Brushes.Firebrick;
            TxtEstado.Text = "No se pudo acceder al selector de archivos del sistema.";
            return;
        }

        IStorageFile? archivo;
        try
        {
            archivo = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Exportar informe de cálculo",
                SuggestedFileName = $"tanque-informe-{DateTime.Now:yyyy-MM-dd-HHmm}.txt",
                DefaultExtension = "txt",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Texto plano (*.txt)") { Patterns = new[] { "*.txt" } }
                }
            });
        }
        catch (Exception ex)
        {
            TxtEstado.Foreground = Brushes.Firebrick;
            TxtEstado.Text = $"Error al abrir el selector de archivos: {ex.Message}";
            return;
        }

        if (archivo is null)
        {
            // El usuario canceló el diálogo -- no es un error, se deja TxtEstado como estaba.
            return;
        }

        try
        {
            await using var flujo = await archivo.OpenWriteAsync();
            await using var escritor = new StreamWriter(flujo, Encoding.UTF8);
            await escritor.WriteAsync(_ultimoReporte);
            TxtEstado.Foreground = Brushes.DarkGreen;
            TxtEstado.Text = $"Informe exportado a \"{archivo.Name}\".";
        }
        catch (Exception ex)
        {
            TxtEstado.Foreground = Brushes.Firebrick;
            TxtEstado.Text = $"Error al escribir el archivo: {ex.Message}";
        }
    }

    // Fase3 del frente de interfaz (2026-08-30): exporta el reporte profesional HTML autocontenido
    // (ReporteHtml.Generar -- encabezado + veredicto + secciones + mapas de calor SVG), elegido por
    // el usuario, usando el mismo selector nativo de Avalonia 11 que BtnExportar_Click. Cero
    // dependencias nuevas; el archivo se abre en cualquier navegador y se imprime a PDF.
    private async void BtnExportarHtml_Click(object? sender, RoutedEventArgs e)
    {
        if (_ultimoResultado is null)
        {
            TxtEstado.Foreground = Brushes.Firebrick;
            TxtEstado.Text = "No hay ningún reporte para exportar -- presione \"Calcular\" primero.";
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null)
        {
            TxtEstado.Foreground = Brushes.Firebrick;
            TxtEstado.Text = "No se pudo acceder al selector de archivos del sistema.";
            return;
        }

        IStorageFile? archivo;
        try
        {
            archivo = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Exportar memoria de cálculo (HTML)",
                SuggestedFileName = $"tanque-memoria-{DateTime.Now:yyyy-MM-dd-HHmm}.html",
                DefaultExtension = "html",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("HTML (*.html)") { Patterns = new[] { "*.html", "*.htm" } }
                }
            });
        }
        catch (Exception ex)
        {
            TxtEstado.Foreground = Brushes.Firebrick;
            TxtEstado.Text = $"Error al abrir el selector de archivos: {ex.Message}";
            return;
        }

        if (archivo is null)
        {
            // El usuario canceló el diálogo -- no es un error.
            return;
        }

        try
        {
            var html = ReporteHtml.Generar(_ultimoResultado);
            await using var flujo = await archivo.OpenWriteAsync();
            await using var escritor = new StreamWriter(flujo, Encoding.UTF8);
            await escritor.WriteAsync(html);
            TxtEstado.Foreground = Brushes.DarkGreen;
            TxtEstado.Text = $"Memoria HTML exportada a \"{archivo.Name}\".";
        }
        catch (Exception ex)
        {
            TxtEstado.Foreground = Brushes.Firebrick;
            TxtEstado.Text = $"Error al escribir el archivo: {ex.Message}";
        }
    }

    // Fase4 (2026-08-31, ítem 2): exporta grillas/resultados a CSV (formato largo de 12 columnas,
    // una sola tabla) para análisis en hojas de cálculo -- ExportadorCsv.Generar, que solo reutiliza
    // los campos ya verificados del núcleo (DiagramaMomento para las grillas, catálogo de barras para
    // el detallado). Cero dependencias nuevas. Se escribe con Encoding.UTF8, que en .NET 8 emite BOM
    // al inicio del archivo, de modo que Excel/LibreOffice detectan correctamente los acentos y
    // símbolos (Ø, ·, ≤, ²) de las etiquetas en español.
    private async void BtnExportarCsv_Click(object? sender, RoutedEventArgs e)
    {
        if (_ultimoResultado is null)
        {
            TxtEstado.Foreground = Brushes.Firebrick;
            TxtEstado.Text = "No hay ningún resultado para exportar -- presione \"Calcular\" primero.";
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null)
        {
            TxtEstado.Foreground = Brushes.Firebrick;
            TxtEstado.Text = "No se pudo acceder al selector de archivos del sistema.";
            return;
        }

        IStorageFile? archivo;
        try
        {
            archivo = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Exportar grillas/resultados (CSV)",
                SuggestedFileName = $"tanque-resultados-{DateTime.Now:yyyy-MM-dd-HHmm}.csv",
                DefaultExtension = "csv",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("CSV (*.csv)") { Patterns = new[] { "*.csv" } }
                }
            });
        }
        catch (Exception ex)
        {
            TxtEstado.Foreground = Brushes.Firebrick;
            TxtEstado.Text = $"Error al abrir el selector de archivos: {ex.Message}";
            return;
        }

        if (archivo is null)
        {
            // El usuario canceló el diálogo -- no es un error.
            return;
        }

        try
        {
            var csv = ExportadorCsv.Generar(_ultimoResultado);
            await using var flujo = await archivo.OpenWriteAsync();
            await using var escritor = new StreamWriter(flujo, Encoding.UTF8);
            await escritor.WriteAsync(csv);
            TxtEstado.Foreground = Brushes.DarkGreen;
            TxtEstado.Text = $"Grillas y resultados exportados a \"{archivo.Name}\" -- abra el archivo en Excel/LibreOffice.";
        }
        catch (Exception ex)
        {
            TxtEstado.Foreground = Brushes.Firebrick;
            TxtEstado.Text = $"Error al escribir el archivo: {ex.Message}";
        }
    }

    /// <summary>
    /// Lee un TextBox como número decimal, aceptando tanto "." como "," como separador decimal
    /// (la configuración regional de Colombia, es-CO, usa "," por defecto -- normalizar aquí evita
    /// depender de que el hilo de la aplicación tenga la cultura "correcta" configurada).
    /// </summary>
    private static double Leer(TextBox caja, string nombre)
    {
        var texto = (caja.Text ?? string.Empty).Trim().Replace(',', '.');
        if (!double.TryParse(texto, NumberStyles.Float, CultureInfo.InvariantCulture, out var valor))
            throw new FormatException($"El valor de \"{nombre}\" (\"{caja.Text}\") no es un número válido.");
        return valor;
    }

    /// <summary>
    /// Igual que <see cref="Leer"/>, pero un TextBox en blanco devuelve <c>null</c> en vez de
    /// lanzar -- usado para <see cref="Materiales.GammaSueloSaturadoKNm3"/> (backlog v2, sobreancho
    /// automático de flotabilidad), que es opcional: solo se necesita cuando el usuario quiere que
    /// el reporte calcule el sobreancho, no para el resto del cálculo.
    /// </summary>
    private static double? LeerOpcional(TextBox caja, string nombre)
    {
        var texto = (caja.Text ?? string.Empty).Trim();
        if (texto.Length == 0) return null;
        return Leer(caja, nombre);
    }

    /// <summary>
    /// Lee el diámetro de barra seleccionado en un ComboBox de detallado, mapeando su SelectedIndex
    /// al catálogo único de Tanque.Core -- nunca se acepta texto libre, así que el usuario no puede
    /// elegir un diámetro fuera de la norma (blindaje solicitado 2026-08-29).
    /// </summary>
    private static double LeerDiametro(ComboBox combo, string elemento)
    {
        if (combo.SelectedIndex < 0 || combo.SelectedIndex >= CatalogoBarras.DiametrosComercialesMm.Length)
            throw new InvalidOperationException($"Seleccione un diámetro de barra válido para \"{elemento}\".");
        return CatalogoBarras.DiametrosComercialesMm[combo.SelectedIndex];
    }

    /// <summary>
    /// Backlog v3, Fase A (2026-08-28): solo lee los controles y construye las entradas de
    /// Tanque.Core -- toda la orquestación de los ocho módulos de cálculo vive ahora en
    /// <see cref="CalculadorTanque.Calcular"/>, y todo el formato del reporte en
    /// <see cref="ReporteResultados.GenerarReporte"/> (biblioteca <c>Tanque.Reportes</c>). Antes de
    /// este cambio, este método construía el reporte directamente (llamaba a los ocho módulos y a
    /// once métodos de formato en línea) -- ver el historial de
    /// RUTA_TRABAJO_PROXIMAS_SESIONES.md para el código anterior. La extracción no cambia
    /// ningún cálculo ni ninguna condición de negocio -- verificado carácter por carácter en
    /// <c>tools/Tanque.Core.Verificacion</c> (módulo 12).
    ///
    /// Backlog v3, Fase B (2026-08-28): devuelve además <see cref="ResultadoCalculoTanque"/> (antes
    /// solo el texto del reporte), para que <see cref="MostrarResultadosAgrupados"/> pueda construir
    /// la pestaña "Resultados" en secciones plegables sin recalcular nada. El texto del reporte
    /// plano (para exportar a .txt) sigue siendo exactamente el mismo, producido por
    /// <see cref="ReporteResultados.GenerarReporte"/>, sin ningún cambio.
    ///
    /// Fase4 (2026-08-31, ítem 2): ahora recibe la <see cref="EntradaCalculoTanque"/> ya leída
    /// (la lectura de controles vive en <see cref="BtnCalcular_Click"/>, que debe correr en el hilo
    /// de la UI) y es estática/pura, por lo que se invoca dentro de <c>Task.Run</c> en un hilo de
    /// fondo: la UI no se bloquea durante el cálculo.
    /// </summary>
    private static (ResultadoCalculoTanque Resultado, string Reporte) EjecutarCalculo(EntradaCalculoTanque entrada)
    {
        var resultado = CalculadorTanque.Calcular(entrada.Proyecto, entrada.Parametros);
        return (resultado, ReporteResultados.GenerarReporte(resultado));
    }

    /// <summary>
    /// Fase4 (2026-08-31): lee TODOS los controles de entrada y los devuelve como
    /// <see cref="EntradaCalculoTanque"/> -- el DTO serializable que también usa
    /// <see cref="PersistenciaTanque"/> para guardar/abrir proyectos en JSON. La orquestación de
    /// cálculo sigue en <see cref="CalculadorTanque.Calcular"/> (hallazgo H1). NombreProyecto queda
    /// vacío hasta exponer un campo de nombre en la UI (pendiente Fase4).
    /// </summary>
    private EntradaCalculoTanque LeerEntradaTanque()
    {
        var conTapa = ChkConTapa.IsChecked == true;

        // Backlog v2 (2026-08-26): TipoTanque -- ver Dominio/TipoTanque.cs. AlturaNivelFreaticoM
        // solo aplica (y solo se lee) para TipoTanque.EnterradoConNivelFreatico -- Geometria.Validar
        // exige que sea null en cualquier otro caso, así que no se lee el TextBox si no aplica.
        var tipoTanque = TipoSeleccionado();
        var alturaNivelFreaticoM = tipoTanque == TipoTanque.EnterradoConNivelFreatico
            ? Leer(TxtAlturaNivelFreatico, "altura del nivel freático")
            : (double?)null;

        // Backlog v2 (2026-08-26): método de obtención de coeficientes PCA cuando r no cae
        // exactamente en un valor tabulado -- ver Modulos/MetodoInterpolacion.cs.
        var metodoInterpolacion = CmbMetodoInterpolacion.SelectedIndex == 1
            ? MetodoInterpolacion.RedondearSuperior
            : MetodoInterpolacion.Interpolar;

        var geometria = new Geometria(
            BAnchoM: Leer(TxtB, "B"),
            LLargoM: Leer(TxtL, "L"),
            HtAlturaM: Leer(TxtHt, "Ht"),
            ConTapa: conTapa,
            EmEspesorMuroM: Leer(TxtEm, "em"),
            EfEspesorFondoM: Leer(TxtEf, "ef"),
            EtEspesorTapaM: conTapa ? Leer(TxtEt, "et") : 0.0,
            HLAlturaLiquidoM: Leer(TxtHL, "HL"),
            HmAlturaSueloSobreMuroM: Leer(TxtHm, "Hm"),
            WextSobrecargaKNm2: Leer(TxtWext, "Wext"),
            Tipo: tipoTanque,
            AlturaNivelFreaticoM: alturaNivelFreaticoM);

        var materiales = new Materiales(
            FcMPa: Leer(TxtFc, "f'c"),
            FyMPa: Leer(TxtFy, "fy"),
            GammaSueloKNm3: Leer(TxtGammaSuelo, "γsuelo"),
            GammaConcretoKNm3: Leer(TxtGammaConcreto, "γconcreto"),
            GammaLiquidoKNm3: Leer(TxtGammaLiquido, "γlíquido"),
            PhiGradosAnguloFriccionSuelo: Leer(TxtPhi, "φ"),
            GammaSueloSaturadoKNm3: LeerOpcional(TxtGammaSueloSaturado, "γsuelo,sat"));

        var proyecto = new ProyectoTanque(geometria, materiales);
        proyecto.Validar();

        var cvCubierta = conTapa ? Leer(TxtCvCubierta, "CV cubierta") : 0.0;
        var cgCubierta = conTapa ? Leer(TxtCgCubierta, "CG cubierta") : 0.0;
        var cvFondo = Leer(TxtCvFondo, "CV fondo");
        var diametrosBarra = new DiametrosBarraCalculo(
            CubiertaMm: LeerDiametro(CmbDiametroCubierta, "cubierta"),
            FondoMm: LeerDiametro(CmbDiametroFondo, "fondo"),
            MuroLongitudinalMm: LeerDiametro(CmbDiametroMuroLong, "muro longitudinal"),
            MuroTransversalMm: LeerDiametro(CmbDiametroMuroTrans, "muro transversal"));

        // Backlog v2 (2026-08-27, interfaz/reportes): diagramas de momento por celda -- opt-in
        // porque las grillas PCA/Marcus completas (36 celdas por placa, 66 por muro, ×4 campos
        // Mx+/Mx-/My+/My- cada una) son mucho más extensas que el resto del reporte.
        var incluirDiagramas = ChkIncluirDiagramas.IsChecked == true;

        // 3. Sismo hidrodinámico (F.5) y dinámico de suelo (F.6) -- opcionales.
        ParametrosSismoCalculo? sismo = null;
        if (ChkIncluirSismo.IsChecked == true)
        {
            var condicionBase = CmbCondicionBase.SelectedIndex == 1
                ? CondicionBaseMuro.Flexible
                : CondicionBaseMuro.Rigida;
            var condicionAnclaje = CmbCondicionAnclaje.SelectedIndex switch
            {
                0 => CondicionAnclajeBase.FlexibleAnclada,
                2 => CondicionAnclajeBase.NoAncladaOSinConfinar,
                _ => CondicionAnclajeBase.ArticuladaEmpotrada
            };
            var espectro = new ParametrosEspectroDiseno(
                Aa: Leer(TxtAa, "Aa"), Av: Leer(TxtAv, "Av"),
                Fa: Leer(TxtFa, "Fa"), Fv: Leer(TxtFv, "Fv"),
                I: Leer(TxtI, "I"),
                CondicionBase: condicionBase, CondicionAnclaje: condicionAnclaje);
            var parametrosSuelo = new ParametrosSueloDinamico(
                KhCoeficienteSismicoHorizontal: Leer(TxtKh, "kh"),
                KvCoeficienteSismicoVertical: Leer(TxtKv, "kv"),
                DeltaGradosFriccionSueloMuro: Leer(TxtDelta, "δ"),
                IGradosInclinacionRelleno: Leer(TxtIRelleno, "i"),
                BetaGradosInclinacionMuro: Leer(TxtBeta, "β"));
            sismo = new ParametrosSismoCalculo(espectro, parametrosSuelo);
        }

        var parametros = new ParametrosCalculoTanque(
            CvCubiertaKNm2: cvCubierta,
            CgCubiertaKNm2: cgCubierta,
            CvFondoKNm2: cvFondo,
            DiametrosBarra: diametrosBarra,
            MetodoInterpolacion: metodoInterpolacion,
            IncluirDiagramas: incluirDiagramas,
            Sismo: sismo);

        return new EntradaCalculoTanque("", proyecto, parametros);
    }

    /// <summary>
    /// Fase4 (2026-08-31): escribe de vuelta en los controles de entrada los valores de un proyecto
    /// cargado desde JSON (<see cref="PersistenciaTanque.Deserializar"/>). Formatea con cultura
    /// invariante y selecciona el diámetro por elemento buscando el índice en el catálogo comercial
    /// (los ComboBox de diámetro se pueblan con <see cref="CatalogoBarras.DiametrosComercialesMm"/>).
    /// </summary>
    private void CargarEntrada(EntradaCalculoTanque e)
    {
        var g = e.Proyecto.Geometria;
        var m = e.Proyecto.Materiales;
        var p = e.Parametros;
        var ci = CultureInfo.InvariantCulture;

        TxtB.Text = g.BAnchoM.ToString("0.###", ci);
        TxtL.Text = g.LLargoM.ToString("0.###", ci);
        TxtHt.Text = g.HtAlturaM.ToString("0.###", ci);
        ChkConTapa.IsChecked = g.ConTapa;
        TxtEm.Text = g.EmEspesorMuroM.ToString("0.###", ci);
        TxtEf.Text = g.EfEspesorFondoM.ToString("0.###", ci);
        TxtEt.Text = g.ConTapa ? g.EtEspesorTapaM.ToString("0.###", ci) : "";
        TxtHL.Text = g.HLAlturaLiquidoM.ToString("0.###", ci);
        TxtHm.Text = g.HmAlturaSueloSobreMuroM.ToString("0.###", ci);
        TxtWext.Text = g.WextSobrecargaKNm2.ToString("0.###", ci);
        CmbTipoTanque.SelectedIndex = g.Tipo switch
        {
            TipoTanque.Superficial => 0,
            TipoTanque.EnterradoConNivelFreatico => 2,
            _ => 1
        };
        TxtAlturaNivelFreatico.Text = g.AlturaNivelFreaticoM is double nf ? nf.ToString("0.###", ci) : "";

        TxtFc.Text = m.FcMPa.ToString("0.###", ci);
        TxtFy.Text = m.FyMPa.ToString("0.###", ci);
        TxtGammaSuelo.Text = m.GammaSueloKNm3.ToString("0.###", ci);
        TxtGammaConcreto.Text = m.GammaConcretoKNm3.ToString("0.###", ci);
        TxtGammaLiquido.Text = m.GammaLiquidoKNm3.ToString("0.###", ci);
        TxtPhi.Text = m.PhiGradosAnguloFriccionSuelo.ToString("0.###", ci);
        TxtGammaSueloSaturado.Text = m.GammaSueloSaturadoKNm3 is double gss ? gss.ToString("0.###", ci) : "";

        TxtCvCubierta.Text = g.ConTapa ? p.CvCubiertaKNm2.ToString("0.###", ci) : "";
        TxtCgCubierta.Text = g.ConTapa ? p.CgCubiertaKNm2.ToString("0.###", ci) : "";
        TxtCvFondo.Text = p.CvFondoKNm2.ToString("0.###", ci);

        SeleccionarDiametro(CmbDiametroCubierta, p.DiametrosBarra.CubiertaMm);
        SeleccionarDiametro(CmbDiametroFondo, p.DiametrosBarra.FondoMm);
        SeleccionarDiametro(CmbDiametroMuroLong, p.DiametrosBarra.MuroLongitudinalMm);
        SeleccionarDiametro(CmbDiametroMuroTrans, p.DiametrosBarra.MuroTransversalMm);
        CmbMetodoInterpolacion.SelectedIndex = p.MetodoInterpolacion == MetodoInterpolacion.RedondearSuperior ? 1 : 0;
        ChkIncluirDiagramas.IsChecked = p.IncluirDiagramas;
        ChkIncluirSismo.IsChecked = p.Sismo is not null;

        if (p.Sismo is { } sismo)
        {
            TxtAa.Text = sismo.Espectro.Aa.ToString("0.###", ci);
            TxtAv.Text = sismo.Espectro.Av.ToString("0.###", ci);
            TxtFa.Text = sismo.Espectro.Fa.ToString("0.###", ci);
            TxtFv.Text = sismo.Espectro.Fv.ToString("0.###", ci);
            TxtI.Text = sismo.Espectro.I.ToString("0.###", ci);
            CmbCondicionBase.SelectedIndex = sismo.Espectro.CondicionBase == CondicionBaseMuro.Flexible ? 1 : 0;
            CmbCondicionAnclaje.SelectedIndex = sismo.Espectro.CondicionAnclaje switch
            {
                CondicionAnclajeBase.FlexibleAnclada => 0,
                CondicionAnclajeBase.NoAncladaOSinConfinar => 2,
                _ => 1
            };
            TxtKh.Text = sismo.Suelo.KhCoeficienteSismicoHorizontal.ToString("0.###", ci);
            TxtKv.Text = sismo.Suelo.KvCoeficienteSismicoVertical.ToString("0.###", ci);
            TxtDelta.Text = sismo.Suelo.DeltaGradosFriccionSueloMuro.ToString("0.###", ci);
            TxtIRelleno.Text = sismo.Suelo.IGradosInclinacionRelleno.ToString("0.###", ci);
            TxtBeta.Text = sismo.Suelo.BetaGradosInclinacionMuro.ToString("0.###", ci);
        }

        // Fase4 (2026-08-31): al cargar un proyecto, aplicar atenuación condicional y refrescar la
        // validación en vivo de todas las cajas (limpia marcas rojas obsoletas, valida lo cargado).
        ActualizarCamposCondicionales();
        RevalidarTodos();
    }

    private static void SeleccionarDiametro(ComboBox combo, double mm)
    {
        for (var i = 0; i < CatalogoBarras.DiametrosComercialesMm.Length; i++)
        {
            if (Math.Abs(CatalogoBarras.DiametrosComercialesMm[i] - mm) < 0.01)
            {
                combo.SelectedIndex = i;
                return;
            }
        }
    }

    /// <summary>
    /// Fase4 (2026-08-31): guarda el proyecto actual (los insumos de los controles de entrada, sin
    /// calcular) en un archivo JSON mediante <see cref="PersistenciaTanque.Serializar"/>.
    /// </summary>
    private async void BtnGuardarProyecto_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var entrada = LeerEntradaTanque();
            var json = PersistenciaTanque.Serializar(entrada);

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider is null)
            {
                TxtEstado.Foreground = Brushes.Firebrick;
                TxtEstado.Text = "No se pudo acceder al selector de archivos del sistema.";
                return;
            }

            var archivo = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Guardar proyecto (JSON)",
                SuggestedFileName = $"tanque-proyecto-{DateTime.Now:yyyy-MM-dd-HHmm}.json",
                DefaultExtension = "json",
                FileTypeChoices = new[] { new FilePickerFileType("JSON (*.json)") { Patterns = new[] { "*.json" } } }
            });
            if (archivo is null) return;

            await using var flujo = await archivo.OpenWriteAsync();
            await using var escritor = new StreamWriter(flujo, Encoding.UTF8);
            await escritor.WriteAsync(json);

            TxtEstado.Foreground = Brushes.DarkGreen;
            TxtEstado.Text = $"Proyecto guardado en \"{archivo.Name}\".";
        }
        catch (Exception ex)
        {
            TxtEstado.Foreground = Brushes.Firebrick;
            TxtEstado.Text = $"Error al guardar: {ex.Message}";
        }
    }

    /// <summary>
    /// Fase4 (2026-08-31): abre un proyecto JSON (<see cref="PersistenciaTanque.Deserializar"/>,
    /// que valida geometría/materiales/parámetros sísmicos) y rellena los controles de entrada.
    /// </summary>
    private async void BtnAbrirProyecto_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null)
        {
            TxtEstado.Foreground = Brushes.Firebrick;
            TxtEstado.Text = "No se pudo acceder al selector de archivos del sistema.";
            return;
        }

        IStorageFile? archivo;
        try
        {
            var elegidos = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Abrir proyecto (JSON)",
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("JSON (*.json)") { Patterns = new[] { "*.json" } } }
            });
            archivo = elegidos.FirstOrDefault();
        }
        catch (Exception ex)
        {
            TxtEstado.Foreground = Brushes.Firebrick;
            TxtEstado.Text = $"Error al abrir el selector de archivos: {ex.Message}";
            return;
        }

        if (archivo is null) return;

        try
        {
            string json;
            await using (var flujo = await archivo.OpenReadAsync())
            using (var lector = new StreamReader(flujo, Encoding.UTF8))
            {
                json = await lector.ReadToEndAsync();
            }

            var entrada = PersistenciaTanque.Deserializar(json);
            CargarEntrada(entrada);

            TxtEstado.Foreground = Brushes.DarkGreen;
            TxtEstado.Text = $"Proyecto cargado de \"{archivo.Name}\" -- presione Calcular.";
        }
        catch (Exception ex)
        {
            TxtEstado.Foreground = Brushes.Firebrick;
            TxtEstado.Text = $"Error al abrir: {ex.Message}";
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // FASE4 (2026-08-31) · ítem 1 (validación en vivo) y ítem 3 (campos condicionales).
    // La validación en vivo es ADVISORY: marca en rojo la caja al escribir, sin bloquear nada.
    // La autoridad final sigue siendo CalculadorTanque.Calcular → Geometria.Validar /
    // Materiales.Validar, que se invoca al presionar "Calcular" y reporta el error completo en la
    // barra de estado. Nada de aquí cambia un cálculo; solo anticipa el mismo error que el núcleo
    // reportaría (espejo de las mismas reglas, no una fuente nueva).
    // ─────────────────────────────────────────────────────────────────────────────
    private static bool ParseDouble(string texto, out double valor)
    {
        var t = (texto ?? string.Empty).Trim().Replace(',', '.');
        return double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out valor);
    }

    private void Vincular(TextBox caja, Func<string, string?> validador)
    {
        _validadores[caja] = validador;
        _tooltipsOriginales[caja] = ToolTip.GetTip(caja) as string;
        caja.TextChanged += (_, _) => Revalidar(caja);
    }

    private void Revalidar(TextBox caja)
    {
        if (!_validadores.TryGetValue(caja, out var validador)) return;
        MarcarInvalido(caja, validador(caja.Text ?? string.Empty));
    }

    private void RevalidarTodos()
    {
        foreach (var caja in _validadores.Keys.ToList()) Revalidar(caja);
    }

    private void MarcarInvalido(TextBox caja, string? error)
    {
        if (error is null)
        {
            caja.ClearValue(TextBox.BorderBrushProperty);
            caja.ClearValue(TextBox.BorderThicknessProperty);
            ToolTip.SetTip(caja, _tooltipsOriginales.GetValueOrDefault(caja));
        }
        else
        {
            caja.BorderBrush = new SolidColorBrush(Color.FromRgb(220, 38, 38)); // rojo #DC2626
            caja.BorderThickness = new Thickness(2);
            ToolTip.SetTip(caja, $"⚠ {error}");
        }
    }

    private TipoTanque TipoSeleccionado() => CmbTipoTanque.SelectedIndex switch
    {
        0 => TipoTanque.Superficial,
        2 => TipoTanque.EnterradoConNivelFreatico,
        _ => TipoTanque.EnterradoSinNivelFreatico
    };

    // Validadores por campo: espejo de las reglas de Geometria.Validar/Materiales.Validar.
    private static string? RequierePositivo(string texto)
    {
        if (!ParseDouble(texto, out var v)) return "Debe ser un número.";
        return v > 0 ? null : "Debe ser mayor que 0.";
    }

    private static string? RequiereNoNegativo(string texto)
    {
        if (!ParseDouble(texto, out var v)) return "Debe ser un número.";
        return v >= 0 ? null : "No puede ser negativo.";
    }

    private static string? RequiereVacioOPositivo(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null; // opcional en blanco
        return RequierePositivo(texto);
    }

    private string? ValidarEt(string texto)
    {
        if (ChkConTapa.IsChecked == true) return RequierePositivo(texto);
        if (string.IsNullOrWhiteSpace(texto)) return null;
        if (!ParseDouble(texto, out var v)) return "Debe ser un número.";
        return v == 0 ? null : "Sin tapa, et debe ser 0 (o dejarse en blanco).";
    }

    private string? ValidarHL(string texto)
    {
        if (!ParseDouble(texto, out var hl)) return "Debe ser un número.";
        if (hl <= 0) return "Debe ser mayor que 0.";
        if (ParseDouble(TxtHt.Text ?? "", out var ht) && hl > ht) return "HL no puede superar Ht.";
        return null;
    }

    private string? ValidarHm(string texto)
    {
        if (!ParseDouble(texto, out var hm)) return "Debe ser un número.";
        if (TipoSeleccionado() == TipoTanque.Superficial)
            return hm == 0 ? null : "Superficial exige Hm = 0.";
        return hm > 0 ? null : "Enterrado exige Hm > 0.";
    }

    private string? ValidarNivelFreatico(string texto)
    {
        if (TipoSeleccionado() != TipoTanque.EnterradoConNivelFreatico) return null; // no aplica
        if (!ParseDouble(texto, out var nf)) return "Debe ser un número.";
        if (nf <= 0) return "Debe ser mayor que 0.";
        if (ParseDouble(TxtHm.Text ?? "", out var hm) && nf > hm) return "No puede superar Hm.";
        return null;
    }

    private static string? ValidarPhi(string texto)
    {
        if (!ParseDouble(texto, out var phi)) return "Debe ser un número.";
        return phi > 0 && phi < 90 ? null : "Debe estar entre 0 y 90 (exclusivo).";
    }

    private void ConfigurarValidacionEnVivo()
    {
        Vincular(TxtB, RequierePositivo);
        Vincular(TxtL, RequierePositivo);
        Vincular(TxtHt, RequierePositivo);
        Vincular(TxtEm, RequierePositivo);
        Vincular(TxtEf, RequierePositivo);
        Vincular(TxtEt, ValidarEt);
        Vincular(TxtHL, ValidarHL);
        Vincular(TxtHm, ValidarHm);
        Vincular(TxtWext, RequiereNoNegativo);
        Vincular(TxtAlturaNivelFreatico, ValidarNivelFreatico);
        Vincular(TxtFc, RequierePositivo);
        Vincular(TxtFy, RequierePositivo);
        Vincular(TxtGammaSuelo, RequierePositivo);
        Vincular(TxtGammaConcreto, RequierePositivo);
        Vincular(TxtGammaLiquido, RequierePositivo);
        Vincular(TxtPhi, ValidarPhi);
        Vincular(TxtGammaSueloSaturado, RequiereVacioOPositivo);
        Vincular(TxtCvCubierta, RequiereNoNegativo);
        Vincular(TxtCgCubierta, RequiereNoNegativo);
        Vincular(TxtCvFondo, RequiereNoNegativo);
        Vincular(TxtAa, RequiereNoNegativo);
        Vincular(TxtAv, RequiereNoNegativo);
        Vincular(TxtFa, RequiereNoNegativo);
        Vincular(TxtFv, RequiereNoNegativo);
        Vincular(TxtI, RequierePositivo);
        Vincular(TxtKh, RequiereNoNegativo);
        Vincular(TxtKv, RequiereNoNegativo);
        Vincular(TxtDelta, RequiereNoNegativo);
        Vincular(TxtIRelleno, RequiereNoNegativo);
        Vincular(TxtBeta, RequiereNoNegativo);

        // Dependencias: al cambiar ConTapa / Tipo / IncluirSismo se revalidan los campos que
        // dependen de ellos y se atenúa/habilita lo pertinente.
        ChkConTapa.IsCheckedChanged += (_, _) =>
        {
            Revalidar(TxtEt);
            Revalidar(TxtCvCubierta);
            Revalidar(TxtCgCubierta);
            ActualizarCamposCondicionales();
        };
        CmbTipoTanque.SelectionChanged += (_, _) =>
        {
            Revalidar(TxtHm);
            Revalidar(TxtAlturaNivelFreatico);
            ActualizarCamposCondicionales();
        };
        ChkIncluirSismo.IsCheckedChanged += (_, _) => ActualizarCamposCondicionales();

        ActualizarCamposCondicionales();
    }

    private void ActualizarCamposCondicionales()
    {
        var conTapa = ChkConTapa.IsChecked == true;
        var conNivelFreatico = TipoSeleccionado() == TipoTanque.EnterradoConNivelFreatico;
        var conSismo = ChkIncluirSismo.IsChecked == true;

        // 1) Cubierta: et, CV/CG de cubierta y el diámetro de cubierta solo aplican con tapa.
        foreach (var c in new Control[] { TxtEt, TxtCvCubierta, TxtCgCubierta, CmbDiametroCubierta })
            EstablecerAplicabilidad(c, conTapa);

        // 2) Nivel freático: solo para Tipo = Enterrado con nivel freático.
        EstablecerAplicabilidad(TxtAlturaNivelFreatico, conNivelFreatico);

        // 3) Sismo: espectro (F.5) y dinámico de suelo (F.6) solo con sismo incluido.
        foreach (var c in new Control[]
        {
            TxtAa, TxtAv, TxtFa, TxtFv, TxtI, CmbCondicionBase, CmbCondicionAnclaje,
            TxtKh, TxtKv, TxtDelta, TxtIRelleno, TxtBeta
        })
            EstablecerAplicabilidad(c, conSismo);
    }

    private void EstablecerAplicabilidad(Control control, bool aplica)
    {
        control.IsEnabled = aplica;
        control.Opacity = aplica ? 1.0 : 0.5;
        if (!aplica && control is TextBox caja) MarcarInvalido(caja, null);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // FASE4 (2026-08-31) · ítem 5: colapsar/expandir los Expander de "Datos de entrada".
    // ─────────────────────────────────────────────────────────────────────────────
    private void BtnExpandirTodo_Click(object? sender, RoutedEventArgs e) => EstablecerExpandidos(true);

    private void BtnColapsarTodo_Click(object? sender, RoutedEventArgs e) => EstablecerExpandidos(false);

    /// <summary>
    /// Abre el diálogo "Acerca de / Aviso legal" (Descargo de Responsabilidad + Términos y
    /// Condiciones de Uso), accesible desde el botón permanente "Ayuda / Acerca de..." de la
    /// cabecera y desde "Acerca de..." de la pestaña "Datos de entrada". Información
    /// legal/informática de presentación: sin efecto en el cálculo.
    /// </summary>
    private void BtnAcercaDe_Click(object? sender, RoutedEventArgs e)
    {
        _ = new AcercaDeWindow().ShowDialog(this);
    }

    private void EstablecerExpandidos(bool expandido)
    {
        foreach (var exp in ExpandidoresEntrada()) exp.IsExpanded = expandido;
    }

    private IEnumerable<Expander> ExpandidoresEntrada() => new[]
    {
        ExpGeometria, ExpMateriales, ExpCargas, ExpSismo, ExpDinamico, ExpDetallado
    };

    // ─────────────────────────────────────────────────────────────────────────────
    // FASE 5 (2026-08-31) — accesibilidad: nombres estables y legibles para UI
    // Automation y lectores de pantalla (AutomationProperties.Name). Solo
    // presentación; no afecta cálculo ni verificación.
    // ─────────────────────────────────────────────────────────────────────────────
    private void ConfigurarAccesibilidad()
    {
        // Geometría y tipo de tanque
        Nombrar(TxtB, "Ancho del tanque (B), m");
        Nombrar(TxtL, "Largo del tanque (L), m");
        Nombrar(TxtHt, "Altura total del muro (Ht), m");
        Nombrar(ChkConTapa, "El tanque tiene placa de cubierta (tapa)");
        Nombrar(TxtEm, "Espesor de muro (em), m");
        Nombrar(TxtEf, "Espesor de placa de fondo (ef), m");
        Nombrar(TxtEt, "Espesor de placa de cubierta (et), m");
        Nombrar(TxtHL, "Altura de la lámina de líquido (HL), m");
        Nombrar(TxtHm, "Altura de suelo sobre el muro (Hm), m");
        Nombrar(TxtWext, "Sobrecarga en superficie (Wext), kN/m²");
        Nombrar(CmbTipoTanque, "Tipo de tanque");
        Nombrar(TxtAlturaNivelFreatico, "Altura del nivel freático, m");

        // Materiales
        Nombrar(TxtFc, "Resistencia del concreto (f'c), MPa");
        Nombrar(TxtFy, "Fluencia del acero (fy), MPa");
        Nombrar(TxtGammaSuelo, "Peso unitario del suelo (γsuelo), kN/m³");
        Nombrar(TxtGammaSueloSaturado, "Peso unitario saturado del suelo (γsuelo,sat), kN/m³");
        Nombrar(TxtGammaConcreto, "Peso unitario del concreto (γconcreto), kN/m³");
        Nombrar(TxtGammaLiquido, "Peso unitario del líquido (γlíquido), kN/m³");
        Nombrar(TxtPhi, "Ángulo de fricción del suelo (φ), grados");

        // Cargas
        Nombrar(TxtCvCubierta, "Carga viva de cubierta (CV), kN/m²");
        Nombrar(TxtCgCubierta, "Carga adicional de cubierta (CG), kN/m²");
        Nombrar(TxtCvFondo, "Carga viva sobre fondo (CV), kN/m²");

        // Sismo
        Nombrar(ChkIncluirSismo, "Incluir análisis sísmico");
        Nombrar(TxtAa, "Aa, aceleración pico efectiva");
        Nombrar(TxtAv, "Av, velocidad pico efectiva");
        Nombrar(TxtFa, "Fa, coeficiente de sitio");
        Nombrar(TxtFv, "Fv, coeficiente de sitio");
        Nombrar(TxtI, "I, coeficiente de importancia");
        Nombrar(CmbCondicionBase, "Condición de la base (hi/hc)");
        Nombrar(CmbCondicionAnclaje, "Condición de anclaje (Ri/Rc)");

        // Dinámico
        Nombrar(TxtKh, "kh, coeficiente sísmico horizontal");
        Nombrar(TxtKv, "kv, coeficiente sísmico vertical");
        Nombrar(TxtDelta, "δ, fricción suelo-muro, grados");
        Nombrar(TxtIRelleno, "i, inclinación del relleno, grados");
        Nombrar(TxtBeta, "β, inclinación del muro, grados");

        // Detallado y armaduras
        Nombrar(CmbDiametroCubierta, "Diámetro de barra de cubierta");
        Nombrar(CmbDiametroFondo, "Diámetro de barra de fondo");
        Nombrar(CmbDiametroMuroLong, "Diámetro de barra de muro longitudinal");
        Nombrar(CmbDiametroMuroTrans, "Diámetro de barra de muro transversal");
        Nombrar(CmbMetodoInterpolacion, "Método de coeficientes PCA fuera de r tabulado");
        Nombrar(ChkIncluirDiagramas, "Incluir diagramas de momento por celda");

        // Expanders y acciones
        Nombrar(ExpGeometria, "Geometría y tipo de tanque");
        Nombrar(ExpMateriales, "Materiales");
        Nombrar(ExpCargas, "Cargas de cubierta y fondo");
        Nombrar(ExpSismo, "Sismo, espectro de diseño");
        Nombrar(ExpDinamico, "Dinámico, fuerza dinámica de suelo");
        Nombrar(ExpDetallado, "Detallado y armaduras");
        Nombrar(BtnCalcular, "Calcular");
        Nombrar(BtnExportar, "Exportar informe (.txt)");
        Nombrar(BtnExportarHtml, "Exportar HTML");
        Nombrar(BtnExportarCsv, "Exportar CSV");
        Nombrar(BtnGuardarProyecto, "Guardar proyecto");
        Nombrar(BtnAbrirProyecto, "Abrir proyecto");
        Nombrar(BtnExpandirTodo, "Expandir todo");
        Nombrar(BtnColapsarTodo, "Colapsar todo");
    }

    private static void Nombrar(Control control, string nombre) => AutomationProperties.SetName(control, nombre);
}
