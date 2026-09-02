// ---------------------------------------------------------------------------
// Tanque.NSR10
// (c) Mauricio Javier Victoria Niño <hidratecsa@gmail.com> · CC BY-NC-SA 4.0
// Uso exclusivamente académico. El motor de cálculo (Tanque.Core) se distribuye
// como binario ofuscado, protegido por derechos de autor (ver LICENSE).
// ---------------------------------------------------------------------------
using Tanque.Core.Modulos;
using Xunit;

namespace Tanque.Core.Tests;

/// <summary>
/// Pruebas del módulo de Diseño a Flexión, Cortante y Control de Fisuración (F.12-14), contra
/// las Tablas 71, 74 y 75 del ejercicio 2 de la tesis. A diferencia de los demás módulos, este
/// no recibe un <see cref="Tanque.Core.Dominio.ProyectoTanque"/> -- opera sobre parámetros
/// explícitos (Mu, Vu, Ms, d, b, s, h) porque el mapeo "espesor de Geometria -> d efectivo" del
/// programa original no reconcilia limpiamente con las tablas (ver discrepancia documentada en
/// el docstring de <see cref="DisenoFlexionCortanteFisuracion"/>). Cada caso de prueba usa un
/// "d" justificado explícitamente en su comentario, tal como haría el futuro módulo de
/// placas/muros al invocar esta clase.
/// </summary>
public class DisenoFlexionCortanteFisuracionTests
{
    private const double Fc = 28.0, Fy = 420.0;

    [Fact]
    public void VerificarCortante_PlacaDeFondo_D0175m_CoincideConTabla71()
    {
        // d=0.175m confirmado de forma independiente por el campo "d_mm"=175 de la propia
        // Tabla 75 (control de fisuración) -- verificación no circular.
        var r = DisenoFlexionCortanteFisuracion.VerificarCortante(vuKN: 83.86, dM: 0.175, bM: 1.0, fcMPa: Fc);
        AssertTol("Vc placa de fondo", r.VcKN, 118.06, atol: 0.05);
        Assert.True(r.Cumple);
    }

    [Fact]
    public void VerificarCortante_Tapa_ConDDeFlexion_NoCoincideConTabla71_DiscrepanciaAbierta()
    {
        // Con el d=0.075m que la propia Tabla 74 declara para flexión, Vc NO coincide con el
        // Vc publicado en la Tabla 71 (67.46 kN) -- discrepancia abierta, documentada en el
        // docstring de la clase (el d de cortante y el d de flexión de este mismo elemento no
        // reconcilian). Se verifica el valor que la fórmula SÍ produce con ese d.
        var r = DisenoFlexionCortanteFisuracion.VerificarCortante(vuKN: 28.32, dM: 0.075, bM: 1.0, fcMPa: Fc);
        AssertTol("Vc tapa (d=0.075m de Tabla74, NO coincide con Tabla71=67.46)", r.VcKN, 50.60, atol: 0.05);
    }

    [Fact]
    public void VerificarCortante_Tapa_ConDImplicitoDeTabla71_SiReconcilia()
    {
        // Con d=0.10m (el que el Vc=67.46 kN publicado implica algebraicamente) sí reconcilia.
        var r = DisenoFlexionCortanteFisuracion.VerificarCortante(vuKN: 28.32, dM: 0.10, bM: 1.0, fcMPa: Fc);
        AssertTol("Vc tapa (d=0.10m implícito de Tabla71)", r.VcKN, 67.46, atol: 0.05);
        Assert.True(r.Cumple);
    }

    [Theory]
    [InlineData(85.72)] // muro longitudinal
    [InlineData(82.22)] // muro transversal
    public void VerificarCortante_Muros_ConDInferido_CoincideConTabla71(double vuKN)
    {
        // d=0.15m inferido algebraicamente del Vc=101.2 kN publicado (ambos muros comparten el
        // mismo Vc en la Tabla 71). No hay tabla de control de fisuración para muros en este
        // ejercicio que permita confirmar ese d de forma independiente, a diferencia de la
        // placa de fondo -- se deja constancia de que este valor es inferido, no confirmado.
        var r = DisenoFlexionCortanteFisuracion.VerificarCortante(vuKN: vuKN, dM: 0.15, bM: 1.0, fcMPa: Fc);
        AssertTol("Vc muro (d=0.15m inferido)", r.VcKN, 101.2, atol: 0.05);
        Assert.True(r.Cumple);
    }

