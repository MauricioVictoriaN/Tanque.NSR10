// ---------------------------------------------------------------------------
// Tanque.NSR10
// (c) Mauricio Javier Victoria Niño <hidratecsa@gmail.com> · CC BY-NC-SA 4.0
// Uso exclusivamente académico. El motor de cálculo (Tanque.Core) se distribuye
// como binario ofuscado, protegido por derechos de autor (ver LICENSE).
// ---------------------------------------------------------------------------
using System;
using Tanque.Core.Dominio;
using Tanque.Core.Modulos;
using Xunit;

namespace Tanque.Core.Tests;

/// <summary>
/// Pruebas espejo de la sección "Persistencia JSON" de tools/Tanque.Core.Verificacion (Fase4 del
/// frente de interfaz, 2026-08-31) -- round-trip de <see cref="PersistenciaTanque"/> con el
/// escenario completo (tapa + sismo + nivel freático), JSON legible (enums como cadenas, sin la
/// propiedad derivada <c>RelacionLadosR</c>) y rechazo de JSON inválido. Cero dependencias
/// (System.Text.Json del runtime).
/// </summary>
public class PersistenciaTanqueTests
{
    private static EntradaCalculoTanque EntradaBase() => new(
        "Tanque de prueba",
        new ProyectoTanque(
            new Geometria(
                BAnchoM: 6.0, LLargoM: 8.0, HtAlturaM: 4.5, ConTapa: true,
                EmEspesorMuroM: 0.3, EfEspesorFondoM: 0.35, EtEspesorTapaM: 0.2,
                HLAlturaLiquidoM: 4.0, HmAlturaSueloSobreMuroM: 3.0, WextSobrecargaKNm2: 0.0,
                Tipo: TipoTanque.EnterradoConNivelFreatico, AlturaNivelFreaticoM: 1.0),
            new Materiales(
                FcMPa: 21, FyMPa: 420, GammaSueloKNm3: 16, GammaConcretoKNm3: 24, GammaLiquidoKNm3: 9.81,
                PhiGradosAnguloFriccionSuelo: 30, GammaSueloSaturadoKNm3: 18)),
        new ParametrosCalculoTanque(
            CvCubiertaKNm2: 1.0, CgCubiertaKNm2: 0.5, CvFondoKNm2: 2.0,
            DiametrosBarra: new DiametrosBarraCalculo(15.9, 15.9, 15.9, 15.9),
            MetodoInterpolacion: MetodoInterpolacion.Interpolar,
            IncluirDiagramas: true,
            Sismo: new ParametrosSismoCalculo(
                new ParametrosEspectroDiseno(Aa: 0.25, Av: 0.25, Fa: 1.2, Fv: 1.4, I: 1.25,
                    CondicionBaseMuro.Rigida, CondicionAnclajeBase.FlexibleAnclada),
                new ParametrosSueloDinamico(KhCoeficienteSismicoHorizontal: 0.10, KvCoeficienteSismicoVertical: 0.0,
                    DeltaGradosFriccionSueloMuro: 0.0, IGradosInclinacionRelleno: 0.0, BetaGradosInclinacionMuro: 90.0))));

    [Fact]
    public void RoundTrip_ReproduceEntradaCompleta()
    {
        var entrada = EntradaBase();
        var vuelta = PersistenciaTanque.Deserializar(PersistenciaTanque.Serializar(entrada));

        Assert.Equal(entrada.NombreProyecto, vuelta.NombreProyecto);
        Assert.Equal(entrada.Proyecto.Geometria, vuelta.Proyecto.Geometria);
        Assert.Equal(entrada.Proyecto.Materiales, vuelta.Proyecto.Materiales);
        Assert.Equal(entrada.Parametros, vuelta.Parametros);
        Assert.Equal(entrada, vuelta);
    }

    [Fact]
    public void JsonLegible_EnumsCadenas_SinRelacionLadosR()
    {
        var json = PersistenciaTanque.Serializar(EntradaBase());

        Assert.Contains("\"EnterradoConNivelFreatico\"", json);
        Assert.Contains("\"Interpolar\"", json);
        Assert.Contains("\"Rigida\"", json);
        Assert.Contains("\"FlexibleAnclada\"", json);
        Assert.DoesNotContain("RelacionLadosR", json);
    }

    [Fact]
    public void JsonInvalido_Lanza()
    {
        Assert.Throws<ArgumentException>(() => PersistenciaTanque.Deserializar("esto no es json"));
        Assert.Throws<ArgumentException>(() => PersistenciaTanque.Deserializar(""));
    }
}
