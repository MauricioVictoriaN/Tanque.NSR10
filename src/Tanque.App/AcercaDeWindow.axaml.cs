// ---------------------------------------------------------------------------
// Tanque.NSR10
// (c) Mauricio Javier Victoria Niño <hidratecsa@gmail.com> · CC BY-NC-SA 4.0
// Uso exclusivamente académico. El motor de cálculo (Tanque.Core) se distribuye
// como binario ofuscado, protegido por derechos de autor (ver LICENSE).
// ---------------------------------------------------------------------------
using Avalonia.Controls;
using Avalonia.Interactivity;
using Tanque.Reportes;

namespace Tanque.App;

/// <summary>
/// Di\u00e1logo "Acerca de / Aviso legal" del programa. Muestra la identidad del desarrollador y el
/// Descargo de Responsabilidad + T\u00e9rminos y Condiciones de Uso (EULA), accesible de forma
/// permanente desde "Ayuda / Acerca de" (cabecera) y desde la pesta\u00f1a "Datos de entrada".
/// Informaci\u00f3n legal/inform\u00e1tica de presentaci\u00f3n: no participa en el c\u00e1lculo del n\u00facleo.
/// </summary>
public partial class AcercaDeWindow : Window
{
    public AcercaDeWindow()
    {
        InitializeComponent();
        TxtAcercaDe.Text =
            $"Tanque.NSR10 — Análisis y diseño de tanques rectangulares de concreto reforzado " +
            $"(NSR-10 / ACI 350 / manual PCA). Prototipo académico.\n" +
            $"Autor / Desarrollador: {IdentidadDesarrollador.Nombre} · {IdentidadDesarrollador.Afiliacion} · " +
            $"{IdentidadDesarrollador.Contacto} · ORCID: {IdentidadDesarrollador.Orcid}";
        TxtTitulo.Text = DisclaimerAndEula.Title;
        TxtDescargo.Text = DisclaimerAndEula.Text;
    }

    private void BtnAceptar_Click(object? sender, RoutedEventArgs e) => Close();
}