    [Fact]
    public void VerificarControlFisuracion_Tabla74_Longitudinal_PrimeraYSegundaPasada()
    {
        // n, rho, As, d, s, h, Mu, Ms dados directamente por la Tabla 74 (no derivados de un
        // espesor) -- verificación no circular de CalcularN/CalcularKJ/CalcularFs/CalcularFsAdmisible.
        var cf = DisenoFlexionCortanteFisuracion.VerificarControlFisuracion(
            msKNm: 12.04, asMm2: 796.0, rho: 0.0086, esMPa: 200000.0, ecMPa: 24870.0,
            dM: 0.075, sM: 0.2, hM: 0.25);
        AssertTol("n", cf.N, 8.04, atol: 0.01);
        AssertTol("k", cf.K, 0.3094, atol: 0.002);
        AssertTol("j", cf.J, 0.8969, atol: 0.001);
        AssertTol("fs", cf.FsMPa, 224.79, atol: 0.15);
        AssertTol("fs,adm", cf.FsAdmisibleMPa, 179.02, atol: 0.02);
        Assert.False(cf.Cumple); // requiere_rediseno=true en la tabla

        var gamma = DisenoFlexionCortanteFisuracion.CalcularGamma(16.85, 12.04);
        AssertTol("gamma", gamma, 1.4, atol: 0.001);

        // Segunda pasada (rediseño): rho_nuevo/As_nuevo/s_nueva dados directamente por la Tabla 74.
        var cf2 = DisenoFlexionCortanteFisuracion.VerificarControlFisuracion(
            msKNm: 12.04, asMm2: 999.49, rho: 0.0133, esMPa: 200000.0, ecMPa: 24870.0,
            dM: 0.075, sM: 0.11431, hM: 0.25);
        AssertTol("k_nuevo", cf2.K, 0.368, atol: 0.002);
        AssertTol("j_nuevo", cf2.J, 0.8773, atol: 0.001);
        AssertTol("fs_nuevo", cf2.FsMPa, 183.01, atol: 0.15);
        AssertTol("fs,adm recalculado", cf2.FsAdmisibleMPa, 249.26, atol: 0.02);

        var servicio = DisenoFlexionCortanteFisuracion.RevisarServicio(gamma, Fy, cf2.FsMPa, 16.85);
        AssertTol("Sd", servicio.Sd, 1.48, atol: 0.02);
    }

    [Fact]
    public void VerificarControlFisuracion_Tabla74_Transversal_PrimeraYSegundaPasada()
    {
        var cf = DisenoFlexionCortanteFisuracion.VerificarControlFisuracion(
            msKNm: 8.56, asMm2: 516.0, rho: 0.00597, esMPa: 200000.0, ecMPa: 24870.0,
            dM: 0.075, sM: 0.2, hM: 0.25);
        AssertTol("k", cf.K, 0.2656, atol: 0.002);
        AssertTol("j", cf.J, 0.9115, atol: 0.001);
        AssertTol("fs", cf.FsMPa, 242.59, atol: 0.15);
        AssertTol("fs,adm", cf.FsAdmisibleMPa, 179.02, atol: 0.02);

        var gamma = DisenoFlexionCortanteFisuracion.CalcularGamma(11.98, 8.56);
        var cf2 = DisenoFlexionCortanteFisuracion.VerificarControlFisuracion(
            msKNm: 8.56, asMm2: 699.22, rho: 0.00932, esMPa: 200000.0, ecMPa: 24870.0,
            dM: 0.075, sM: 0.155756, hM: 0.25);
        AssertTol("fs_nuevo", cf2.FsMPa, 182.62, atol: 0.15);
        AssertTol("fs,adm recalculado", cf2.FsAdmisibleMPa, 211.416, atol: 0.02);

        var servicio = DisenoFlexionCortanteFisuracion.RevisarServicio(gamma, Fy, cf2.FsMPa, 11.98);
        AssertTol("Sd", servicio.Sd, 1.48, atol: 0.02);
    }

