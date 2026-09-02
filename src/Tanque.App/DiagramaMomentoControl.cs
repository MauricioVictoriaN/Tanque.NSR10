// ---------------------------------------------------------------------------
// Tanque.NSR10
// (c) Mauricio Javier Victoria Niño <hidratecsa@gmail.com> · CC BY-NC-SA 4.0
// Uso exclusivamente académico. El motor de cálculo (Tanque.Core) se distribuye
// como binario ofuscado, protegido por derechos de autor (ver LICENSE).
// ---------------------------------------------------------------------------
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Tanque.Core.Modulos;

namespace Tanque.App;

/// <summary>
/// Fase 2 del frente de interfaz (2026-08-30): dibuja una <see cref="CurvaMomento"/> (faja
/// gobernante de momento) como un diagrama de líneas -- marco, línea de cero y polilínea de la
/// curva -- usando solo primitivas de <see cref="DrawingContext"/> (<see cref="DrawingContext.DrawLine"/>),
/// sin librerías externas. Los textos (título, valor pico, unidades) los agrega el code-behind como
/// <see cref="TextBlock"/>, para mantener este control puramente gráfico. No calcula nada: recibe
/// los puntos ya extraídos por <see cref="DiagramaMomento.Calcular"/> (re-muestreo del campo Marcus
/// ya verificado, principio rector: sin fórmula ni valor nuevo).
/// </summary>
public sealed class DiagramaMomentoControl : Control
{
    private CurvaMomento? _curva;

    public CurvaMomento? Curva
    {
        get => _curva;
        set
        {
            _curva = value;
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_curva is null || _curva.Puntos.Count < 2) return;

        var b = Bounds;
        const double pad = 8.0;
        var plotL = pad;
        var plotR = b.Width - pad;
        var plotT = pad;
        var plotB = b.Height - pad;
        if (plotR <= plotL || plotB <= plotT) return;

        var puntos = _curva.Puntos;
        double mMin = 0.0, mMax = 0.0;
        foreach (var p in puntos)
        {
            mMin = Math.Min(mMin, p.MomentoKNmM);
            mMax = Math.Max(mMax, p.MomentoKNmM);
        }
        if (mMax - mMin < 1e-9) { mMin -= 1.0; mMax += 1.0; }

        var luz = _curva.LuzM;
        double X(double pos) => plotL + (luz <= 0 ? 0 : pos / luz) * (plotR - plotL);
        double Y(double m) => plotB - (m - mMin) / (mMax - mMin) * (plotB - plotT);

        var marco = new Pen(Brushes.LightGray, 1);
        var cero = new Pen(Brushes.LightGray, 1);
        var curvaPen = new Pen(Brushes.SteelBlue, 2);

        // Marco (4 líneas, para no depender de la sobrecarga DrawRectangle).
        context.DrawLine(marco, new Point(plotL, plotT), new Point(plotR, plotT));
        context.DrawLine(marco, new Point(plotR, plotT), new Point(plotR, plotB));
        context.DrawLine(marco, new Point(plotR, plotB), new Point(plotL, plotB));
        context.DrawLine(marco, new Point(plotL, plotB), new Point(plotL, plotT));

        // Línea de cero (referencia de sagging/hogging).
        var ceroY = Y(0.0);
        context.DrawLine(cero, new Point(plotL, ceroY), new Point(plotR, ceroY));

        // Curva (polilínea por segmentos).
        for (var i = 1; i < puntos.Count; i++)
        {
            var a = new Point(X(puntos[i - 1].PosicionM), Y(puntos[i - 1].MomentoKNmM));
            var b2 = new Point(X(puntos[i].PosicionM), Y(puntos[i].MomentoKNmM));
            context.DrawLine(curvaPen, a, b2);
        }
    }
}
