// ---------------------------------------------------------------------------
// Tanque.NSR10
// (c) Mauricio Javier Victoria Niño <hidratecsa@gmail.com> · CC BY-NC-SA 4.0
// Uso exclusivamente académico. El motor de cálculo (Tanque.Core) se distribuye
// como binario ofuscado, protegido por derechos de autor (ver LICENSE).
// ---------------------------------------------------------------------------
using Tanque.Core.Dominio;
using Tanque.Core.Modulos;
using Xunit;

namespace Tanque.Core.Tests;

/// <summary>
/// Pruebas espejo de la sección "Diagramas de momento y cortante (Fase 2 del frente de interfaz)" de
/// tools/Tanque.Core.Verificacion/Program.cs (789/789 aserciones al momento de escribir este archivo).
/// El módulo <see cref="DiagramaMomento"/> no calcula nada nuevo: re-muestrea el campo Marcus ya
/// verificado a lo largo de la faja gobernante (envolvente con signo) y expone el cortante gobernante
/// puntual del diseño. Ver Modulos/DiagramaMomento.cs.
/// </summary>
public class DiagramaMomentoTests
{
    private static Geometria GeoBase() => new(
        BAnchoM: 6.0, LLargoM: 8.0, HtAlturaM: 4.5, ConTapa: true,
        EmEspesorMuroM: 0.3, EfEspesorFondoM: 0.35, EtEspesorTapaM: 0.2,
        HLAlturaLiquidoM: 4.0, HmAlturaSueloSobreMuroM: 3.0, WextSobrecargaKNm2: 0.0);

    private static Materiales MatBase() => new(
        FcMPa: 21, FyMPa: 420, GammaConcretoKNm3: 24, GammaLiquidoKNm3: 9.81,
        GammaSueloKNm3: 16, PhiGradosAnguloFriccionSuelo: 30, GammaSueloSaturadoKNm3: 18);

    private static ParametrosCalculoTanque ParametrosBase() => new(
        CvCubiertaKNm2: 0.0, CgCubiertaKNm2: 0.0, CvFondoKNm2: 0.0,
        DiametrosBarra: new DiametrosBarraCalculo(
            CatalogoBarras.DiametroPredeterminadoBarraMm, CatalogoBarras.DiametroPredeterminadoBarraMm,
            CatalogoBarras.DiametroPredeterminadoBarraMm, CatalogoBarras.DiametroPredeterminadoBarraMm),
        MetodoInterpolacion: MetodoInterpolacion.Interpolar,
        IncluirDiagramas: false,
        Sismo: null);

    private static void AssertCerca(double esperado, double actual, string mensaje)
        => Assert.True(Math.Abs(esperado - actual) < 1e-6, $"{mensaje}: esperado={esperado}, actual={actual}");