    [Fact]
    public void VerificarControlFisuracion_Tabla75_CaraSuperiorLongitudinal_RequiereRediseno()
    {
        var cf = DisenoFlexionCortanteFisuracion.VerificarControlFisuracion(
            msKNm: 25.83, asMm2: 645.0, rho: 0.0033, esMPa: 200000.0, ecMPa: 24870.0,
            dM: 0.175, sM: 0.167, hM: 0.15);
        AssertTol("k", cf.K, 0.2054, atol: 0.002);
        AssertTol("j", cf.J, 0.9315, atol: 0.001);
        AssertTol("fs", cf.FsMPa, 245.64, atol: 0.15);
        // h=ef=0.15m del proyecto (aunque d=175mm > ef=150mm -- misma inconsistencia de las
        // Tablas 71-76 ya documentada; beta solo depende del umbral h>=0.4m, no cambia el resultado).
        AssertTol("fs,adm", cf.FsAdmisibleMPa, 202.67, atol: 0.3);
        Assert.False(cf.Cumple); // requiere_rediseno=true en la tabla

        var gamma = DisenoFlexionCortanteFisuracion.CalcularGamma(36.16, 25.83);
        var cf2 = DisenoFlexionCortanteFisuracion.VerificarControlFisuracion(
            msKNm: 25.83, asMm2: 781.77, rho: 0.0045, esMPa: 200000.0, ecMPa: 24870.0,
            dM: 0.175, sM: 0.14164, hM: 0.15);
        AssertTol("fs,adm recalculado", cf2.FsAdmisibleMPa, 223.51, atol: 0.05);

        var servicio = DisenoFlexionCortanteFisuracion.RevisarServicio(gamma, Fy, cf2.FsMPa, 36.16);
        AssertTol("Sd", servicio.Sd, 1.32, atol: 0.05);
    }

    [Fact]
    public void VerificarControlFisuracion_Tabla75_CaraInferiorLongitudinal_NoRequiereRediseno()
    {
        var cf = DisenoFlexionCortanteFisuracion.VerificarControlFisuracion(
            msKNm: 7.66, asMm2: 645.0, rho: 0.0033, esMPa: 200000.0, ecMPa: 24870.0,
            dM: 0.175, sM: 0.167, hM: 0.15);
        AssertTol("fs", cf.FsMPa, 72.89, atol: 0.15);
        Assert.True(cf.Cumple); // requiere_rediseno=false en la tabla
    }

    [Fact]
    public void DisenarFlexion_EsAutoconsistente_ConLaCuadraticaAABACA()
    {
        // CORREGIDO 2026-08-28 (segunda ronda de auditoría externa, hallazgo O1): esta prueba
        // quedó obsoleta cuando se corrigió el signo de BA/CA y se agregó φ en
        // DisenoFlexionCortanteFisuracion.DisenarFlexion (ver el docstring de ese método para el
        // detalle completo de H-CRÍTICO-1/H-ALTO-2) -- todavía validaba la ecuación VIEJA
        // (AA·ρ²+BA·ρ=Mu, sin φ), que con el código ya corregido da 43.93 en vez de 36.53 y
        // hubiera hecho fallar "dotnet test". Este es el mismo hallazgo, en un artefacto distinto
        // (la prueba espejo xUnit), que el error de signo original: el principio rector del
        // proyecto ("corregir, no heredar") se aplica también a los propios artefactos de
        // verificación, no solo al código de producción.
        //
        // No se fuerza una coincidencia con las Tablas 72/73 (el "d" de flexión de esas tablas
        // no reconcilia con ningún espesor de Geometria, ver discrepancia documentada). Se
        // verifica en cambio la autoconsistencia algebraica de la cuadrática CORREGIDA (que ya
        // demostró ser sensible a errores de transcripción -- ver el hallazgo del factor d²
        // documentado en el docstring de la clase, y el propio hallazgo de signo).
        const double mu = 36.53, d = 0.15, b = 1.0;
        const double phi = 0.9; // default de DisenarFlexion
        var f = DisenoFlexionCortanteFisuracion.DisenarFlexion(muKNm: mu, dM: d, bM: b, fyMPa: Fy, fcMPa: Fc);

        var aa = 0.59 * (Fy * 1000.0) * (Fy * 1000.0) * b * d * d / (Fc * 1000.0);
        var ba = (Fy * 1000.0) * b * d * d;
        var mnReconstruido = ba * f.Rho - aa * f.Rho * f.Rho; // Mn(ρ) = BA·ρ - AA·ρ² (brazo d-a/2)
        var muReconstruido = phi * mnReconstruido; // φ·Mn = Mu
        AssertTol("φ·(BA·ρ-AA·ρ²) = Mu", muReconstruido, mu, atol: 0.01);

        var asEsperado = f.Rho * b * d * 1_000_000.0;
        AssertTol("As = ρ·b·d·1,000,000", f.AsRequeridoMm2, asEsperado, atol: 0.001);
    }

    [Fact]
    public void DisenarFlexion_SaturaEnCuantiaMinima_ConMuPequeno()
    {
        var f = DisenoFlexionCortanteFisuracion.DisenarFlexion(muKNm: 0.5, dM: 0.15, bM: 1.0, fyMPa: Fy, fcMPa: Fc);
        AssertTol("rho saturado", f.Rho, DisenoFlexionCortanteFisuracion.CuantiaMinima, atol: 1e-9);
    }

    [Fact]
    public void DisenarFlexion_LanzaExcepcion_ConMuExcesivoParaElEspesor()
    {
        // Con el signo corregido, este caso concreto ahora dispara la rama de discriminante
        // negativo (ver docstring de DisenarFlexion) en vez de la rama "rhoRequerido >
        // CuantiaMaxima" -- ambas ramas son EspesorInsuficienteException, la aserción sigue
        // siendo válida sin cambios.
        Assert.Throws<EspesorInsuficienteException>(() =>
            DisenoFlexionCortanteFisuracion.DisenarFlexion(muKNm: 500.0, dM: 0.10, bM: 1.0, fyMPa: Fy, fcMPa: Fc));
    }

    // Verificación INDEPENDIENTE (segunda ronda de auditoría externa, 2026-08-28, recomendación
    // §7.1 aplicada también aquí, no solo en tools/Tanque.Core.Verificacion/Program.cs) -- una
    // segunda derivación cerrada, vía la profundidad del bloque de compresión "a"
    // (Mn=0.85·f'c·b·a·(d-a/2)), con el coeficiente EXACTO 0.85 (no el 0.59 redondeado ≈1/1.7 que
    // usa el código), para que un futuro error de álgebra en DisenarFlexion sí quede atrapado por
    // esta prueba espejo, no solo por la herramienta de verificación. Réplica exacta, en xUnit, de
    // FlexionIndependiente en Program.cs -- mantener ambas en sincronía si alguna cambia.
    private static ResultadoDisenoFlexion FlexionIndependiente(double muKNm, double dM, double bM, double fyMPa, double fcMPa, double phi)
    {
        var fcKPa = fcMPa * 1000.0;
        var fyKPa = fyMPa * 1000.0;
        var muDisenoKNm = muKNm / phi;
        var aCoef = 0.425 * fcKPa * bM;
        var bCoef = -0.85 * fcKPa * bM * dM;
        var cCoef = muDisenoKNm;
        var disc = bCoef * bCoef - 4 * aCoef * cCoef;
        if (disc < 0) throw new EspesorInsuficienteException(double.PositiveInfinity, DisenoFlexionCortanteFisuracion.CuantiaMaxima);
        var a = (-bCoef - Math.Sqrt(disc)) / (2 * aCoef);
        var asMm2 = 0.85 * fcKPa * bM * a / fyKPa * 1_000_000.0;
        var rho = asMm2 / (bM * dM * 1_000_000.0);
        if (rho > DisenoFlexionCortanteFisuracion.CuantiaMaxima)
            throw new EspesorInsuficienteException(rho, DisenoFlexionCortanteFisuracion.CuantiaMaxima);
        var rhoClamp = Math.Max(rho, DisenoFlexionCortanteFisuracion.CuantiaMinima);
        return new ResultadoDisenoFlexion(rhoClamp, rhoClamp * bM * dM * 1_000_000.0);
    }