    [Fact]
    public void DiagramaMomento_FajaGobernante_PicoIgualGobernante_Y_Estructura()
    {
        var geo = GeoBase() with { Tipo = TipoTanque.EnterradoConNivelFreatico, AlturaNivelFreaticoM = 1.0 };
        var resultado = CalculadorTanque.Calcular(new ProyectoTanque(geo, MatBase()), ParametrosBase());
        var d = DiagramaMomento.Calcular(resultado);

        // (a) Conjunto exacto: cubierta(2) + fondo/envolvente(2) + muro long interior/exterior(4)
        //     + muro trans interior/exterior(4) = 12 curvas.
        Assert.Equal(12, d.Curvas.Count);
        Assert.Equal(10, d.Cortantes.Count);

        // (b) Pico de cada curva == gobernante del campo fuente (max(pos, neg)).
        var cub = resultado.PlacaCubierta!;
        var env = resultado.CamposEnvolventeFondo!;
        var mlInt = resultado.MuroLongitudinalEstatico.Interior;
        var mlExt = resultado.MuroLongitudinalEstatico.Exterior!;
        var mtInt = resultado.MuroTransversalEstatico.Interior;
        var mtExt = resultado.MuroTransversalEstatico.Exterior!;

        double Pico(CurvaMomento c) => c.Puntos.Max(p => Math.Abs(p.MomentoKNmM));
        double PicoCampo(double[,] campo) { double m = 0; for (var f = 0; f < campo.GetLength(0); f++) for (var c = 0; c < campo.GetLength(1); c++) m = Math.Max(m, Math.Abs(campo[f, c])); return m; }
        CurvaMomento Curva(string e, string dir, string cond) => d.Curvas.First(c => c.Elemento == e && c.Direccion == dir && c.Condicion == cond);

        AssertCerca(Math.Max(cub.MxPosGobernanteKNmM, cub.MxNegGobernanteKNmM), Pico(Curva("Cubierta", "Mx", "")), "Cubierta Mx");
        AssertCerca(Math.Max(cub.MyPosGobernanteKNmM, cub.MyNegGobernanteKNmM), Pico(Curva("Cubierta", "My", "")), "Cubierta My");
        AssertCerca(Math.Max(PicoCampo(env.CampoMxCaraInferior), PicoCampo(env.CampoMxCaraSuperior)), Pico(Curva("Fondo", "Mx", "Envolvente final")), "Fondo Mx envolvente");
        AssertCerca(Math.Max(mlInt.MxPosGobernanteKNmM, mlInt.MxNegGobernanteKNmM), Pico(Curva("Muro longitudinal", "Mx", "Interior")), "Muro long Mx interior");
        AssertCerca(Math.Max(mlInt.MyPosGobernanteKNmM, mlInt.MyNegGobernanteKNmM), Pico(Curva("Muro longitudinal", "My", "Interior")), "Muro long My interior");
        AssertCerca(Math.Max(mlExt.MxPosGobernanteKNmM, mlExt.MxNegGobernanteKNmM), Pico(Curva("Muro longitudinal", "Mx", "Exterior")), "Muro long Mx exterior");
        AssertCerca(Math.Max(mtInt.MxPosGobernanteKNmM, mtInt.MxNegGobernanteKNmM), Pico(Curva("Muro transversal", "Mx", "Interior")), "Muro trans Mx interior");
        AssertCerca(Math.Max(mtExt.MyPosGobernanteKNmM, mtExt.MyNegGobernanteKNmM), Pico(Curva("Muro transversal", "My", "Exterior")), "Muro trans My exterior");

        // (c) Estructura: muro Mx = 11 puntos con Luz = HL; muro My = 6 puntos con Luz = 0.5·(L−em)
        //     (luz eje a eje, PCA pág. 173); placa = 6 puntos con Luz = 0.5·(a−em). Posiciones 0..Luz.
        var mlMxInt = Curva("Muro longitudinal", "Mx", "Interior");
        var mlMyInt = Curva("Muro longitudinal", "My", "Interior");
        var cubMx = Curva("Cubierta", "Mx", "");
        Assert.Equal(11, mlMxInt.Puntos.Count);
        AssertCerca(geo.HLAlturaLiquidoM, mlMxInt.LuzM, "Luz muro Mx interior");
        Assert.Equal(6, mlMyInt.Puntos.Count);
        AssertCerca(0.5 * (geo.LLargoM - geo.EmEspesorMuroM), mlMyInt.LuzM, "Luz muro My interior");
        Assert.Equal(6, cubMx.Puntos.Count);
        AssertCerca(0.5 * (geo.LLargoM - geo.EmEspesorMuroM), cubMx.LuzM, "Luz cubierta Mx");
        AssertCerca(0.0, mlMxInt.Puntos[0].PosicionM, "Posición inicial");
        AssertCerca(mlMxInt.LuzM, mlMxInt.Puntos[^1].PosicionM, "Posición final");

        // (d) Cortantes gobernantes con cruce contra el diseño.
        Assert.Equal(resultado.DisenoCubierta!.CortanteX.VuKN, d.Cortantes.First(c => c.Elemento == "Cubierta" && c.Ubicacion == "Borde 'a'").VuKNm);
        Assert.Equal(resultado.DisenoMuroLongitudinal.CortanteFondo.VuKN, d.Cortantes.First(c => c.Elemento == "Muro longitudinal" && c.Ubicacion == "Fondo").VuKNm);
        Assert.Equal(resultado.EnvolventeFondo!.CortanteX.VuKN, d.Cortantes.First(c => c.Elemento == "Fondo" && c.Ubicacion == "Borde 'a'").VuKNm);
    }

    [Fact]
    public void DiagramaMomento_SinTapaSuperficial_SinCubiertaNiExterior()
    {
        // Tanque superficial sin tapa: no hay cubierta, no hay condición exterior (Hm=0) y no hay
        // envolvente de fondo (no hay subpresión) → solo fondo gravitacional + muros (interior).
        var geo = new Geometria(
            BAnchoM: 3.0, LLargoM: 4.0, HtAlturaM: 2.5, ConTapa: false,
            EmEspesorMuroM: 0.2, EfEspesorFondoM: 0.2, EtEspesorTapaM: 0.0,
            HLAlturaLiquidoM: 2.0, HmAlturaSueloSobreMuroM: 0.0, WextSobrecargaKNm2: 0.0,
            Tipo: TipoTanque.Superficial);
        var resultado = CalculadorTanque.Calcular(new ProyectoTanque(geo, MatBase()), ParametrosBase());
        var d = DiagramaMomento.Calcular(resultado);

        Assert.DoesNotContain(d.Curvas, c => c.Elemento == "Cubierta");
        Assert.DoesNotContain(d.Curvas, c => c.Condicion == "Exterior");
        Assert.DoesNotContain(d.Curvas, c => c.Condicion == "Envolvente final");
        Assert.Contains(d.Curvas, c => c.Elemento == "Fondo" && c.Condicion == "");
        Assert.Contains(d.Curvas, c => c.Elemento == "Muro longitudinal" && c.Direccion == "Mx" && c.Condicion == "Interior");
        Assert.Contains(d.Curvas, c => c.Elemento == "Muro transversal" && c.Direccion == "My" && c.Condicion == "Interior");
    }