    [Fact]
    public void DisenarFlexion_CoincideConFormaCerradaIndependiente_CuandoFlexionGobierna()
    {
        var codigo = DisenoFlexionCortanteFisuracion.DisenarFlexion(muKNm: 36.53, dM: 0.15, bM: 1.0, fyMPa: Fy, fcMPa: Fc);
        var independiente = FlexionIndependiente(36.53, 0.15, 1.0, Fy, Fc, 0.9);
        // Tolerancia amplia (no atol fijo de Tolerancia.cs): ~0.2% esperado por el redondeo 0.59 vs 1/1.7.
        var limite = 0.00002 + 0.003 * Math.Abs(independiente.Rho);
        Assert.True(Math.Abs(codigo.Rho - independiente.Rho) <= limite,
            $"ρ código={codigo.Rho:0.######} vs. independiente={independiente.Rho:0.######} (límite ±{limite:0.######})");
    }

    [Fact]
    public void DisenarFlexion_FormaCerradaIndependiente_ConfirmaCuantiaMinima_ConMuPequeno()
    {
        var codigo = DisenoFlexionCortanteFisuracion.DisenarFlexion(muKNm: 5.0, dM: 0.15, bM: 1.0, fyMPa: Fy, fcMPa: Fc);
        var independiente = FlexionIndependiente(5.0, 0.15, 1.0, Fy, Fc, 0.9);
        Assert.Equal(DisenoFlexionCortanteFisuracion.CuantiaMinima, codigo.Rho);
        Assert.Equal(DisenoFlexionCortanteFisuracion.CuantiaMinima, independiente.Rho);
    }

    [Fact]
    public void DisenarFlexion_FormaCerradaIndependiente_ConfirmaExcepcion_ConMuExcesivoParaElEspesor()
    {
        Assert.Throws<EspesorInsuficienteException>(() => FlexionIndependiente(500.0, 0.10, 1.0, Fy, Fc, 0.9));
    }

    [Fact]
    public void CalcularFsAdmisible_AcotaAlTopeDe250MPa_YAlPisoDe140_170MPa()
    {
        // NSR-10 C.23-C.10.6.4.1 (cruce 2026-08-30): fs,max = clamp(fórmula, piso 140/170, tope 250).
        // (a) separación densa (s=75mm): la fórmula cruda da ~305 MPa → debe quedar ACOTADA a 250.
        AssertTol("Tope 250 MPa (s=75mm, Ø15.9mm, h=0.25m)",
            DisenoFlexionCortanteFisuracion.CalcularFsAdmisible(0.075, 0.25, 15.9), 250.0, atol: 1e-6);
        // (b) separación amplia (s=1.0m): la fórmula cruda da ~42 MPa → debe PISARSE a 140 (una dirección).
        AssertTol("Piso 140 MPa (s=1.0m, una dirección)",
            DisenoFlexionCortanteFisuracion.CalcularFsAdmisible(1.0, 0.25, 25.0), 140.0, atol: 1e-6);
        // (c) el piso de DOS direcciones (170 MPa) se respeta cuando se pide explícitamente.
        AssertTol("Piso 170 MPa (s=1.0m, dos direcciones)",
            DisenoFlexionCortanteFisuracion.CalcularFsAdmisible(1.0, 0.25, 25.0, DisenoFlexionCortanteFisuracion.FsAdmisibleMinimoDosDireccionesMPa), 170.0, atol: 1e-6);
        // (d) un valor intermedio dentro de [140, 250] NO se altera por el acotado.
        AssertTol("Sin alterar dentro de [140,250] (s=0.15m)",
            DisenoFlexionCortanteFisuracion.CalcularFsAdmisible(0.15, 0.25, 25.0), 216.24, atol: 0.01);
    }

    private static void AssertTol(string nombre, double actual, double esperado, double? atol = null)
    {
        var ok = atol.HasValue
            ? Tolerancia.SonIguales(actual, esperado, toleranciaAbsoluta: atol.Value)
            : Tolerancia.SonIguales(actual, esperado);
        Assert.True(ok, Tolerancia.Diagnostico(nombre, actual, esperado));
    }
}