    [Fact]
    public void DiagramaMomento_Campos_MapaDeCalor_CoincidenConFuente()
    {
        var geo = GeoBase() with { Tipo = TipoTanque.EnterradoConNivelFreatico, AlturaNivelFreaticoM = 1.0 };
        var resultado = CalculadorTanque.Calcular(new ProyectoTanque(geo, MatBase()), ParametrosBase());
        var d = DiagramaMomento.Calcular(resultado);

        // (e) 24 campos para el escenario completo (cubierta4 + fondo/envolvente4 + muros interior/exterior 8×2).
        Assert.Equal(24, d.Campos.Count);

        var cub = resultado.PlacaCubierta!;
        var env = resultado.CamposEnvolventeFondo!;
        var mlInt = resultado.MuroLongitudinalEstatico.Interior;

        CampoMomento Campo(string e, string dir, string cara, string cond)
            => d.Campos.First(c => c.Elemento == e && c.Direccion == dir && c.Cara == cara && c.Condicion == cond);

        // (f) Copia fiel: la grilla de cada campo es la MISMA que su fuente (la cara superior de la
        //     envolvente se niega para reconstruir el signo hogging).
        void Igual(double[,] a, double[,] b)
        {
            Assert.Equal(a.GetLength(0), b.GetLength(0));
            Assert.Equal(a.GetLength(1), b.GetLength(1));
            for (var f = 0; f < a.GetLength(0); f++)
                for (var c = 0; c < a.GetLength(1); c++)
                    AssertCerca(a[f, c], b[f, c], "celda");
        }

        Igual(cub.CampoMxPos, Campo("Cubierta", "Mx", "Cara inferior", "").Valores);
        Igual(cub.CampoMxNeg, Campo("Cubierta", "Mx", "Cara superior", "").Valores);
        Igual(cub.CampoMyPos, Campo("Cubierta", "My", "Cara inferior", "").Valores);
        Igual(cub.CampoMyNeg, Campo("Cubierta", "My", "Cara superior", "").Valores);
        Igual(env.CampoMxCaraInferior, Campo("Fondo", "Mx", "Cara inferior", "Envolvente final").Valores);

        var mxSup = Campo("Fondo", "Mx", "Cara superior", "Envolvente final").Valores;
        Assert.Equal(env.CampoMxCaraSuperior.GetLength(0), mxSup.GetLength(0));
        Assert.Equal(env.CampoMxCaraSuperior.GetLength(1), mxSup.GetLength(1));
        for (var f = 0; f < env.CampoMxCaraSuperior.GetLength(0); f++)
            for (var c = 0; c < env.CampoMxCaraSuperior.GetLength(1); c++)
                AssertCerca(-env.CampoMxCaraSuperior[f, c], mxSup[f, c], "celda negada");

        Igual(mlInt.CampoMxPos, Campo("Muro longitudinal", "Mx", "Cara interior", "Interior").Valores);
        Igual(mlInt.CampoMyNeg, Campo("Muro longitudinal", "My", "Cara exterior", "Interior").Valores);

        // (g) Estructura de grilla: muro 11×6, placa 6×6; luces correctas.
        var muroMx = Campo("Muro longitudinal", "Mx", "Cara interior", "Interior");
        var placaMx = Campo("Cubierta", "Mx", "Cara inferior", "");
        Assert.Equal(11, muroMx.Valores.GetLength(0));
        Assert.Equal(6, muroMx.Valores.GetLength(1));
        AssertCerca(geo.HLAlturaLiquidoM, muroMx.LuzFilasM, "LuzFilas muro");
        AssertCerca(0.5 * (geo.LLargoM - geo.EmEspesorMuroM), muroMx.LuzColumnasM, "LuzColumnas muro");
        Assert.Equal(6, placaMx.Valores.GetLength(0));
        Assert.Equal(6, placaMx.Valores.GetLength(1));
        AssertCerca(0.5 * (geo.LLargoM - geo.EmEspesorMuroM), placaMx.LuzFilasM, "LuzFilas placa");
    }
}
