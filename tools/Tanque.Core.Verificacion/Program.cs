// ---------------------------------------------------------------------------
// Tanque.NSR10
// (c) Mauricio Javier Victoria Niño <hidratecsa@gmail.com> · CC BY-NC-SA 4.0
// Uso exclusivamente académico. El motor de cálculo (Tanque.Core) se distribuye
// como binario ofuscado, protegido por derechos de autor (ver LICENSE).
// ---------------------------------------------------------------------------
// Herramienta de verificación sin dependencias externas (sin NuGet).
//
// Por qué existe: la sesión en la nube que generó este scaffold tiene nuget.org bloqueado por
// la lista blanca de red del sandbox, así que el proyecto de pruebas xUnit
// (tests/Tanque.Core.Tests) no pudo restaurarse ni compilarse ahí. Esta herramienta reimplementa
// las mismas comprobaciones sin depender de ningún paquete NuGet (solo usa Tanque.Core y
// System.Text.Json, que forman parte del SDK), para poder verificar la lógica de cálculo dentro
// de ese sandbox. En un equipo con acceso normal a NuGet, use "dotnet test" sobre
// tests/Tanque.Core.Tests en su lugar -- esta herramienta no reemplaza esas pruebas, solo cubre
// el mismo caso mientras no hay acceso a NuGet.

using System.Linq;
using System.Text.Json.Nodes;
using Tanque.Core.Dominio;
using Tanque.Core.Modulos;
using Tanque.Reportes;

var baseDir = AppContext.BaseDirectory;
var casosDir = Path.Combine(baseDir, "casos_prueba");

var fallos = 0;
var totalAserciones = 0;

void AssertTol(string nombre, double actual, double esperado, double atol = 0.01, double rtol = 0.001)
{
    totalAserciones++;
    var limite = atol + rtol * Math.Abs(esperado);
    var diff = actual - esperado;
    var ok = Math.Abs(diff) <= limite;
    var estado = ok ? "OK  " : "FAIL";
    Console.WriteLine($"  [{estado}] {nombre}: esperado={esperado}, actual={actual:0.######}, diff={diff:+0.######;-0.######}, límite=±{limite:0.######}");
    if (!ok) fallos++;
}

double Geo(JsonNode raiz, string campo) => raiz["entradas"]!["geometria"]![campo]!.GetValue<double>();
bool GeoBool(JsonNode raiz, string campo) => raiz["entradas"]!["geometria"]![campo]!.GetValue<bool>();
double Mat(JsonNode raiz, string campo) => raiz["entradas"]!["materiales"]![campo]!.GetValue<double>();
double Suelo(JsonNode raiz, string campo) => raiz["entradas"]!["suelo"]![campo]!.GetValue<double>();

ProyectoTanque ProyectoDesde(JsonNode raiz)
{
    var conTapa = GeoBool(raiz, "con_tapa");
    var geometria = new Geometria(
        BAnchoM: Geo(raiz, "B_ancho_m"),
        LLargoM: Geo(raiz, "L_largo_m"),
        HtAlturaM: Geo(raiz, "Ht_altura_m"),
        ConTapa: conTapa,
        EmEspesorMuroM: Geo(raiz, "em_espesor_muro_m"),
        EfEspesorFondoM: Geo(raiz, "ef_espesor_fondo_m"),
        EtEspesorTapaM: conTapa ? Geo(raiz, "et_espesor_tapa_m") : 0.0,
        HLAlturaLiquidoM: Geo(raiz, "HL_altura_liquido_m"),
        HmAlturaSueloSobreMuroM: Geo(raiz, "Hm_altura_suelo_sobre_muro_m"),
        WextSobrecargaKNm2: Geo(raiz, "Wext_sobrecarga_kNm2"));

    var materiales = new Materiales(
        FcMPa: Mat(raiz, "fc_MPa"),
        FyMPa: Mat(raiz, "fy_MPa"),
        GammaSueloKNm3: Mat(raiz, "gamma_suelo_kNm3"),
        GammaConcretoKNm3: Mat(raiz, "gamma_concreto_kNm3"),
        GammaLiquidoKNm3: Mat(raiz, "gamma_liquido_kNm3"),
        PhiGradosAnguloFriccionSuelo: Suelo(raiz, "phi_grados"));

    return new ProyectoTanque(geometria, materiales);
}

Console.WriteLine("=== Ejercicio 1: tanque de lados iguales, sin tapa ===");
{
    var raiz = JsonNode.Parse(File.ReadAllText(Path.Combine(casosDir, "ejercicio_1_tanque_lados_iguales.json")))!;
    var proyecto = ProyectoDesde(raiz);
    var r = CargasGravitacionales.Calcular(proyecto);
    var esperado = raiz["resultados_esperados"]!["peso_tanque_kN"]!;

    AssertTol("Pm1 (par muros tipo B)", r.Pm1ParMurosTipoBKN, esperado["Pm1_par_muros_tipo_B_kN"]!.GetValue<double>());
    AssertTol("Pm2 (par muros tipo L)", r.Pm2ParMurosTipoLKN, esperado["Pm2_par_muros_tipo_L_kN"]!.GetValue<double>());
    AssertTol("muros_x4 (2Pm1+2Pm2)", 2 * r.Pm1ParMurosTipoBKN + 2 * r.Pm2ParMurosTipoLKN, esperado["muros_x4"]!.GetValue<double>());
    AssertTol("placa_cimentacion (Pf)", r.PfFondoKN, esperado["placa_cimentacion"]!.GetValue<double>());
    AssertTol("total (Ptt)", r.PttTotalKN, esperado["total"]!.GetValue<double>());
    AssertTol("Pt (cubierta, debe ser 0 sin tapa)", r.PtCubiertaKN, 0.0);
}

Console.WriteLine();
Console.WriteLine("=== Ejercicio 2: tanque de lados dispares, con tapa (Tabla 36 de la tesis) ===");
{
    var raiz = JsonNode.Parse(File.ReadAllText(Path.Combine(casosDir, "ejercicio_2_tanque_lados_dispares_sismo.json")))!;
    var proyecto = ProyectoDesde(raiz);
    var r = CargasGravitacionales.Calcular(proyecto);
    var esperado = raiz["resultados_esperados"]!["peso_tanque_kN"]!;

    AssertTol("Pm1 (par muros dirección corta B)", r.Pm1ParMurosTipoBKN, esperado["Pm1_par_muros_direccion_corta_B_kN"]!.GetValue<double>());
    AssertTol("Pm2 (par muros dirección larga L)", r.Pm2ParMurosTipoLKN, esperado["Pm2_par_muros_direccion_larga_L_kN"]!.GetValue<double>());
    AssertTol("tapa (Pt)", r.PtCubiertaKN, esperado["tapa_kN"]!.GetValue<double>());
    AssertTol("placa_fondo (Pf)", r.PfFondoKN, esperado["placa_fondo_kN"]!.GetValue<double>());
    AssertTol("total (Ptt)", r.PttTotalKN, esperado["total"]!.GetValue<double>());
}

Console.WriteLine();
Console.WriteLine("=== Presiones laterales -- ejercicio 1 (phi=32 grados) ===");
{
    var raiz = JsonNode.Parse(File.ReadAllText(Path.Combine(casosDir, "ejercicio_1_tanque_lados_iguales.json")))!;
    var proyecto = ProyectoDesde(raiz);
    var r = PresionesLaterales.Calcular(proyecto);
    var esperado = raiz["resultados_esperados"]!["presiones_diseno"]!;

    AssertTol("Ph (hidrostática máxima)", r.PhMaximaKNm2, esperado["Ph_hidrostatica_kNm2"]!.GetValue<double>());
    AssertTol("Ka (Rankine corregido, NO el publicado 0.2544)", r.Ka, 0.30706, atol: 0.0005);
    AssertTol("Ps2 (con Ka corregido, NO el publicado 12.21)", r.Ps2MaximaKNm2, 14.74, atol: 0.02);
    AssertTol("Ph[10] == PhMaximaKNm2", r.Ph[10], r.PhMaximaKNm2);
    AssertTol("Ps[10] == Ps2MaximaKNm2", r.Ps[10], r.Ps2MaximaKNm2);
}

Console.WriteLine();
Console.WriteLine("=== Presiones laterales -- ejercicio 2 (phi=28 grados) ===");
{
    var raiz = JsonNode.Parse(File.ReadAllText(Path.Combine(casosDir, "ejercicio_2_tanque_lados_dispares_sismo.json")))!;
    var proyecto = ProyectoDesde(raiz);
    var r = PresionesLaterales.Calcular(proyecto);
    var esperado = raiz["resultados_esperados"]!["presiones_diseno"]!;

    AssertTol("Ph (con HL=3.00m consistente, NO el publicado 44.15)", r.PhMaximaKNm2,
        esperado["Ph_si_HL_3_00m_consistente_con_resto_del_ejercicio"]!.GetValue<double>());
    AssertTol("Ka (Rankine corregido, NO el publicado 0.2818)", r.Ka, 0.36095, atol: 0.0005);
    AssertTol("Ps2 (con Ka corregido, NO el publicado 15.21)", r.Ps2MaximaKNm2, 19.49, atol: 0.02);
}

Console.WriteLine();
Console.WriteLine("=== Fuerza sísmica hidrodinámica (Housner) -- ejercicio 2, verificación INDEPENDIENTE con claro interior (ACI 350.3 \"inside dimensions\") ===");
{
    var raiz = JsonNode.Parse(File.ReadAllText(Path.Combine(casosDir, "ejercicio_2_tanque_lados_dispares_sismo.json")))!;
    var proyecto = ProyectoDesde(raiz);
    var cargas = CargasGravitacionales.Calcular(proyecto);
    var sismo = raiz["entradas"]!["sismo"]!;
    var espectro = new ParametrosEspectroDiseno(
        Aa: sismo["Aa"]!.GetValue<double>(),
        Av: sismo["Av"]!.GetValue<double>(),
        Fa: sismo["Fa"]!.GetValue<double>(),
        Fv: sismo["Fv"]!.GetValue<double>(),
        I: 1.25,
        CondicionBase: CondicionBaseMuro.Rigida,
        CondicionAnclaje: CondicionAnclajeBase.ArticuladaEmpotrada);

    var r = FuerzaSismicaHidrodinamica.Calcular(proyecto, espectro);
    var g = proyecto.Geometria;
    var em = g.EmEspesorMuroM;
    var hl = g.HLAlturaLiquidoM;
    var gammaL = proyecto.Materiales.GammaLiquidoKNm3;
    var gammaC = proyecto.Materiales.GammaConcretoKNm3;
    var Lint = g.LLargoM - 2.0 * em; // claro interior (ACI 350.3: "inside length")
    var Bint = g.BAnchoM - 2.0 * em;

    // ---- verificaciones DIRECTAS de la dimensión (la decisión de esta sesión) ----
    AssertTol("Muro longitudinal: DireccionSismoM == claro interior (L-2·em)", r.MuroLongitudinal.DireccionSismoM, Lint, atol: 1e-12);
    AssertTol("Muro transversal: DireccionSismoM == claro interior (B-2·em)", r.MuroTransversal.DireccionSismoM, Bint, atol: 1e-12);
    var wlEsperado = gammaL * Bint * hl * Lint;
    AssertTol("WL (peso total del líquido) == γ·(B-2em)·HL·(L-2em) [volumen interior]", r.MuroLongitudinal.WLPesoTotalLiquidoKN, wlEsperado, atol: 1e-9);

    // ---- espectro y anclaje (independientes de la dimensión del líquido) ----
    var sdsEsperado = 2.5 * espectro.Fa * espectro.Aa;
    var s1Esperado = 1.2 * espectro.Fv * espectro.Av;
    var tsEsperado = 0.48 * (espectro.Av * espectro.Fv) / (espectro.Aa * espectro.Fa);
    AssertTol("Sds", r.MuroLongitudinal.Sds, sdsEsperado, atol: 1e-12);
    AssertTol("S1", r.MuroLongitudinal.S1, s1Esperado, atol: 1e-12);
    AssertTol("Ts", r.MuroLongitudinal.Ts, tsEsperado, atol: 1e-12);
    AssertTol("Ri", r.MuroLongitudinal.Ri, 3.0, atol: 1e-12);
    AssertTol("Rc", r.MuroLongitudinal.Rc, 1.0, atol: 1e-12);

    // Pr usa Wr=Pt (peso de cubierta, independiente de la dimensión del líquido) y Ci=Sds=0.65.
    AssertTol("Pr (autoconsistente, Wr=Pt)", r.MuroLongitudinal.PrSobrecargaCubiertaKN, sdsEsperado * espectro.I * (cargas.PtCubiertaKN / 3.0), atol: 0.01);

    // ---- re-derivación INDEPENDIENTE de Housner/ACI 350.3 con claro interior (ambas vías) ----
    void VerificarMuro(string nombre, ResultadoFuerzaSismicaMuro m, double ldir, double lperp)
    {
        var ratio = ldir / hl;
        var hlSobreLdir = hl / ldir;
        var argImp = 0.866 * ratio;
        var wi = (Math.Tanh(argImp) / argImp) * wlEsperado;
        var wc = (0.264 * ratio * Math.Tanh(3.16 * hlSobreLdir)) * wlEsperado;
        // base rígida (k=1)
        var hi = ratio >= 1.33 ? 0.375 * hl : (0.5 - 0.09375 * ratio) * hl;
        var hc = hl * (1.0 - (Math.Cosh(3.16 * hlSobreLdir) - 1.0) / (3.16 * hlSobreLdir * Math.Sinh(3.16 * hlSobreLdir)));

        var mi = (wi / wlEsperado) * (ldir / 2.0) * hl * (gammaL / 9.8061);
        var mw = g.HtAlturaM * em * (gammaC / 9.8061);
        var mTotal = mi + mw;
        var h = ((g.HtAlturaM / 2.0) * mw + hi * mi) / (mw + mi);
        var ec = 4700 * Math.Sqrt(proyecto.Materiales.FcMPa);
        var k = (ec / 4e6) * Math.Pow(em * 1000.0 / h, 3);
        var ti = 2 * Math.PI * Math.Sqrt(mTotal / k);
        var lambda = Math.Sqrt(3.16 * 9.8065 * Math.Tanh(3.16 * hlSobreLdir));
        var tc = (2 * Math.PI / lambda) * Math.Sqrt(ldir);
        var epsilon = Math.Min(1.0, 0.051 * ratio * ratio - 0.1908 * ratio + 1.021);

        var ci = (ti <= tsEsperado || s1Esperado / ti > sdsEsperado) ? sdsEsperado : s1Esperado / ti;
        var cc = (tc <= 1.6 / tsEsperado) ? Math.Min(1.5 * sdsEsperado, 1.5 * s1Esperado / tc) : 2.4 * sdsEsperado / (tc * tc);
        var ri = 3.0; var rc = 1.0; // ArticuladaEmpotrada
        var pi = ci * espectro.I * (wi / ri);
        var pc = cc * espectro.I * (wc / rc);

        // Distribución trapezoidal p(y)=(P/(2·lperp))·(4hl-6h*-(6hl-12h*)(y/hl))/hl² ; y=0 fondo, y=hl superficie.
        double Eval(double p, double hE, double y) => (p / (2.0 * lperp)) * ((4.0 * hl - 6.0 * hE - (6.0 * hl - 12.0 * hE) * (y / hl)) / (hl * hl));
        var piFondo = Math.Max(0.0, Eval(pi, hi, 0.0));
        var piSup = Math.Max(0.0, Eval(pi, hi, hl));
        var pcFondoBruto = Eval(pc, hc, 0.0);
        var pcSupBruto = Eval(pc, hc, hl);
        var pcFondo = pcFondoBruto < 0 ? 0.0 : pcFondoBruto;
        var pcSup = pcFondoBruto < 0 ? pc / (lperp * hl) : pcSupBruto;

        AssertTol($"{nombre}: Wi (impulsivo, claro interior)", m.WiPesoImpulsivoKN, wi, atol: 0.05);
        AssertTol($"{nombre}: Wc (convectivo, claro interior)", m.WcPesoConvectivoKN, wc, atol: 0.05);
        AssertTol($"{nombre}: hi", m.HiAlturaCentroideImpulsivoM, hi, atol: 0.005);
        AssertTol($"{nombre}: hc", m.HcAlturaCentroideConvectivoM, hc, atol: 0.005);
        AssertTol($"{nombre}: mw (masa muro)", m.MwMasaMuro, mw, atol: 0.01);
        AssertTol($"{nombre}: mi (masa impulsiva tributaria)", m.MiMasaImpulsivaTributaria, mi, atol: 0.01);
        AssertTol($"{nombre}: h (centroide)", m.HCentroideCombinadoM, h, atol: 0.01);
        AssertTol($"{nombre}: Ec", m.EcMPa, ec, atol: 0.5);
        AssertTol($"{nombre}: k (rigidez)", m.KRigidezLateralMuro, k, atol: 20, rtol: 0.002);
        AssertTol($"{nombre}: Ti", m.TiPeriodoImpulsivoS, ti, atol: 0.001);
        AssertTol($"{nombre}: lambda", m.LambdaFactorPeriodoConvectivo, lambda, atol: 0.01);
        AssertTol($"{nombre}: Tc", m.TcPeriodoConvectivoS, tc, atol: 0.01);
        AssertTol($"{nombre}: epsilon", m.Epsilon, epsilon, atol: 0.002);
        AssertTol($"{nombre}: Ci", m.Ci, ci, atol: 0.005);
        AssertTol($"{nombre}: Cc", m.Cc, cc, atol: 0.005);
        AssertTol($"{nombre}: Pi (impulsiva)", m.PiImpulsivaKN, pi, atol: 0.1);
        AssertTol($"{nombre}: Pc (convectiva)", m.PcConvectivaKN, pc, atol: 0.5);
        AssertTol($"{nombre}: presión impulsiva fondo", m.PresionImpulsiva.FondoKNm2, piFondo, atol: 0.05);
        AssertTol($"{nombre}: presión impulsiva superficie", m.PresionImpulsiva.SuperficieKNm2, piSup, atol: 0.02);
        AssertTol($"{nombre}: presión convectiva fondo", m.PresionConvectiva.FondoKNm2, pcFondo, atol: 0.02);
        AssertTol($"{nombre}: presión convectiva superficie", m.PresionConvectiva.SuperficieKNm2, pcSup, atol: 0.02);
    }

    VerificarMuro("Muro longitudinal", r.MuroLongitudinal, Lint, Bint);
    VerificarMuro("Muro transversal", r.MuroTransversal, Bint, Lint);
}

Console.WriteLine();
Console.WriteLine("=== Fuerza dinámica de suelo (Mononobe-Okabe) -- ejercicio 2 (Tabla 39) ===");
{
    var raiz = JsonNode.Parse(File.ReadAllText(Path.Combine(casosDir, "ejercicio_2_tanque_lados_dispares_sismo.json")))!;
    var proyecto = ProyectoDesde(raiz);
    var sismo = raiz["entradas"]!["sismo"]!;
    var parametros = new ParametrosSueloDinamico(
        KhCoeficienteSismicoHorizontal: sismo["kh_coef_sismico_horizontal"]!.GetValue<double>(),
        KvCoeficienteSismicoVertical: sismo["kv_coef_sismico_vertical"]!.GetValue<double>(),
        DeltaGradosFriccionSueloMuro: 0,
        IGradosInclinacionRelleno: 0,
        BetaGradosInclinacionMuro: 90);

    var r = FuerzaDinamicaSuelo.Calcular(proyecto, parametros);
    var esperado = raiz["resultados_esperados"]!["presion_dinamica_suelo_mononobe_okabe_tabla39"]!;

    AssertTol("theta", r.ThetaGrados, esperado["theta_grados"]!.GetValue<double>(), atol: 0.01);
    AssertTol("psi", r.Psi, esperado["psi"]!.GetValue<double>(), atol: 0.01);
    AssertTol("Kae", r.Kae, esperado["Kae"]!.GetValue<double>(), atol: 0.001);
    AssertTol("Ka (Rankine corregido, NO el publicado 0.2818)", r.Ka, 0.36095, atol: 0.0005);
    AssertTol("Keq (con Ka corregido, NO el publicado 0.1376)", r.Keq, 0.05835, atol: 0.001);
    AssertTol("Qae (con Ka corregido, NO el publicado 7.43 kNm2)", r.QaeKNm2, 3.15, atol: 0.05);

    // Correccion 2026-08-28 (auditoria externa del usuario, "revision sismo fuera de dominio PCA
    // Caso 7", hallazgo P5): Qae debe escalar con Hm (altura de suelo RETENIDO contra el muro), NO
    // con Ht (altura total del muro) -- la geometria de arriba tiene Hm=Ht=3.0m por coincidencia,
    // asi que no distingue la formula corregida de la anterior. Geometria dedicada con Hm != Ht.
    Console.WriteLine("-- Correccion Ht->Hm (hallazgo P5, auditoria 2026-08-28): Qae debe escalar con Hm, no con Ht --");
    var geoHtHm = new Geometria(BAnchoM: 4.5, LLargoM: 6.0, HtAlturaM: 5.0, ConTapa: false, EmEspesorMuroM: 0.25, EfEspesorFondoM: 0.20, EtEspesorTapaM: 0.0, HLAlturaLiquidoM: 3.0, HmAlturaSueloSobreMuroM: 2.0, WextSobrecargaKNm2: 0.0);
    var matHtHm = new Materiales(FcMPa: 21, FyMPa: 420, GammaConcretoKNm3: 24, GammaLiquidoKNm3: 9.81, GammaSueloKNm3: 18, PhiGradosAnguloFriccionSuelo: 28);
    var proyectoHtHm = new ProyectoTanque(geoHtHm, matHtHm);
    var parametrosHtHm = new ParametrosSueloDinamico(KhCoeficienteSismicoHorizontal: 0.20, KvCoeficienteSismicoVertical: 0.0, DeltaGradosFriccionSueloMuro: 0.0, IGradosInclinacionRelleno: 0.0, BetaGradosInclinacionMuro: 90.0);
    var rHtHm = FuerzaDinamicaSuelo.Calcular(proyectoHtHm, parametrosHtHm);
    var qaeEsperadoConHm = matHtHm.GammaSueloKNm3 * geoHtHm.HmAlturaSueloSobreMuroM * rHtHm.Keq; // Hm=2.0, NO Ht=5.0
    var qaeSiUsaraHt = matHtHm.GammaSueloKNm3 * geoHtHm.HtAlturaM * rHtHm.Keq; // el valor INCORRECTO que producia la version anterior
    AssertTol("Qae usa Hm (altura de suelo retenido), no Ht (altura total del muro)", rHtHm.QaeKNm2, qaeEsperadoConHm, atol: 1e-9);
    totalAserciones++;
    if (Math.Abs(rHtHm.QaeKNm2 - qaeSiUsaraHt) > 0.5 * Math.Abs(qaeSiUsaraHt - qaeEsperadoConHm))
        Console.WriteLine($"  [OK  ] Qae ({rHtHm.QaeKNm2:0.###} kN/m²) difiere claramente del valor que habria dado Ht ({qaeSiUsaraHt:0.###} kN/m²) -- confirma que Hm!=Ht en esta geometria realmente ejercita la correccion");
    else { fallos++; Console.WriteLine($"  [FAIL] Qae con Hm ({rHtHm.QaeKNm2:0.###}) demasiado cercano al que daria Ht ({qaeSiUsaraHt:0.###}) -- la geometria de prueba no distingue la correccion"); }

    // TipoTanque.Superficial exige Hm=0 -- Qae debe ser exactamente 0 (sin suelo, sin presion
    // dinamica de suelo que calcular), lo que antes de esta correccion NO ocurria (Ht>0 producia un
    // Qae distinto de cero pese a no haber ningun suelo contra el muro).
    var geoSuperficialHtHm = new Geometria(BAnchoM: 6.0, LLargoM: 8.0, HtAlturaM: 4.5, ConTapa: true, EmEspesorMuroM: 0.3, EfEspesorFondoM: 0.35, EtEspesorTapaM: 0.2, HLAlturaLiquidoM: 4.0, HmAlturaSueloSobreMuroM: 0.0, WextSobrecargaKNm2: 0.0, Tipo: TipoTanque.Superficial);
    var proyectoSuperficialHtHm = new ProyectoTanque(geoSuperficialHtHm, matHtHm);
    var rSuperficialHtHm = FuerzaDinamicaSuelo.Calcular(proyectoSuperficialHtHm, parametrosHtHm);
    AssertTol("TipoTanque.Superficial (Hm=0): Qae=0 exacto (sin suelo, sin presion dinamica de suelo)", rSuperficialHtHm.QaeKNm2, 0.0, atol: 1e-9);
}

Console.WriteLine();
Console.WriteLine("=== Diseño a flexión, cortante y control de fisuración (F.12-14) -- ejercicio 2 ===");
{
    // fc=28 MPa, fy=420 MPa (materiales del ejercicio 2). Este módulo NO deriva d de Geometria
    // (ver discrepancia abierta documentada en el docstring de la clase); cada assert usa un "d"
    // justificado explícitamente en el comentario, tal como haría el futuro módulo de placas/muros.
    const double fc = 28.0, fy = 420.0;

    Console.WriteLine("-- Cortante (Tabla 71) --");
    // Placa de fondo: d=0.175m confirmado de forma independiente por el campo "d_mm"=175 de la
    // propia Tabla 75 (control de fisuración) -- verificación no circular.
    var vcFondo = DisenoFlexionCortanteFisuracion.VerificarCortante(vuKN: 83.86, dM: 0.175, bM: 1.0, fcMPa: fc);
    AssertTol("Vc placa de fondo (d=0.175m, de Tabla 75)", vcFondo.VcKN, 118.06, atol: 0.05);
    if (!vcFondo.Cumple) { fallos++; Console.WriteLine("  [FAIL] se esperaba Vc>=Vu para la placa de fondo"); }

    // Placa de cubierta (tapa): con el d=0.075m que la propia Tabla 74 declara para flexión, Vc no
    // coincide con el Vu/Vc publicado en la Tabla 71 (67.46 kN) -- discrepancia abierta, ya
    // documentada en el docstring de la clase (el d de cortante y el d de flexión de este mismo
    // elemento no reconcilian). Se verifica aquí el valor que la fórmula SÍ produce con ese d,
    // dejando constancia explícita del valor publicado que no reconcilia.
    var vcTapaConDFlexion = DisenoFlexionCortanteFisuracion.VerificarCortante(vuKN: 28.32, dM: 0.075, bM: 1.0, fcMPa: fc);
    AssertTol("Vc tapa con d=0.075m (de Tabla 74) -- NO coincide con el Vc publicado en Tabla 71 (67.46 kN); discrepancia abierta", vcTapaConDFlexion.VcKN, 50.60, atol: 0.05);
    // Con d=0.10m (el que el Vc=67.46 kN publicado implica algebraicamente) sí reconcilia:
    var vcTapaConDCortante = DisenoFlexionCortanteFisuracion.VerificarCortante(vuKN: 28.32, dM: 0.10, bM: 1.0, fcMPa: fc);
    AssertTol("Vc tapa con d=0.10m (implícito del Vc publicado en Tabla 71)", vcTapaConDCortante.VcKN, 67.46, atol: 0.05);

    // Muros longitudinal y transversal: d=0.15m inferido algebraicamente del Vc=101.2 kN publicado
    // (ambos muros comparten el mismo Vc en la Tabla 71). No hay tabla de control de fisuración
    // para muros en este ejercicio que permita confirmar ese d de forma independiente -- se deja
    // constancia de que este valor es inferido, no confirmado como el de la placa de fondo.
    var vcMuroLong = DisenoFlexionCortanteFisuracion.VerificarCortante(vuKN: 85.72, dM: 0.15, bM: 1.0, fcMPa: fc);
    AssertTol("Vc muro longitudinal (d=0.15m, inferido -- no confirmable de forma independiente)", vcMuroLong.VcKN, 101.2, atol: 0.05);
    var vcMuroTrans = DisenoFlexionCortanteFisuracion.VerificarCortante(vuKN: 82.22, dM: 0.15, bM: 1.0, fcMPa: fc);
    AssertTol("Vc muro transversal (d=0.15m, inferido -- no confirmable de forma independiente)", vcMuroTrans.VcKN, 101.2, atol: 0.05);

    Console.WriteLine("-- Control de fisuración y factor de servicio Sd (Tabla 74, placa de cubierta) --");
    // Columna "Longitudinal": n, rho, As, d, s, h, Mu, Ms dados directamente por la Tabla 74 (no
    // derivados de un espesor) -- verificación no circular de CalcularN/CalcularKJ/CalcularFs/
    // CalcularFsAdmisible.
    {
        var cf = DisenoFlexionCortanteFisuracion.VerificarControlFisuracion(
            msKNm: 12.04, asMm2: 796.0, rho: 0.0086, esMPa: 200000.0, ecMPa: 24870.0,
            dM: 0.075, sM: 0.2, hM: 0.25);
        AssertTol("n (Tabla74 Longitudinal)", cf.N, 8.04, atol: 0.01);
        AssertTol("k (Tabla74 Longitudinal)", cf.K, 0.3094, atol: 0.002);
        AssertTol("j (Tabla74 Longitudinal)", cf.J, 0.8969, atol: 0.001);
        AssertTol("fs (Tabla74 Longitudinal)", cf.FsMPa, 224.79, atol: 0.15);
        AssertTol("fs,adm (Tabla74 Longitudinal)", cf.FsAdmisibleMPa, 179.02, atol: 0.02);

        var gamma = DisenoFlexionCortanteFisuracion.CalcularGamma(16.85, 12.04);
        AssertTol("gamma (Tabla74 Longitudinal)", gamma, 1.4, atol: 0.001);

        // Segunda pasada (rediseño tras no cumplir fs<=fs,adm): rho_nuevo/As_nuevo/s_nueva dados
        // directamente por la Tabla 74 -- confirma CalcularKJ/CalcularFs/CalcularFsAdmisible con un
        // segundo juego de entradas independiente, y RevisarServicio con el fs resultante.
        var cf2 = DisenoFlexionCortanteFisuracion.VerificarControlFisuracion(
            msKNm: 12.04, asMm2: 999.49, rho: 0.0133, esMPa: 200000.0, ecMPa: 24870.0,
            dM: 0.075, sM: 0.11431, hM: 0.25);
        AssertTol("k_nuevo (Tabla74 Longitudinal)", cf2.K, 0.368, atol: 0.002);
        AssertTol("j_nuevo (Tabla74 Longitudinal)", cf2.J, 0.8773, atol: 0.001);
        AssertTol("fs_nuevo (Tabla74 Longitudinal)", cf2.FsMPa, 183.01, atol: 0.15);
        AssertTol("fs,adm recalculado (Tabla74 Longitudinal)", cf2.FsAdmisibleMPa, 249.26, atol: 0.02);

        var servicio = DisenoFlexionCortanteFisuracion.RevisarServicio(gamma, fy, cf2.FsMPa, 16.85);
        AssertTol("Sd (Tabla74 Longitudinal)", servicio.Sd, 1.48, atol: 0.02);
    }

    // Columna "Transversal": mismo patrón, segundo juego de entradas de la Tabla 74.
    {
        var cf = DisenoFlexionCortanteFisuracion.VerificarControlFisuracion(
            msKNm: 8.56, asMm2: 516.0, rho: 0.00597, esMPa: 200000.0, ecMPa: 24870.0,
            dM: 0.075, sM: 0.2, hM: 0.25);
        AssertTol("k (Tabla74 Transversal)", cf.K, 0.2656, atol: 0.002);
        AssertTol("j (Tabla74 Transversal)", cf.J, 0.9115, atol: 0.001);
        AssertTol("fs (Tabla74 Transversal)", cf.FsMPa, 242.59, atol: 0.15);
        AssertTol("fs,adm (Tabla74 Transversal)", cf.FsAdmisibleMPa, 179.02, atol: 0.02);

        var gamma = DisenoFlexionCortanteFisuracion.CalcularGamma(11.98, 8.56);
        var cf2 = DisenoFlexionCortanteFisuracion.VerificarControlFisuracion(
            msKNm: 8.56, asMm2: 699.22, rho: 0.00932, esMPa: 200000.0, ecMPa: 24870.0,
            dM: 0.075, sM: 0.155756, hM: 0.25);
        AssertTol("fs_nuevo (Tabla74 Transversal)", cf2.FsMPa, 182.62, atol: 0.15);
        AssertTol("fs,adm recalculado (Tabla74 Transversal)", cf2.FsAdmisibleMPa, 211.416, atol: 0.02);

        var servicio = DisenoFlexionCortanteFisuracion.RevisarServicio(gamma, fy, cf2.FsMPa, 11.98);
        AssertTol("Sd (Tabla74 Transversal)", servicio.Sd, 1.48, atol: 0.02);
    }

    Console.WriteLine("-- Control de fisuración y factor de servicio Sd (Tabla 75, placa de fondo) --");
    // Columna "CaraSuperior_Longitudinal": SÍ requiere rediseño (fs > fs,adm).
    {
        var cf = DisenoFlexionCortanteFisuracion.VerificarControlFisuracion(
            msKNm: 25.83, asMm2: 645.0, rho: 0.0033, esMPa: 200000.0, ecMPa: 24870.0,
            dM: 0.175, sM: 0.167, hM: 0.15);
        AssertTol("k (Tabla75 CaraSuperior_Longitudinal)", cf.K, 0.2054, atol: 0.002);
        AssertTol("j (Tabla75 CaraSuperior_Longitudinal)", cf.J, 0.9315, atol: 0.001);
        AssertTol("fs (Tabla75 CaraSuperior_Longitudinal)", cf.FsMPa, 245.64, atol: 0.15);
        AssertTol("fs,adm (Tabla75 CaraSuperior_Longitudinal, con h=ef=0.15m del proyecto)", cf.FsAdmisibleMPa, 202.67, atol: 0.3);
        if (cf.Cumple) { fallos++; Console.WriteLine("  [FAIL] se esperaba que NO cumpliera (requiere_rediseno=true en Tabla75)"); }
        totalAserciones++;

        var gamma = DisenoFlexionCortanteFisuracion.CalcularGamma(36.16, 25.83);
        var cf2 = DisenoFlexionCortanteFisuracion.VerificarControlFisuracion(
            msKNm: 25.83, asMm2: 781.77, rho: 0.0045, esMPa: 200000.0, ecMPa: 24870.0,
            dM: 0.175, sM: 0.14164, hM: 0.15);
        AssertTol("fs,adm recalculado (Tabla75 CaraSuperior_Longitudinal)", cf2.FsAdmisibleMPa, 223.51, atol: 0.05);
        var servicio = DisenoFlexionCortanteFisuracion.RevisarServicio(gamma, fy, cf2.FsMPa, 36.16);
        AssertTol("Sd (Tabla75 CaraSuperior_Longitudinal)", servicio.Sd, 1.32, atol: 0.05);
    }

    // Columna "CaraInferior_Longitudinal": NO requiere rediseño (fs <= fs,adm) -- cubre la otra rama.
    {
        var cf = DisenoFlexionCortanteFisuracion.VerificarControlFisuracion(
            msKNm: 7.66, asMm2: 645.0, rho: 0.0033, esMPa: 200000.0, ecMPa: 24870.0,
            dM: 0.175, sM: 0.167, hM: 0.15);
        AssertTol("fs (Tabla75 CaraInferior_Longitudinal)", cf.FsMPa, 72.89, atol: 0.15);
        if (!cf.Cumple) { fallos++; Console.WriteLine("  [FAIL] se esperaba que SÍ cumpliera (requiere_rediseno=false en Tabla75)"); }
        totalAserciones++;
    }

    Console.WriteLine("-- Diseño a flexión (sección 8.1) -- autoconsistencia y casos límite --");
    // CORREGIDO 2026-08-28 (auditoría externa, segunda ronda, H-CRÍTICO-1/H-ALTO-2): el signo de
    // BA/CA estaba invertido (raíz mayor, brazo de palanca d+a/2 físicamente imposible) y no se
    // aplicaba φ -- ver el docstring de DisenarFlexion para el detalle completo. La autoconsistencia
    // de abajo por sí sola NUNCA habría detectado ese error (reinserta ρ en la MISMA ecuación
    // implementada, que por construcción siempre cuadra tenga o no el signo correcto) -- por eso se
    // complementa, más abajo, con una verificación INDEPENDIENTE (forma cerrada de ACI 318 vía la
    // profundidad del bloque de compresión "a", sin reutilizar el coeficiente 0.59 del propio
    // código) que sí lo habría atrapado.
    {
        var f = DisenoFlexionCortanteFisuracion.DisenarFlexion(muKNm: 36.53, dM: 0.15, bM: 1.0, fyMPa: fy, fcMPa: fc);
        totalAserciones++;
        var aaCheck = 0.59 * (fy * 1000.0) * (fy * 1000.0) * 1.0 * 0.15 * 0.15 / (fc * 1000.0);
        var baCheck = (fy * 1000.0) * 1.0 * 0.15 * 0.15;
        const double phiCheck = 0.9; // default de DisenarFlexion
        var mnReconstruido = baCheck * f.Rho - aaCheck * f.Rho * f.Rho; // Mn(ρ) = BA·ρ - AA·ρ² (brazo d-a/2)
        var muReconstruido = phiCheck * mnReconstruido; // φ·Mn = Mu
        if (Math.Abs(muReconstruido - 36.53) > 0.01)
        { fallos++; Console.WriteLine($"  [FAIL] autoconsistencia φ·(BA·ρ-AA·ρ²)=Mu: reconstruido={muReconstruido:0.####}, esperado=36.53"); }
        else Console.WriteLine($"  [OK  ] autoconsistencia φ·(BA·ρ-AA·ρ²)=Mu: reconstruido={muReconstruido:0.####} == 36.53");

        var asEsperado = f.Rho * 1.0 * 0.15 * 1_000_000.0;
        AssertTol("As = ρ·b·d·1,000,000 (autoconsistencia)", f.AsRequeridoMm2, asEsperado, atol: 0.001);
    }
    {
        // Mu muy pequeño -> se satura en la cuantía mínima (0.0018), no en la raíz de la cuadrática.
        var fMin = DisenoFlexionCortanteFisuracion.DisenarFlexion(muKNm: 0.5, dM: 0.15, bM: 1.0, fyMPa: fy, fcMPa: fc);
        AssertTol("ρ saturado en cuantía mínima con Mu pequeño", fMin.Rho, DisenoFlexionCortanteFisuracion.CuantiaMinima, atol: 1e-9);
    }
    {
        // Mu excesivo para el espesor dado -> debe lanzar EspesorInsuficienteException. Con la
        // corrección de signo, este caso concreto (Mu=500kNm, d=0.10m) ahora dispara la rama de
        // discriminante negativo (ningún ρ real, ni siquiera por encima de CuantiaMaxima, alcanza
        // ese Mn) en vez de la rama "rhoRequerido > CuantiaMaxima" -- ambas ramas son
        // EspesorInsuficienteException, así que la aserción sigue siendo válida sin cambios.
        totalAserciones++;
        try
        {
            DisenoFlexionCortanteFisuracion.DisenarFlexion(muKNm: 500.0, dM: 0.10, bM: 1.0, fyMPa: fy, fcMPa: fc);
            fallos++;
            Console.WriteLine("  [FAIL] se esperaba EspesorInsuficienteException con Mu=500kNm, d=0.10m");
        }
        catch (EspesorInsuficienteException)
        {
            Console.WriteLine("  [OK  ] EspesorInsuficienteException lanzada correctamente con Mu excesivo para d dado");
        }
    }

    Console.WriteLine("-- Diseño a flexión (sección 8.1) -- verificación INDEPENDIENTE (forma cerrada ACI 318, hallazgo H-CRÍTICO-1/H-ALTO-2) --");
    // Recomendación §7.3 del informe de auditoría externa (segunda ronda, 2026-08-28): una
    // comprobación que NO reutilice la ecuación cuadrática AA/BA/CA del propio código, para que un
    // futuro error de signo/álgebra en DisenarFlexion sí quede atrapado. Deriva "a" (profundidad del
    // bloque de compresión de Whitney) directamente de Mn=0.85·f'c·a·b·(d-a/2), con el coeficiente
    // EXACTO 0.85 (no el 0.59 redondeado -- ≈1/1.7 -- que usa el código), así que la comparación
    // tolera una pequeña diferencia de redondeo (~0.2%) entre ambos caminos, no coincidencia exacta.
    ResultadoDisenoFlexion FlexionIndependiente(double muKNm, double dM, double bM, double fyMPa, double fcMPa, double phi)
    {
        var fcKPa = fcMPa * 1000.0;
        var fyKPa = fyMPa * 1000.0;
        var muDisenoKNm = muKNm / phi;
        // 0.425·f'c·b·a² - 0.85·f'c·b·d·a + Mu/φ = 0  (despejando "a" de Mn=0.85·f'c·b·a·(d-a/2))
        var aCoef = 0.425 * fcKPa * bM;
        var bCoef = -0.85 * fcKPa * bM * dM;
        var cCoef = muDisenoKNm;
        var disc = bCoef * bCoef - 4 * aCoef * cCoef;
        if (disc < 0) throw new EspesorInsuficienteException(double.PositiveInfinity, DisenoFlexionCortanteFisuracion.CuantiaMaxima);
        var a = (-bCoef - Math.Sqrt(disc)) / (2 * aCoef); // raíz menor -> a más pequeño -> sección controlada por tracción
        var asMm2 = 0.85 * fcKPa * bM * a / fyKPa * 1_000_000.0;
        var rho = asMm2 / (bM * dM * 1_000_000.0);
        if (rho > DisenoFlexionCortanteFisuracion.CuantiaMaxima)
            throw new EspesorInsuficienteException(rho, DisenoFlexionCortanteFisuracion.CuantiaMaxima);
        var rhoClamp = Math.Max(rho, DisenoFlexionCortanteFisuracion.CuantiaMinima);
        return new ResultadoDisenoFlexion(rhoClamp, rhoClamp * bM * dM * 1_000_000.0);
    }
    {
        // Caso 1: la flexión gobierna sobre la cuantía mínima (el caso que hoy fallaría sin el fix).
        var codigo = DisenoFlexionCortanteFisuracion.DisenarFlexion(muKNm: 36.53, dM: 0.15, bM: 1.0, fyMPa: fy, fcMPa: fc);
        var independiente = FlexionIndependiente(36.53, 0.15, 1.0, fy, fc, 0.9);
        AssertTol("ρ (Mu=36.53, flexión gobierna) -- código vs. forma cerrada independiente", codigo.Rho, independiente.Rho, atol: 0.00002, rtol: 0.003);
    }
    {
        // Caso 2: la cuantía mínima gobierna -- el ρ crudo (sin acotar) de AMBOS caminos debe caer
        // por debajo de CuantiaMinima, confirmando que el acotamiento a 0.0018 es legítimo y no un
        // error que enmascara un ρ requerido mayor.
        const double muPequeno = 5.0;
        var fcKPa = fc * 1000.0; var fyKPa = fy * 1000.0;
        var aCoef = 0.425 * fcKPa * 1.0; var bCoef = -0.85 * fcKPa * 1.0 * 0.15; var cCoef = muPequeno / 0.9;
        var disc = bCoef * bCoef - 4 * aCoef * cCoef;
        var aRaw = (-bCoef - Math.Sqrt(disc)) / (2 * aCoef);
        var rhoIndependienteCrudo = 0.85 * fcKPa * 1.0 * aRaw / fyKPa * 1_000_000.0 / (1.0 * 0.15 * 1_000_000.0);
        var codigo = DisenoFlexionCortanteFisuracion.DisenarFlexion(muKNm: muPequeno, dM: 0.15, bM: 1.0, fyMPa: fy, fcMPa: fc);
        totalAserciones++;
        if (rhoIndependienteCrudo >= DisenoFlexionCortanteFisuracion.CuantiaMinima || Math.Abs(codigo.Rho - DisenoFlexionCortanteFisuracion.CuantiaMinima) > 1e-9)
        { fallos++; Console.WriteLine($"  [FAIL] cuantía mínima: ρ_crudo_independiente={rhoIndependienteCrudo:0.######} (debía ser < 0.0018), código.Rho={codigo.Rho:0.######}"); }
        else Console.WriteLine($"  [OK  ] cuantía mínima gobierna, confirmado por forma cerrada independiente: ρ_crudo={rhoIndependienteCrudo:0.######} < 0.0018");
    }
    {
        // Caso 3: Mu excede lo que la sección puede desarrollar -- la forma cerrada independiente
        // también debe fallar (discriminante negativo o ρ > CuantiaMaxima), confirmando que la
        // EspesorInsuficienteException del código (caso ya probado arriba) no es un artefacto de la
        // ecuación AA/BA/CA en sí.
        totalAserciones++;
        try
        {
            FlexionIndependiente(500.0, 0.10, 1.0, fy, fc, 0.9);
            fallos++;
            Console.WriteLine("  [FAIL] se esperaba que la forma cerrada independiente también fallara con Mu=500kNm, d=0.10m");
        }
        catch (EspesorInsuficienteException)
        {
            Console.WriteLine("  [OK  ] forma cerrada independiente confirma EspesorInsuficienteException con Mu=500kNm, d=0.10m (no es un artefacto de AA/BA/CA)");
        }
    }
}

Console.WriteLine("=== Placas rectangulares PCA/Marcus (F.7, 10-11) -- ejercicio 1, placa de fondo (r=1.0 exacto) ===");
{
    var raiz1 = JsonNode.Parse(File.ReadAllText(Path.Combine(casosDir, "ejercicio_1_tanque_lados_iguales.json")))!;
    var apf = raiz1["resultados_esperados"]!["analisis_placa_fondo"]!;
    var subpresionKNm2 = apf["revision_flotacion"]!["subpresion_kNm2"]!.GetValue<double>();
    var aM = Geo(raiz1, "B_ancho_m"); // B=L=4.5m en este ejercicio -> r=1.0 exacto, a=b=4.5

    // Uso directo del motor general (no de las envolturas CalcularPlacaCubierta/Fondo, que además
    // aplican su propia combinación de carga U/k1-k3) -- aquí se usa directamente la subpresión ya
    // publicada por la tesis como "q" para aislar la verificación del mecanismo Marcus en sí.
    var resultado = PlacasRectangulares.Calcular(r: 1.0, qKNm2: subpresionKNm2, aM: aM);

    Console.WriteLine("-- Cortante (Tabla 24) --");
    // Cs=0.34 en las 4 ubicaciones para r=1.0 -- ver Cs = V/(q*a).
    AssertTol("Cs bottom_edge_midpoint (r=1.0, vía Vx)", resultado.VxKNm / (subpresionKNm2 * aM), 0.34, atol: 0.001);
    AssertTol("Cs side_edge_maximum (r=1.0, vía Vy)", resultado.VyKNm / (subpresionKNm2 * aM), 0.34, atol: 0.001);
    // El propio V publicado en la Tabla 24 (8.97 kN) no reconcilia exactamente con Cs×q×a=9.006 kN
    // (fórmula del propio manual PCA, ya confirmada dígito a dígito contra las Tablas 26-29 de
    // momento del mismo ejercicio -- ver más abajo) -- diferencia de 0.4%, fuera de la tolerancia
    // estándar del proyecto. Se documenta como discrepancia abierta de la tesis, no se fuerza.
    Console.WriteLine($"  [NOTA] V=Cs×q×a={resultado.VxKNm:0.###} kN -- la Tabla 24 publica 8.97 kN (diff ~0.4%, discrepancia abierta de la tesis, no de la fórmula: ver más abajo la coincidencia exacta de las Tablas 26-29 con esta misma fórmula/datos).");

    int CeldaRedondeada(double[,] campo, int fila, int col) => (int)Math.Round(campo[fila, col], MidpointRounding.AwayFromZero);

    void VerificarCampo(string nombreTabla, double[,] campoCalculado, JsonNode tablaJson, (int fila, int col, string motivo)[]? excepciones = null)
    {
        var arr = tablaJson.AsArray();
        var celdasExcepcion = 0;
        for (var fila = 0; fila < 6; fila++)
        {
            var filaJson = arr[fila]!.AsArray();
            for (var col = 0; col < 6; col++)
            {
                totalAserciones++;
                var esperado = filaJson[col]!.GetValue<int>();
                var actual = CeldaRedondeada(campoCalculado, fila, col);
                var excepcion = excepciones?.FirstOrDefault(e => e.fila == fila && e.col == col);
                if (actual != esperado)
                {
                    if (excepcion is { } ex && ex.motivo != null)
                    {
                        celdasExcepcion++;
                        Console.WriteLine($"  [NOTA] {nombreTabla}[{fila},{col}]: actual={campoCalculado[fila, col]:0.###} (redondeado {actual}), tesis publica {esperado} -- divergencia esperada y documentada: {ex.motivo}");
                    }
                    else
                    {
                        fallos++;
                        Console.WriteLine($"  [FAIL] {nombreTabla}[{fila},{col}]: actual={campoCalculado[fila, col]:0.###} (redondeado {actual}), esperado={esperado}");
                    }
                }
            }
        }
        var sufijo = celdasExcepcion > 0 ? $" ({celdasExcepcion} celda(s) con divergencia documentada, ver [NOTA] arriba)" : "";
        Console.WriteLine($"  [OK  ] {nombreTabla}: las 36 celdas (filas TOP..0.5a) coinciden con la tesis tras redondear al entero más cercano{sufijo}.");
    }

    Console.WriteLine("-- Momentos (Tablas 26-29, mecanismo Marcus completo) --");
    var momentos = apf["momentos_kNm"]!;
    // [5,3] (fila "0.5a", columna "0.3b"): Cmx(r=1.0) en esta celda es la anomalía de baja
    // confianza ya documentada en tabla_placa_biaxial.json (hallazgo_adicional_Cmx_r1) -- el
    // bytecode original fija 37, el manual PCA (releído, doble lectura del resto de la tabla, pero
    // esta celda puntual con una sola relectura) publica 38. Tanque.Core usa 38 (el manual, fuente
    // de verdad elegida para todo el módulo) por política del proyecto; la tesis, al haber
    // ejecutado el programa original (con el bug de 37 en esa celda), reproduce 37 -> 4 redondeado
    // en vez de los 5 que da Tanque.Core con 38. Es la única celda de las 144 verificadas (36 × 4
    // tablas) que no reconcilia -- divergencia esperada, no un error de Tanque.Core.
    (int, int, string)[] excepcionCmxR1 = [(5, 3, "anomalía Cmx[5,3](r=1.0) manual=38 vs bytecode=37, ver tabla_placa_biaxial.json")];
    VerificarCampo("Mx_pos (Tabla 26, positivo_X)", resultado.CampoMxPos, momentos["positivo_X_tabla26"]!, excepcionCmxR1);
    VerificarCampo("My_pos (Tabla 27, positivo_Y)", resultado.CampoMyPos, momentos["positivo_Y_tabla27"]!);
    VerificarCampo("Mx_neg (Tabla 28, negativo_X -- signo preservado)", resultado.CampoMxNeg, momentos["negativo_X_tabla28"]!);
    VerificarCampo("My_neg (Tabla 29, negativo_Y -- signo preservado)", resultado.CampoMyNeg, momentos["negativo_Y_tabla29"]!);
}

Console.WriteLine();
Console.WriteLine("=== Placas rectangulares -- interpolación, rango tabulado y cargas de diseño (F.7, 10-11) ===");
{
    // Extremos tabulados (r=4.0 y r=0.5): no deben lanzar.
    totalAserciones++;
    try { PlacasRectangulares.Calcular(PlacasRectangulares.RMaximo, 10.0, 3.0); Console.WriteLine("  [OK  ] r=RMaximo (4.0) no lanza"); }
    catch { fallos++; Console.WriteLine("  [FAIL] r=RMaximo lanzó excepción inesperada"); }

    totalAserciones++;
    try { PlacasRectangulares.Calcular(PlacasRectangulares.RMinimo, 10.0, 3.0); Console.WriteLine("  [OK  ] r=RMinimo (0.5) no lanza"); }
    catch { fallos++; Console.WriteLine("  [FAIL] r=RMinimo lanzó excepción inesperada"); }

    // Fuera de rango: debe lanzar (Tanque.Core no extrapola sin respaldo normativo).
    totalAserciones++;
    try { PlacasRectangulares.Calcular(4.5, 10.0, 3.0); fallos++; Console.WriteLine("  [FAIL] r=4.5 (fuera de rango) no lanzó"); }
    catch (ArgumentOutOfRangeException) { Console.WriteLine("  [OK  ] r=4.5 (fuera de rango) lanza ArgumentOutOfRangeException"); }

    // Interpolación lineal exacta en un punto tabulado (r=1.5 -> Cs bottom_edge_midpoint=0.42).
    var r15 = PlacasRectangulares.Calcular(1.5, 1.0, 1.0);
    AssertTol("Cs bottom_edge_midpoint(r=1.5), valor tabulado exacto", r15.VxKNm, 0.42, atol: 1e-9);

    // Punto medio interpolado entre r=1.0 (Cs=0.34) y r=1.25 (Cs=0.39): en r=1.125 -> 0.365.
    var rMedio = PlacasRectangulares.Calcular(1.125, 1.0, 1.0);
    AssertTol("Cs bottom_edge_midpoint interpolado en r=1.125 (entre 0.34 y 0.39)", rMedio.VxKNm, 0.365, atol: 1e-9);

    // Hallazgo 12: la carga de diseño de cubierta corregida usa 1.2×D (no la constante 1.2 sin
    // multiplicar, bug de literal Decimal "1.2D" de VB.NET del programa original -- ver docstring
    // de CalcularCargaDisenoCubierta).
    // D, CV, CG elegidos deliberadamente para que C3 sea la combinación gobernante (C3 > C1, C3 >
    // C2) -- así el caso de prueba SÍ distingue el valor corregido del valor con el bug del
    // original (con D pequeño ambos coinciden por casualidad, ya que 1.2×D≈1.2).
    var d = 10.0; var cv = 0.1; var cg = 3.0;
    var uCubierta = PlacasRectangulares.CalcularCargaDisenoCubierta(d, cv, cg);
    var c1 = 1.4 * d; var c2 = 1.2 * d + 1.6 * cv;
    var c3Corregido = 1.2 * d + cv + cg;
    var c3ConBugDelOriginal = 1.2 + cv + cg; // NO debe coincidir con uCubierta -- confirma que el bug no se heredó
    AssertTol("U cubierta = max(1.4D, 1.2D+1.6CV, 1.2D+CV+CG corregido), con C3 gobernando", uCubierta, Math.Max(c1, Math.Max(c2, c3Corregido)), atol: 1e-9);
    totalAserciones++;
    if (Math.Abs(uCubierta - Math.Max(c1, Math.Max(c2, c3ConBugDelOriginal))) < 1e-9)
    { fallos++; Console.WriteLine("  [FAIL] U cubierta coincide con el resultado del bug del original (hallazgo 12 no corregido)"); }
    else Console.WriteLine($"  [OK  ] U cubierta ({uCubierta:0.###}) NO reproduce el bug de literal Decimal \"1.2D\" del programa original (que hubiera dado {Math.Max(c1, Math.Max(c2, c3ConBugDelOriginal)):0.###}) -- hallazgo 12 corregido");
}

Console.WriteLine();
Console.WriteLine("=== Placas rectangulares -- MetodoInterpolacion.RedondearSuperior (backlog v2) ===");
{
    // r=1.125 esta entre los tabulados 1.00 (Cs=0.34) y 1.25 (Cs=0.39). RedondearSuperior debe
    // usar el tabulado INMEDIATAMENTE SUPERIOR (1.25 -> 0.39), nunca interpolar.
    var redondeado = PlacasRectangulares.Calcular(1.125, 1.0, 1.0, MetodoInterpolacion.RedondearSuperior);
    AssertTol("RedondearSuperior en r=1.125 usa el Cs del tabulado superior (r=1.25 -> 0.39), no el interpolado (0.365)", redondeado.VxKNm, 0.39, atol: 1e-9);

    var interpolado = PlacasRectangulares.Calcular(1.125, 1.0, 1.0, MetodoInterpolacion.Interpolar);
    totalAserciones++;
    if (Math.Abs(redondeado.VxKNm - interpolado.VxKNm) < 1e-9)
    { fallos++; Console.WriteLine("  [FAIL] RedondearSuperior coincide con Interpolar en un punto no tabulado (deberian diferir)"); }
    else Console.WriteLine($"  [OK  ] RedondearSuperior ({redondeado.VxKNm:0.###}) difiere de Interpolar ({interpolado.VxKNm:0.###}) en r=1.125, como se espera");

    // Coincidencia EXACTA con un valor tabulado: ambos metodos deben concordar (r=1.25 -> 0.39).
    var exactoRedondeado = PlacasRectangulares.Calcular(1.25, 1.0, 1.0, MetodoInterpolacion.RedondearSuperior);
    AssertTol("RedondearSuperior en r=1.25 EXACTO coincide con el valor tabulado (0.39), no salta al siguiente", exactoRedondeado.VxKNm, 0.39, atol: 1e-9);

    // Con q y a por defecto, RedondearSuperior tambien debe producir momentos gobernantes
    // consistentes con las tablas del manual en r=1.5 (tabulado): estructuralmente identico al
    // metodo por defecto en ese punto.
    var m15Redondeado = PlacasRectangulares.Calcular(1.5, 1.0, 1.0, MetodoInterpolacion.RedondearSuperior);
    var m15Interpolado = PlacasRectangulares.Calcular(1.5, 1.0, 1.0, MetodoInterpolacion.Interpolar);
    AssertTol("En r=1.5 (tabulado exacto), RedondearSuperior y Interpolar dan el mismo MxPosGobernante", m15Redondeado.MxPosGobernanteKNmM, m15Interpolado.MxPosGobernanteKNmM, atol: 1e-9);
}


Console.WriteLine("=== Muros rectangulares PCA/Marcus (F.8-9) -- ejercicio 1, r=1.5 exacto ===");
{
    var raiz1 = JsonNode.Parse(File.ReadAllText(Path.Combine(casosDir, "ejercicio_1_tanque_lados_iguales.json")))!;
    var am = raiz1["resultados_esperados"]!["analisis_muros"]!;
    var proyecto1 = ProyectoDesde(raiz1);
    var presiones1 = PresionesLaterales.Calcular(proyecto1);

    var HL = Geo(raiz1, "HL_altura_liquido_m");
    var Hm = Geo(raiz1, "Hm_altura_suelo_sobre_muro_m");
    var L = Geo(raiz1, "L_largo_m");

    Console.WriteLine("-- Cortante (Tabla 17) -- condicion 1 (interior/liquido) --");
    var q1 = MurosRectangulares.CalcularCargaMuroInterior(presiones1.PhMaximaKNm2);
    AssertTol("q1 = 1.4 x Ph_maxima (B.2.4-1)", q1, 41.202, atol: 0.01);
    var r1 = L / HL;
    AssertTol("r condicion 1 (=L/HL)", r1, 1.5, atol: 1e-9);
    var resultado1 = MurosRectangulares.Calcular(r1, r1, q1, HL, true);
    AssertTol("V fondo, punto medio (Tabla 17, condicion 1)", resultado1.VBottomKNm, 49.44, atol: 0.02);
    AssertTol("V lateral, valor maximo (Tabla 17, condicion 1)", resultado1.VSideMaxKNm, 32.13, atol: 0.02);
    AssertTol("V lateral, punto medio (Tabla 17, condicion 1)", resultado1.VSideMidKNm, 32.13, atol: 0.02);

    Console.WriteLine("-- Cortante (Tabla 17) -- condicion 2 (exterior/suelo, Ka corregido -- hallazgo 1) --");
    var q2 = MurosRectangulares.CalcularCargaMuroExterior(presiones1.Ps2MaximaKNm2);
    var r2 = L / Hm;
    var resultado2 = MurosRectangulares.Calcular(r2, r2, q2, Hm, true);
    // La tesis publico estos valores usando el Ka INCORRECTO (hallazgo 1) -- ver docstring de la
    // clase. Con el Ka corregido que usa Tanque.Core, el resultado NO debe coincidir con la Tabla
    // 17 (discriminador, igual que el hallazgo 12 de Placas), y en su lugar debe coincidir con la
    // version recalculada con Ka correcto.
    var kaCorrecto = presiones1.Ka;
    var q2ConBug = 1.6 * (0.254366 /*Ka incorrecto hallazgo 1, phi=32*/) * Mat(raiz1, "gamma_suelo_kNm3") * Hm;
    totalAserciones++;
    if (Math.Abs(resultado2.VBottomKNm - 23.44) < 0.05)
    { fallos++; Console.WriteLine($"  [FAIL] V fondo condicion 2 coincide con la Tabla 17 (23.44 kN) -- el Ka corregido (hallazgo 1) debia dar un resultado distinto"); }
    else Console.WriteLine($"  [OK  ] V fondo condicion 2 con Ka corregido ({resultado2.VBottomKNm:0.###} kN) difiere de la Tabla 17 (23.44 kN, calculada con el Ka incorrecto del hallazgo 1) -- confirmado: hallazgo 1 no se hereda");
    var resultado2ConBug = MurosRectangulares.Calcular(r2, r2, q2ConBug, Hm, true);
    AssertTol("V fondo condicion 2, RECALCULADO con el Ka incorrecto del hallazgo 1 (discriminador)", resultado2ConBug.VBottomKNm, 23.44, atol: 0.05);

    Console.WriteLine("-- Momentos (Tablas 20/21, condicion 1 -- campos crudos Mx/My sin combinar) --");
    var mom = am["momentos_kNm"]!;

    void VerificarCampoMuro(string nombreTabla, double[,] campoCalculado, JsonNode tablaJson, (int fila, int col, string motivo)[]? excepciones = null)
    {
        var arr = tablaJson.AsArray();
        for (var fila = 0; fila < 11; fila++)
        {
            var filaJson = arr[fila]!.AsArray();
            for (var col = 0; col < 6; col++)
            {
                var esperado = filaJson[col]!.GetValue<double>();
                var actual = campoCalculado[fila, col];
                var excepcion = excepciones?.FirstOrDefault(e => e.fila == fila && e.col == col);
                totalAserciones++;
                var diff = Math.Abs(actual - esperado);
                if (excepcion is { } ex && ex.motivo != null)
                {
                    Console.WriteLine($"  [NOTA] {nombreTabla}[{fila},{col}]: actual={actual:0.###}, publicado={esperado} -- diff={diff:0.###} -- {ex.motivo} (no cuenta como fallo)");
                    continue;
                }
                if (diff > 0.02)
                {
                    fallos++;
                    Console.WriteLine($"  [FAIL] {nombreTabla}[{fila},{col}]: actual={actual:0.###}, publicado={esperado}, diff={diff:0.###}");
                }
            }
        }
        Console.WriteLine($"  [OK  ] {nombreTabla}: 66 celdas verificadas (excepciones documentadas aparte, si las hay).");
    }

    VerificarCampoMuro("Mx (Tabla 20, condicion 1)", resultado1.CampoMx, mom["condicion_1_Mx_tabla20"]!);
    VerificarCampoMuro("My (Tabla 21, condicion 1)", resultado1.CampoMy, mom["condicion_1_My_tabla21"]!,
        [(7, 3, "discrepancia aislada de transcripcion de la tesis -- publicado 2.6, calculado 2.967; ninguna otra de las 65 celdas restantes de esta tabla difiere en mas de 0.02 -- ver docstring de MurosRectangulares")]);

    Console.WriteLine("-- Momentos (Tablas 22/23, condicion 2 -- RECALCULADOS con el Ka incorrecto del hallazgo 1, discriminador) --");
    var resultado2ConBugParaMomentos = MurosRectangulares.Calcular(r2, r2, q2ConBug, Hm, true);
    VerificarCampoMuro("Mx (Tabla 22, condicion 2, Ka hallazgo1)", resultado2ConBugParaMomentos.CampoMx, mom["condicion_2_Mx_tabla22"]!);
    VerificarCampoMuro("My (Tabla 23, condicion 2, Ka hallazgo1)", resultado2ConBugParaMomentos.CampoMy, mom["condicion_2_My_tabla23"]!,
        [(7, 3, "misma discrepancia aislada de transcripcion que en la Tabla 21, escalada proporcionalmente -- ver nota anterior")]);
}

Console.WriteLine();
Console.WriteLine("=== Muros rectangulares -- interpolacion, rango tabulado y combinacion Marcus ===");
{
    totalAserciones++;
    try { MurosRectangulares.Calcular(MurosRectangulares.RMaximo, MurosRectangulares.RMaximo, 10.0, 3.0, true); Console.WriteLine("  [OK  ] r=RMaximo (4.0) no lanza"); }
    catch { fallos++; Console.WriteLine("  [FAIL] r=RMaximo lanzo excepcion inesperada"); }

    totalAserciones++;
    try { MurosRectangulares.Calcular(MurosRectangulares.RMinimo, MurosRectangulares.RMinimo, 10.0, 3.0, true); Console.WriteLine("  [OK  ] r=RMinimo (0.5) no lanza"); }
    catch { fallos++; Console.WriteLine("  [FAIL] r=RMinimo lanzo excepcion inesperada"); }

    totalAserciones++;
    try { MurosRectangulares.Calcular(4.5, 4.5, 10.0, 3.0, true); fallos++; Console.WriteLine("  [FAIL] r=4.5 (fuera de rango) no lanzo"); }
    catch (ArgumentOutOfRangeException) { Console.WriteLine("  [OK  ] r=4.5 (fuera de rango) lanza ArgumentOutOfRangeException"); }

    // Interpolacion lineal exacta en un punto tabulado (r=2.0 -> Cs bottom_edge_midpoint=0.45).
    var r20 = MurosRectangulares.Calcular(2.0, 2.0, 1.0, 1.0, true);
    AssertTol("Cs bottom_edge_midpoint(r=2.0), valor tabulado exacto", r20.VBottomKNm, 0.45, atol: 1e-9);

    // Punto medio interpolado entre r=1.0 (Cs=0.32) y r=1.25 (Cs=0.36): en r=1.125 -> 0.34.
    var rMedio = MurosRectangulares.Calcular(1.125, 1.125, 1.0, 1.0, true);
    AssertTol("Cs bottom_edge_midpoint interpolado en r=1.125 (entre 0.32 y 0.36)", rMedio.VBottomKNm, 0.34, atol: 1e-9);

    // MetodoInterpolacion.RedondearSuperior (backlog v2): en r=1.125 debe usar el tabulado
    // INMEDIATAMENTE SUPERIOR (r=1.25 -> Cs=0.36), no el valor interpolado (0.34).
    var rMedioRedondeado = MurosRectangulares.Calcular(1.125, 1.125, 1.0, 1.0, true, MetodoInterpolacion.RedondearSuperior);
    AssertTol("RedondearSuperior en r=1.125 usa Cs del tabulado superior (r=1.25 -> 0.36), no el interpolado (0.34)", rMedioRedondeado.VBottomKNm, 0.36, atol: 1e-9);
    totalAserciones++;
    if (Math.Abs(rMedioRedondeado.VBottomKNm - rMedio.VBottomKNm) < 1e-9)
    { fallos++; Console.WriteLine("  [FAIL] RedondearSuperior coincide con Interpolar en un punto no tabulado (deberian diferir)"); }
    else Console.WriteLine($"  [OK  ] RedondearSuperior ({rMedioRedondeado.VBottomKNm:0.###}) difiere de Interpolar ({rMedio.VBottomKNm:0.###}) en r=1.125, como se espera");

    // Coincidencia EXACTA con un tabulado: ambos metodos concuerdan (r=2.0 -> Cs=0.45).
    var exactoRedondeado = MurosRectangulares.Calcular(2.0, 2.0, 1.0, 1.0, true, MetodoInterpolacion.RedondearSuperior);
    AssertTol("RedondearSuperior en r=2.0 EXACTO coincide con el valor tabulado (0.45)", exactoRedondeado.VBottomKNm, 0.45, atol: 1e-9);

    // Combinacion Marcus: los campos "pos" deben ser siempre >=0 y los "neg" siempre <=0 en toda
    // la grilla, y los valores gobernantes deben ser el maximo/minimo real de sus 66 celdas (no
    // una celda fija) -- verificacion estructural, no numerica contra un oraculo (no hay tabla
    // publicada de momentos combinados de muro en el banco de pruebas -- ver docstring de la clase).
    var m = MurosRectangulares.Calcular(1.5, 1.5, 30.0, 3.0, true);
    var todosPosOk = true; var todosNegOk = true;
    double maxPosReal = double.NegativeInfinity, minNegReal = double.PositiveInfinity;
    for (var i = 0; i < 11; i++)
        for (var j = 0; j < 6; j++)
        {
            if (m.CampoMxPos[i, j] < 0) todosPosOk = false;
            if (m.CampoMxNeg[i, j] > 0) todosNegOk = false;
            maxPosReal = Math.Max(maxPosReal, m.CampoMxPos[i, j]);
            minNegReal = Math.Min(minNegReal, m.CampoMxNeg[i, j]);
        }
    totalAserciones++;
    if (todosPosOk) Console.WriteLine("  [OK  ] CampoMxPos siempre >= 0 en las 66 celdas");
    else { fallos++; Console.WriteLine("  [FAIL] CampoMxPos tiene celdas negativas"); }
    totalAserciones++;
    if (todosNegOk) Console.WriteLine("  [OK  ] CampoMxNeg siempre <= 0 en las 66 celdas");
    else { fallos++; Console.WriteLine("  [FAIL] CampoMxNeg tiene celdas positivas"); }
    AssertTol("MxPosGobernante = maximo real de las 66 celdas (no celda fija)", m.MxPosGobernanteKNmM, maxPosReal, atol: 1e-9);
    AssertTol("MxNegGobernante = magnitud del minimo real de las 66 celdas", m.MxNegGobernanteKNmM, -minNegReal, atol: 1e-9);
}
Console.WriteLine();
Console.WriteLine("=== Muros -- combinaciones sismicas aumentadas (Capitulo 3, Caso 7 PCA + NSR-10 B.2.4-5/7) ===");
{
    Console.WriteLine("-- Interpolacion exacta en un punto tabulado (b/a=2.0, c/a=1.5, coincide con ejercicio 2 de la tesis) --");
    // q=1000, a=1 -> escala=q*a^2/1000=1, entonces CampoMx/Mz reproducen el coeficiente tabulado sin escalar.
    var c7 = MurosRectangularesSismico.Calcular(2.0, 1.5, 1000.0, 1.0);
    AssertTol("Mx[TOP,CORNER] lado largo, b/a=2.0 c/a=1.5", c7.LadoLargo.CampoMx[0, 0], -33, atol: 1e-9);
    AssertTol("Mx[BOT,0.5b] lado largo, b/a=2.0 c/a=1.5", c7.LadoLargo.CampoMx[10, 5], -221, atol: 1e-9);
    AssertTol("Mz[TOP,CORNER] lado corto, b/a=2.0 c/a=1.5", c7.LadoCorto.CampoMx[0, 0], -33, atol: 1e-9);
    AssertTol("Mz[BOT,0.5b] lado corto, b/a=2.0 c/a=1.5", c7.LadoCorto.CampoMx[10, 5], -106, atol: 1e-9);
    AssertTol("Mxy[0.5a,0.2b] lado largo, b/a=2.0 c/a=1.5", c7.LadoLargo.CampoMxy[5, 2], 47, atol: 1e-9);
    totalAserciones++;
    if (Math.Abs(c7.BSobreA - 2.0) < 1e-9 && Math.Abs(c7.CSobreA - 1.5) < 1e-9)
        Console.WriteLine("  [OK  ] BSobreA/CSobreA expuestos correctamente");
    else { fallos++; Console.WriteLine("  [FAIL] BSobreA/CSobreA no coinciden con lo pedido"); }

    Console.WriteLine("-- Interpolacion bilineal en un punto intermedio (b/a=2.5, c/a=1.0, entre filas b/a=2.0 y 3.0) --");
    var c7Interp = MurosRectangularesSismico.Calcular(2.5, 1.0, 1000.0, 1.0);
    // Verificacion estructural (no hay oraculo publicado): el valor interpolado debe caer
    // estrictamente entre los dos valores tabulados en las filas b/a=2.0 y b/a=3.0 para el mismo c/a=1.0.
    var c7B20 = MurosRectangularesSismico.Calcular(2.0, 1.0, 1000.0, 1.0);
    var c7B30 = MurosRectangularesSismico.Calcular(3.0, 1.0, 1000.0, 1.0);
    var vInterp = c7Interp.LadoLargo.CampoMx[10, 5];
    var vB20 = c7B20.LadoLargo.CampoMx[10, 5];
    var vB30 = c7B30.LadoLargo.CampoMx[10, 5];
    totalAserciones++;
    if (vInterp <= Math.Max(vB20, vB30) + 1e-6 && vInterp >= Math.Min(vB20, vB30) - 1e-6)
        Console.WriteLine($"  [OK  ] Mx[BOT,0.5b] interpolado en b/a=2.5 ({vInterp:0.###}) esta entre los valores de b/a=2.0 ({vB20:0.###}) y b/a=3.0 ({vB30:0.###})");
    else { fallos++; Console.WriteLine($"  [FAIL] Mx[BOT,0.5b] interpolado ({vInterp:0.###}) fuera del rango [{Math.Min(vB20, vB30):0.###}, {Math.Max(vB20, vB30):0.###}]"); }

    Console.WriteLine("-- Validacion de rango: b/a, c/a y el limite escalonado de la grilla --");
    totalAserciones++;
    try { MurosRectangularesSismico.Calcular(MurosRectangularesSismico.BSobreAMaximo, 0.5, 10, 1); Console.WriteLine("  [OK  ] b/a=BSobreAMaximo (4.0) no lanza"); }
    catch { fallos++; Console.WriteLine("  [FAIL] b/a=BSobreAMaximo lanzo excepcion inesperada"); }

    totalAserciones++;
    try { MurosRectangularesSismico.Calcular(0.9, 0.5, 10, 1); fallos++; Console.WriteLine("  [FAIL] b/a=0.9 (fuera de rango) no lanzo"); }
    catch (ArgumentOutOfRangeException) { Console.WriteLine("  [OK  ] b/a=0.9 (fuera de rango, < BSobreAMinimo) lanza ArgumentOutOfRangeException"); }

    totalAserciones++;
    try { MurosRectangularesSismico.Calcular(2.0, 2.5, 10, 1); fallos++; Console.WriteLine("  [FAIL] c/a=2.5 > b/a=2.0 no lanzo"); }
    catch (ArgumentOutOfRangeException) { Console.WriteLine("  [OK  ] c/a=2.5 > b/a=2.0 (c debe ser el lado corto) lanza ArgumentOutOfRangeException"); }

    totalAserciones++;
    // b/a=1.2 (entre las filas tabuladas b/a=1.0 [solo c/a=0.5] y b/a=1.5 [c/a hasta 1.0]) con
    // c/a=0.9: la fila b/a=1.0 no tiene dato para c/a=0.9 (solo tabula 0.5) -- debe lanzar, no
    // extrapolar silenciosamente (ver docstring de InterpolarCeldaC).
    try { MurosRectangularesSismico.Calcular(1.2, 0.9, 10, 1); fallos++; Console.WriteLine("  [FAIL] b/a=1.2, c/a=0.9 (fuera del rango escalonado de la fila b/a=1.0) no lanzo"); }
    catch (ArgumentOutOfRangeException) { Console.WriteLine("  [OK  ] b/a=1.2, c/a=0.9 (la fila vecina b/a=1.0 solo tabula c/a=0.5) lanza ArgumentOutOfRangeException -- no extrapola sin respaldo normativo"); }

    Console.WriteLine("-- Combinacion Marcus (estructural, mismo patron que MurosRectangulares) --");
    var todosPosOk = true; var todosNegOk = true;
    for (var i = 0; i < 11; i++)
        for (var j = 0; j < 6; j++)
        {
            if (c7.LadoLargo.CampoMxPos[i, j] < 0) todosPosOk = false;
            if (c7.LadoLargo.CampoMxNeg[i, j] > 0) todosNegOk = false;
        }
    totalAserciones++;
    if (todosPosOk) Console.WriteLine("  [OK  ] CampoMxPos (lado largo) siempre >= 0 en las 66 celdas");
    else { fallos++; Console.WriteLine("  [FAIL] CampoMxPos (lado largo) tiene celdas negativas"); }
    totalAserciones++;
    if (todosNegOk) Console.WriteLine("  [OK  ] CampoMxNeg (lado largo) siempre <= 0 en las 66 celdas");
    else { fallos++; Console.WriteLine("  [FAIL] CampoMxNeg (lado largo) tiene celdas positivas"); }

    Console.WriteLine("-- Formulas de carga sismica (NSR-10 B.2.4-5/B.2.4-7 con SRSS impulsiva/convectiva) --");
    // B.2.4-5: 1.2*Ph + SRSS(Pi,Pc). Con Ph=10, Pi=6, Pc=8 -> SRSS=10 -> q=1.2*10+10=22.
    AssertTol("CalcularCargaSismicaInterior (B.2.4-5, SRSS 6-8-10)", MurosRectangularesSismico.CalcularCargaSismicaInterior(10.0, 6.0, 8.0), 22.0, atol: 1e-9);
    // B.2.4-7: 1.6*Ps2 + Qae. Con Ps2=5, Qae=3 -> q=1.6*5+3=11.
    AssertTol("CalcularCargaSismicaExterior (B.2.4-7)", MurosRectangularesSismico.CalcularCargaSismicaExterior(5.0, 3.0), 11.0, atol: 1e-9);

    Console.WriteLine("-- Extremo a extremo: CalcularMuroLongitudinal/Transversal sobre una geometria sintetica (no cuadrada) --");
    // Geometría sintética NO cuadrada. B y L desplazadas +em respecto a las "redondas" (4.5/6.0)
    // para que, con la luz EJE A EJE (corrección 2026-08-31), sigan dando los ratios tabulados
    // b/a=2.0 y c/a=1.5 del Capítulo 3 Caso 7: (L-em)/HL=(6.25-0.25)/3.0=2.0, (B-em)/HL=(4.75-0.25)/3.0=1.5.
    var geoSint = new Geometria(BAnchoM: 4.75, LLargoM: 6.25, HtAlturaM: 3.5, ConTapa: false, EmEspesorMuroM: 0.25, EfEspesorFondoM: 0.20, EtEspesorTapaM: 0.0, HLAlturaLiquidoM: 3.0, HmAlturaSueloSobreMuroM: 3.0, WextSobrecargaKNm2: 0.0);
    var matSint = new Materiales(FcMPa: 21, FyMPa: 420, GammaConcretoKNm3: 24, GammaLiquidoKNm3: 9.81, GammaSueloKNm3: 16, PhiGradosAnguloFriccionSuelo: 30);
    var proyectoSint = new ProyectoTanque(geoSint, matSint);
    var presionesSint = PresionesLaterales.Calcular(proyectoSint);
    var espectroSint = new ParametrosEspectroDiseno(Aa: 0.2, Av: 0.2, Fa: 1.3, Fv: 2.0, I: 1.0, CondicionBase: CondicionBaseMuro.Rigida, CondicionAnclaje: CondicionAnclajeBase.ArticuladaEmpotrada);
    var sismoHidroSint = FuerzaSismicaHidrodinamica.Calcular(proyectoSint, espectroSint);
    var sueloSint = new ParametrosSueloDinamico(KhCoeficienteSismicoHorizontal: 0.2, KvCoeficienteSismicoVertical: 0.0, DeltaGradosFriccionSueloMuro: 0.0, IGradosInclinacionRelleno: 0.0, BetaGradosInclinacionMuro: 90.0);
    var sismoSueloSint = FuerzaDinamicaSuelo.Calcular(proyectoSint, sueloSint);

    totalAserciones++;
    try
    {
        var muroLSint = MurosRectangularesSismico.CalcularMuroLongitudinal(proyectoSint, presionesSint, sismoHidroSint, sismoSueloSint);
        var muroTSint = MurosRectangularesSismico.CalcularMuroTransversal(proyectoSint, presionesSint, sismoHidroSint, sismoSueloSint);
        var rInteriorEsperadoL = (geoSint.LLargoM - geoSint.EmEspesorMuroM) / geoSint.HLAlturaLiquidoM; // (6.25-0.25)/3.0 = 2.0 (eje a eje)
        var rInteriorEsperadoT = (geoSint.BAnchoM - geoSint.EmEspesorMuroM) / geoSint.HLAlturaLiquidoM; // (4.75-0.25)/3.0 = 1.5 (eje a eje)
        totalAserciones += 2;
        if (Math.Abs(muroLSint.Interior.R - rInteriorEsperadoL) < 1e-9) Console.WriteLine($"  [OK  ] Muro longitudinal, R interior = L/HL = {rInteriorEsperadoL:0.###}");
        else { fallos++; Console.WriteLine($"  [FAIL] Muro longitudinal, R interior esperado {rInteriorEsperadoL:0.###}, actual {muroLSint.Interior.R:0.###}"); }
        if (Math.Abs(muroTSint.Interior.R - rInteriorEsperadoT) < 1e-9) Console.WriteLine($"  [OK  ] Muro transversal, R interior = B/HL = {rInteriorEsperadoT:0.###}");
        else { fallos++; Console.WriteLine($"  [FAIL] Muro transversal, R interior esperado {rInteriorEsperadoT:0.###}, actual {muroTSint.Interior.R:0.###}"); }
        Console.WriteLine($"  [OK  ] CalcularMuroLongitudinal/Transversal no lanzan sobre geometria sintetica no cuadrada (L=6.0,B=4.5,HL=Hm=3.0) -- MxPosGob interior L={muroLSint.Interior.MxPosGobernanteKNmM:0.###} kNm/m, T={muroTSint.Interior.MxPosGobernanteKNmM:0.###} kNm/m");
    }
    catch (Exception ex) { fallos++; Console.WriteLine($"  [FAIL] CalcularMuroLongitudinal/Transversal lanzaron excepcion inesperada: {ex.Message}"); }

    Console.WriteLine();
    Console.WriteLine("=== Modulo 8 conectado a muro (DisenoMuros: flexion/cortante/fisuracion sobre F.7/F.7b) ===");
    Console.WriteLine("-- RecubrimientosNSR10.CalcularDEfectivo --");
    AssertTol("d = espesor - recubrimiento formado - Ø/2 (em=0.25, recub=0.05, Ø25mm)",
        RecubrimientosNSR10.CalcularDEfectivo(0.25, RecubrimientosNSR10.RecubrimientoFormadoM, 25.0), 0.1875, atol: 1e-9);
    totalAserciones++;
    try { RecubrimientosNSR10.CalcularDEfectivo(0.05, RecubrimientosNSR10.RecubrimientoFormadoM, 25.0); fallos++; Console.WriteLine("  [FAIL] espesor=0.05m (insuficiente) no lanzo"); }
    catch (ArgumentOutOfRangeException) { Console.WriteLine("  [OK  ] espesor=0.05m (insuficiente para recubrimiento+Ø/2) lanza ArgumentOutOfRangeException"); }

    // Reutiliza la geometria sintetica no cuadrada ya definida arriba (geoSint/matSint/proyectoSint/
    // presionesSint/sismoHidroSint/sismoSueloSint) -- misma geometria confirmada esta sesion que no
    // lanza en el dominio sismico del Capitulo 3 (b/a=2.0, c/a=1.5 en ambos muros).
    Console.WriteLine("-- Extremo a extremo con sismo disponible: DisenarMuroLongitudinal/Transversal --");
    var estaticoL = MurosRectangulares.CalcularMuroLongitudinal(proyectoSint, presionesSint);
    var sismicoL = MurosRectangularesSismico.CalcularMuroLongitudinal(proyectoSint, presionesSint, sismoHidroSint, sismoSueloSint);
    var disenoL = DisenoMuros.DisenarMuroLongitudinal(proyectoSint, presionesSint, sismoHidroSint, sismoSueloSint);

    totalAserciones++;
    if (disenoL.SismoIncluido && disenoL.MotivoSismoOmitido is null) Console.WriteLine("  [OK  ] Muro longitudinal: sismo incluido (geometria dentro del dominio del Capitulo 3), sin motivo de omision");
    else { fallos++; Console.WriteLine($"  [FAIL] Muro longitudinal: SismoIncluido={disenoL.SismoIncluido}, MotivoSismoOmitido={disenoL.MotivoSismoOmitido}"); }

    AssertTol("d expuesto en ResultadoDisenoMuro == RecubrimientosNSR10.CalcularDEfectivo(em)", disenoL.DEfectivoM,
        RecubrimientosNSR10.CalcularDEfectivo(geoSint.EmEspesorMuroM, RecubrimientosNSR10.RecubrimientoFormadoM, CatalogoBarras.DiametroPredeterminadoBarraMm), atol: 1e-9);

    // Envolvente: el Mu expuesto por cada direccion debe ser exactamente el maximo de las 4
    // condiciones (estatico interior/exterior, sismico interior/exterior) obtenidas de forma
    // independiente -- verificacion estructural de que DisenoMuros no "inventa" ni pierde ningun
    // candidato de la envolvente.
    // geoSint tiene Hm=3.0 (!=0), asi que Exterior nunca es null aqui -- ver CalcularExteriorOSinSuelo.
    double MaxDe4(Func<ResultadoMuroRectangular, double> sel) => Math.Max(Math.Max(sel(estaticoL.Interior), sel(estaticoL.Exterior!)), Math.Max(sel(sismicoL.Interior), sel(sismicoL.Exterior!)));
    AssertTol("Envolvente Mx+ (vertical positivo) = maximo de las 4 condiciones", disenoL.VerticalPositivo.MuKNm, MaxDe4(r => r.MxPosGobernanteKNmM), atol: 1e-9);
    AssertTol("Envolvente Mx- (vertical negativo) = maximo de las 4 condiciones", disenoL.VerticalNegativo.MuKNm, MaxDe4(r => r.MxNegGobernanteKNmM), atol: 1e-9);
    AssertTol("Envolvente My+ (horizontal positivo) = maximo de las 4 condiciones", disenoL.HorizontalPositivo.MuKNm, MaxDe4(r => r.MyPosGobernanteKNmM), atol: 1e-9);
    AssertTol("Envolvente My- (horizontal negativo) = maximo de las 4 condiciones", disenoL.HorizontalNegativo.MuKNm, MaxDe4(r => r.MyNegGobernanteKNmM), atol: 1e-9);
    AssertTol("Envolvente Vu fondo = maximo de las 4 condiciones", disenoL.CortanteFondo.VuKN, MaxDe4(r => r.VBottomKNm), atol: 1e-9);
    AssertTol("Envolvente Vu lateral maximo = maximo de las 4 condiciones", disenoL.CortanteLateralMaximo.VuKN, MaxDe4(r => r.VSideMaxKNm), atol: 1e-9);

    // Diseno a flexion (+ segunda pasada de fisuracion, fix del 2026-08-26 -- ver docstring de
    // DisenarFlexionConControlFisuracion) autoconsistente: re-derivar directamente sobre el Mu/Ms
    // expuestos debe reproducir exactamente el resultado ya empaquetado en Flexion/Fisuracion.
    var ecSint = 4700.0 * Math.Sqrt(matSint.FcMPa);
    var hMuroSint = disenoL.DEfectivoM + RecubrimientosNSR10.RecubrimientoFormadoM + CatalogoBarras.DiametroPredeterminadoBarraMm / 2000.0;
    var (flexionDirecta, fisuracionDirecta) = DisenoFlexionCortanteFisuracion.DisenarFlexionConControlFisuracion(
        disenoL.VerticalPositivo.MuKNm, disenoL.VerticalPositivo.MsKNm, disenoL.DEfectivoM, 1.0,
        matSint.FyMPa, matSint.FcMPa, 200000.0, ecSint, hMuroSint,
        diametroBarraMm: CatalogoBarras.DiametroPredeterminadoBarraMm,
        cuantiaMinima: DisenoFlexionCortanteFisuracion.CuantiaMinimaMuroTanque,
        espaciamientoMaximoM: CatalogoBarras.EspaciamientoMaximoMuroM);
    AssertTol("Flexion.Rho (Mx+) autoconsistente con DisenarFlexionConControlFisuracion(Mu,Ms,d,...) directo", disenoL.VerticalPositivo.Flexion.Rho, flexionDirecta.Rho, atol: 1e-9);

    totalAserciones++;
    // ms puede ser null (combinacion gobernante sismica, ver el bloque "Control de fisuracion..."
    // mas abajo) -- en ese caso ambos lados deben ser null, no solo "no null y coincidentes".
    var fisuracionAutoconsistente = disenoL.VerticalPositivo.Fisuracion is null && fisuracionDirecta is null
        ? true
        : disenoL.VerticalPositivo.Fisuracion is not null && fisuracionDirecta is not null
            && Math.Abs(disenoL.VerticalPositivo.Fisuracion.FsMPa - fisuracionDirecta.FsMPa) < 1e-9
            && disenoL.VerticalPositivo.Fisuracion.Cumple == fisuracionDirecta.Cumple;
    if (fisuracionAutoconsistente) Console.WriteLine("  [OK  ] Fisuracion (Mx+) autoconsistente con DisenarFlexionConControlFisuracion(Mu,Ms,d,...) directo");
    else { fallos++; Console.WriteLine("  [FAIL] Fisuracion (Mx+) NO autoconsistente con la recalculada directamente"); }

    Console.WriteLine("-- Segunda pasada de rediseno por fisuracion (fix 2026-08-26): caso sintetico donde el As de flexion pura NO cumple fisuracion --");
    totalAserciones++;
    {
        // Momento de resistencia ultima pequeno (As por flexion cae en CuantiaMinima) pero momento
        // de servicio grande (mismo orden que el mayorado, gamma≈1) -- fuerza fs a superar fs,adm
        // con la separacion "nominal" que produce el As minimo, replicando exactamente el escenario
        // reportado por el usuario (captura de pantalla, fs,adm≈21 MPa). d=0.1875m, h=0.25m,
        // Ø25mm -- misma geometria del muro sintetico ya usado en este bloque.
        var muPequeno = 15.0; // kN·m/m -> As por flexion en CuantiaMinima
        var msGrande = 15.0; // kN·m/m de servicio, igual al mayorado (gamma=1) -- exige mucho de fs
        var flexionMin = DisenoFlexionCortanteFisuracion.DisenarFlexion(muPequeno, disenoL.DEfectivoM, 1.0, matSint.FyMPa, matSint.FcMPa);
        var areaBarra25 = Math.PI / 4.0 * 25.0 * 25.0;
        var sNominalMin = areaBarra25 / flexionMin.AsRequeridoMm2;
        var fisuracionSinSegundaPasada = DisenoFlexionCortanteFisuracion.VerificarControlFisuracion(
            msGrande, flexionMin.AsRequeridoMm2, flexionMin.Rho, 200000.0, ecSint, disenoL.DEfectivoM, sNominalMin, hMuroSint);

        var (flexionConSegundaPasada, fisuracionConSegundaPasada) = DisenoFlexionCortanteFisuracion.DisenarFlexionConControlFisuracion(
            muPequeno, msGrande, disenoL.DEfectivoM, 1.0, matSint.FyMPa, matSint.FcMPa, 200000.0, ecSint, hMuroSint);

        if (!fisuracionSinSegundaPasada.Cumple
            && fisuracionConSegundaPasada is not null && fisuracionConSegundaPasada.Cumple
            && flexionConSegundaPasada.AsRequeridoMm2 > flexionMin.AsRequeridoMm2 + 1e-6
            && flexionConSegundaPasada.Rho <= DisenoFlexionCortanteFisuracion.CuantiaMaxima + 1e-9)
        {
            Console.WriteLine($"  [OK  ] Segunda pasada: As de flexion pura ({flexionMin.AsRequeridoMm2:0.#}mm²) NO cumple fisuracion " +
                $"(fs={fisuracionSinSegundaPasada.FsMPa:0.#} > fs,adm={fisuracionSinSegundaPasada.FsAdmisibleMPa:0.#}); " +
                $"As aumentado a {flexionConSegundaPasada.AsRequeridoMm2:0.#}mm² SI cumple " +
                $"(fs={fisuracionConSegundaPasada.FsMPa:0.#} <= fs,adm={fisuracionConSegundaPasada.FsAdmisibleMPa:0.#})");
        }
        else
        {
            fallos++;
            Console.WriteLine($"  [FAIL] Segunda pasada no se comporto como se esperaba: sinSegundaPasada.Cumple={fisuracionSinSegundaPasada.Cumple}, " +
                $"conSegundaPasada.Cumple={fisuracionConSegundaPasada?.Cumple}, AsMin={flexionMin.AsRequeridoMm2:0.#}, AsFinal={flexionConSegundaPasada.AsRequeridoMm2:0.#}");
        }
    }

    totalAserciones++;
    var todasLasCuantiasEnRango = new[] { disenoL.VerticalPositivo, disenoL.VerticalNegativo, disenoL.HorizontalPositivo, disenoL.HorizontalNegativo }
        .All(dd => dd.Flexion.Rho >= DisenoFlexionCortanteFisuracion.CuantiaMinima - 1e-9 && dd.Flexion.Rho <= DisenoFlexionCortanteFisuracion.CuantiaMaxima + 1e-9);
    if (todasLasCuantiasEnRango) Console.WriteLine("  [OK  ] Las 4 cuantias de diseno (Mx+/Mx-/My+/My-) caen en [CuantiaMinima, CuantiaMaxima]");
    else { fallos++; Console.WriteLine("  [FAIL] Alguna cuantia de diseno cae fuera de [CuantiaMinima, CuantiaMaxima]"); }

    Console.WriteLine("-- Refuerzo minimo de MURO de tanque (cruce normativo C.23-C.14.3, 2026-08-29): cuantia minima 0.0030, no la generica 0.0018 --");
    // Un muro de tanque con Mu pequeno debe saturarse en CuantiaMinimaMuroTanque (0.0030), no en
    // CuantiaMinima (0.0018). Se verifica con DisenarFlexion directo: mismo Mu/d/fy/f'c, distinta
    // cuantia minima -> distinto As (la cuantia minima de muro gobierna por encima de la generica).
    var muChico = 1.0; // kN·m/m, claramente por debajo de cualquier cuantia de flexión real
    var flexionLosa = DisenoFlexionCortanteFisuracion.DisenarFlexion(muChico, disenoL.DEfectivoM, 1.0, matSint.FyMPa, matSint.FcMPa);
    var flexionMuro = DisenoFlexionCortanteFisuracion.DisenarFlexion(muChico, disenoL.DEfectivoM, 1.0, matSint.FyMPa, matSint.FcMPa, cuantiaMinima: DisenoFlexionCortanteFisuracion.CuantiaMinimaMuroTanque);
    AssertTol("Con cuantia minima generica (0.0018), Mu pequeno satura en 0.0018", flexionLosa.Rho, DisenoFlexionCortanteFisuracion.CuantiaMinima, atol: 1e-9);
    AssertTol("Con cuantia minima de muro (0.0030), Mu pequeno satura en 0.0030", flexionMuro.Rho, DisenoFlexionCortanteFisuracion.CuantiaMinimaMuroTanque, atol: 1e-9);
    totalAserciones++;
    if (flexionMuro.AsRequeridoMm2 > flexionLosa.AsRequeridoMm2 + 1e-6)
        Console.WriteLine($"  [OK  ] As de muro ({flexionMuro.AsRequeridoMm2:0.#}mm²/m) > As de losa ({flexionLosa.AsRequeridoMm2:0.#}mm²/m) para el mismo Mu -- el minimo de muro 0.0030 gobierna sobre el generico 0.0018");
    else { fallos++; Console.WriteLine($"  [FAIL] As de muro ({flexionMuro.AsRequeridoMm2}) no supera As de losa ({flexionLosa.AsRequeridoMm2}) como se esperaba"); }
    totalAserciones++;
    try { DisenoFlexionCortanteFisuracion.DisenarFlexion(muChico, disenoL.DEfectivoM, 1.0, matSint.FyMPa, matSint.FcMPa, cuantiaMinima: 0.02); fallos++; Console.WriteLine("  [FAIL] cuantiaMinima > CuantiaMaxima no lanzo"); }
    catch (ArgumentOutOfRangeException) { Console.WriteLine("  [OK  ] cuantiaMinima > CuantiaMaxima lanza ArgumentOutOfRangeException"); }

    Console.WriteLine("-- Control de fisuracion: presente solo cuando la condicion gobernante es ESTATICA --");
    totalAserciones++;
    var direcciones = new[] { ("Mx+", disenoL.VerticalPositivo), ("Mx-", disenoL.VerticalNegativo), ("My+", disenoL.HorizontalPositivo), ("My-", disenoL.HorizontalNegativo) };
    var consistente = direcciones.All(t => (t.Item2.ComboGobernante.StartsWith("Estático") && t.Item2.MsKNm is not null && t.Item2.Fisuracion is not null && t.Item2.Servicio is not null)
                                         || (t.Item2.ComboGobernante.StartsWith("Sísmico") && t.Item2.MsKNm is null && t.Item2.Fisuracion is null && t.Item2.Servicio is null));
    if (consistente) Console.WriteLine("  [OK  ] Ms/Fisuracion/Servicio no-null solo bajo combinacion ESTATICA gobernante, null bajo SISMICA, en las 4 direcciones");
    else
    {
        fallos++;
        foreach (var (nombre, dd) in direcciones)
            Console.WriteLine($"  [FAIL detalle] {nombre}: combo={dd.ComboGobernante}, Ms={dd.MsKNm}, Fisuracion={(dd.Fisuracion is null ? "null" : "no-null")}");
    }

    Console.WriteLine("-- Muro transversal, mismo patron --");
    var disenoT = DisenoMuros.DisenarMuroTransversal(proyectoSint, presionesSint, sismoHidroSint, sismoSueloSint);
    totalAserciones++;
    if (disenoT.SismoIncluido && disenoT.MotivoSismoOmitido is null) Console.WriteLine("  [OK  ] Muro transversal: sismo incluido, sin motivo de omision");
    else { fallos++; Console.WriteLine($"  [FAIL] Muro transversal: SismoIncluido={disenoT.SismoIncluido}, MotivoSismoOmitido={disenoT.MotivoSismoOmitido}"); }

    Console.WriteLine("-- Sismo NO provisto: DisenarMuroLongitudinal con sismoHidrodinamico/sismoSuelo=null --");
    var disenoSinSismo = DisenoMuros.DisenarMuroLongitudinal(proyectoSint, presionesSint, null, null);
    totalAserciones++;
    if (!disenoSinSismo.SismoIncluido && disenoSinSismo.MotivoSismoOmitido is not null && disenoSinSismo.MotivoSismoOmitido.Contains("No se proveyeron"))
        Console.WriteLine("  [OK  ] Sin F.5/F.6 provistos: SismoIncluido=false, motivo explicito, diseno completa sin lanzar (envolvente solo estatica)");
    else { fallos++; Console.WriteLine($"  [FAIL] SismoIncluido={disenoSinSismo.SismoIncluido}, MotivoSismoOmitido={disenoSinSismo.MotivoSismoOmitido}"); }

    AssertTol("Sin sismo: envolvente Mx+ = maximo SOLO de las 2 condiciones estaticas", disenoSinSismo.VerticalPositivo.MuKNm,
        Math.Max(estaticoL.Interior.MxPosGobernanteKNmM, estaticoL.Exterior!.MxPosGobernanteKNmM), atol: 1e-9);

    Console.WriteLine("-- CORRECCION 2026-08-28 (auditoria externa, \"revision sismo fuera de dominio PCA Caso 7\"): --");
    Console.WriteLine("-- geometria fuera del dominio sismico del Capitulo 3 (b/a<1.0) pero valida para el Capitulo 2 (estatico) --");
    Console.WriteLine("-- YA NO se omite el sismo -- se calcula una cota conservadora de una via (cantiliever+franja) --");
    // Em (espesor de muro) deliberadamente generoso (0.6m): la cota conservadora de una via puede
    // exigir bastante mas acero que la solucion de placa en dos direcciones para la misma carga
    // (es, a proposito, una sobreestimacion segura) -- un muro delgado tipico (0.25m) puede resultar
    // insuficiente frente a esa demanda conservadora, lo que en si mismo seria un resultado
    // correcto (EspesorInsuficienteException, ya verificado en otro lugar), pero no es lo que este
    // bloque quiere ejercitar (la mecanica de la aproximacion, no el chequeo de espesor).
    var geoFueraDominio = new Geometria(BAnchoM: 3.0, LLargoM: 3.5, HtAlturaM: 4.5, ConTapa: false, EmEspesorMuroM: 0.6, EfEspesorFondoM: 0.20, EtEspesorTapaM: 0.0, HLAlturaLiquidoM: 4.0, HmAlturaSueloSobreMuroM: 4.0, WextSobrecargaKNm2: 0.0);
    var proyectoFueraDominio = new ProyectoTanque(geoFueraDominio, matSint);
    var presionesFueraDominio = PresionesLaterales.Calcular(proyectoFueraDominio);
    var sismoHidroFueraDominio = FuerzaSismicaHidrodinamica.Calcular(proyectoFueraDominio, espectroSint);
    var sismoSueloFueraDominio = FuerzaDinamicaSuelo.Calcular(proyectoFueraDominio, sueloSint);

    // b/a=(L-em)/HL=2.9/4.0=0.725<1.0 y c/a=(B-em)/HL=2.4/4.0=0.6 (Hm=HL en esta geometria) --
    // interior y exterior caen fuera del dominio tabulado del Capitulo 3 para el muro
    // longitudinal (span propio L-em=2.9); c/a=0.6 entra en la banda 1.0<=b/a<2.0, donde el
    // refinamiento 2026-09-02 acota con la fila tabulada b/a=3.0.
    totalAserciones++;
    try { MurosRectangularesSismico.Calcular(geoFueraDominio.LLargoM / geoFueraDominio.HLAlturaLiquidoM, geoFueraDominio.BAnchoM / geoFueraDominio.HLAlturaLiquidoM, 10, 1); fallos++; Console.WriteLine("  [FAIL] Calcular() de bajo nivel no lanzo para esta geometria (deberia seguir lanzando -- solo el nivel superior degrada)"); }
    catch (ArgumentOutOfRangeException) { Console.WriteLine("  [OK  ] Calcular() de bajo nivel SIGUE lanzando ArgumentOutOfRangeException para b/a<1.0 -- la tabla del Capitulo 3 no cambio, solo el nivel superior ahora degrada"); }

    var sismicoFueraDominio = MurosRectangularesSismico.CalcularMuroLongitudinal(proyectoFueraDominio, presionesFueraDominio, sismoHidroFueraDominio, sismoSueloFueraDominio);
    totalAserciones++;
    if (sismicoFueraDominio.Interior.EsAproximacionConservadora && sismicoFueraDominio.MotivoAproximacionInterior is not null && sismicoFueraDominio.MotivoAproximacionInterior.Contains("fuera del dominio tabulado"))
        Console.WriteLine("  [OK  ] Interior: EsAproximacionConservadora=true, MotivoAproximacionInterior documenta la causa");
    else { fallos++; Console.WriteLine($"  [FAIL] Interior: EsAproximacionConservadora={sismicoFueraDominio.Interior.EsAproximacionConservadora}, Motivo={sismicoFueraDominio.MotivoAproximacionInterior}"); }

    totalAserciones++;
    if (sismicoFueraDominio.Exterior is not null && sismicoFueraDominio.Exterior.EsAproximacionConservadora && sismicoFueraDominio.MotivoAproximacionExterior is not null)
        Console.WriteLine("  [OK  ] Exterior: EsAproximacionConservadora=true, MotivoAproximacionExterior documenta la causa (Hm=HL en esta geometria, tambien fuera de dominio)");
    else { fallos++; Console.WriteLine($"  [FAIL] Exterior: {(sismicoFueraDominio.Exterior is null ? "null" : $"EsAproximacionConservadora={sismicoFueraDominio.Exterior.EsAproximacionConservadora}")}"); }

    // Verificacion INDEPENDIENTE de las formulas cerradas -- estatica elemental, no reutiliza
    // CalcularConservador (recalculada aqui a mano a partir de la definicion de cada formula).
    var qInteriorFD = MurosRectangularesSismico.CalcularCargaSismicaInterior(
        presionesFueraDominio.PhMaximaKNm2,
        sismoHidroFueraDominio.MuroLongitudinal.PresionImpulsiva.FondoKNm2,
        sismoHidroFueraDominio.MuroLongitudinal.PresionConvectiva.FondoKNm2);
    var aFD = geoFueraDominio.HLAlturaLiquidoM;
    var luzEjeAejeFD = geoFueraDominio.LLargoM - geoFueraDominio.EmEspesorMuroM; // span propio EJE A EJE (corrección 2026-08-31)
    // REFINAMIENTO 2026-09-02 (banda 1.0<=b/a<2.0): c/a=(B-em)/HL=0.6 cabe en la fila b/a=3.0 y la
    // monotonicidad verificada garantiza que la cota tabulada (3.0,c/a) es cota superior válida --
    // se usa el MÍNIMO con la franja/cantiléver (nunca se afloja respecto del comportamiento previo).
    var cSobreAFD = (geoFueraDominio.BAnchoM - geoFueraDominio.EmEspesorMuroM) / aFD;
    var tabFD = MurosRectangularesSismico.Calcular(3.0, cSobreAFD, qInteriorFD, aFD).LadoLargo;
    var mxTabFD = Math.Max(Math.Abs(tabFD.MxPosGobernanteKNmM), Math.Abs(tabFD.MxNegGobernanteKNmM));
    var myTabFD = Math.Max(Math.Abs(tabFD.MyPosGobernanteKNmM), Math.Abs(tabFD.MyNegGobernanteKNmM));
    var mxBaseEsperado = Math.Min(qInteriorFD * aFD * aFD / 2.0, mxTabFD); // cantiliever UNIFORME q*a^2/2 (hallazgo 2026-09-02), acotado por la fila b/a=3.0
    var myFranjaEsperado = Math.Min(qInteriorFD * luzEjeAejeFD * luzEjeAejeFD / 8.0, myTabFD); // franja q*(L-em)^2/8, acotada
    var vBaseEsperado = qInteriorFD * aFD / 2.0; // q*a/2 (convención triangular del Cs, consistente con el diseño en dominio)
    var vLadoEsperado = Math.Min(qInteriorFD * luzEjeAejeFD / 2.0, tabFD.VSideMaxKNm); // franja q*(L-em)/2, acotada
    AssertTol("Interior conservador: Mx+ = Mx- = min(q*a^2/2, |Mx| tabulado (3.0,c/a)) (cantiliever UNIFORME, recalculado independiente)", sismicoFueraDominio.Interior.MxPosGobernanteKNmM, mxBaseEsperado, atol: 1e-6);
    AssertTol("Interior conservador: Mx- = min(q*a^2/2, |Mx| tabulado (3.0,c/a))", sismicoFueraDominio.Interior.MxNegGobernanteKNmM, mxBaseEsperado, atol: 1e-6);
    AssertTol("Interior conservador: My+ = My- = min(q*L^2/8, |My| tabulado (3.0,c/a)) (franja, recalculado independiente)", sismicoFueraDominio.Interior.MyPosGobernanteKNmM, myFranjaEsperado, atol: 1e-6);
    AssertTol("Interior conservador: My- = min(q*L^2/8, |My| tabulado (3.0,c/a))", sismicoFueraDominio.Interior.MyNegGobernanteKNmM, myFranjaEsperado, atol: 1e-6);
    AssertTol("Interior conservador: V fondo = q*a/2 (recalculado independiente)", sismicoFueraDominio.Interior.VBottomKNm, vBaseEsperado, atol: 1e-6);
    AssertTol("Interior conservador: V lateral max = V lateral medio = min(q*L/2, V tabulado (3.0,c/a)) (recalculado independiente)", sismicoFueraDominio.Interior.VSideMaxKNm, vLadoEsperado, atol: 1e-6);
    AssertTol("Interior conservador: V lateral medio = min(q*L/2, V tabulado (3.0,c/a))", sismicoFueraDominio.Interior.VSideMidKNm, vLadoEsperado, atol: 1e-6);

    // Consistencia con el motor tabulado en el limite del dominio: en b/a=1.0 (el valor mas bajo
    // tabulado) la cota conservadora de una via, evaluada con la MISMA carga q y la misma altura
    // a=b (b/a=1.0), debe ser >= el momento gobernante real de la placa en dos direcciones --
    // porque es una cota superior de una via sobre una solucion de placa que redistribuye momento
    // en dos direcciones (nunca menor).
    totalAserciones++;
    {
        const double qLimite = 50.0;
        const double aLimite = 4.0; // b/a=1.0 -> span=altura=4.0
        var tabuladoLimite = MurosRectangularesSismico.Calcular(1.0, 0.5, qLimite, aLimite).LadoLargo;
        var mxConservadorLimite = qLimite * aLimite * aLimite / 2.0; // q*a^2/2 (cantiléver uniforme, hallazgo 2026-09-02)
        if (mxConservadorLimite >= tabuladoLimite.MxPosGobernanteKNmM - 1e-9 && mxConservadorLimite >= tabuladoLimite.MxNegGobernanteKNmM - 1e-9)
            Console.WriteLine($"  [OK  ] Cota conservadora Mx en b/a=1.0 ({mxConservadorLimite:0.###}) >= momento gobernante de la tabla del Capitulo 3 (Mx+={tabuladoLimite.MxPosGobernanteKNmM:0.###}, |Mx-|={tabuladoLimite.MxNegGobernanteKNmM:0.###}) -- consistente con ser una cota superior de una via");
        else { fallos++; Console.WriteLine($"  [FAIL] Cota conservadora ({mxConservadorLimite:0.###}) es MENOR que el valor tabulado -- ya no seria conservadora"); }
    }

    // HALLAZGO 2026-09-02 (carga del Caso 7 UNIFORME): la cota anterior q*a^2/6 (cantiléver
    // triangular) NO cubría el borde LARGO del dominio -- en b/a=4.0 la tabla da |Mx| gobernante
    // ≈ 0.43·q·a² > 0.167·q·a², físicamente imposible para carga triangular (la acción de placa
    // solo reduce el momento respecto del cantiléver de una vía). Con la cota corregida q*a^2/2
    // (cantiléver UNIFORME, cota rigurosa para cualquier w(y)≤q) se verifica la cobertura aquí.
    totalAserciones++;
    {
        const double qLimite4 = 50.0;
        const double aLimite4 = 4.0;
        var tabuladoB4 = MurosRectangularesSismico.Calcular(4.0, 0.5, qLimite4, aLimite4).LadoLargo;
        var mxConservadorB4 = qLimite4 * aLimite4 * aLimite4 / 2.0; // q*a^2/2 (corregida)
        var mxAntiguoB4 = qLimite4 * aLimite4 * aLimite4 / 6.0; // q*a^2/6 (anterior, NO conservadora)
        // MxNegGobernanteKNmM es MAGNITUD positiva (convención del reporte "Mx- = ..."); no negar.
        var tabuladoMxB4 = Math.Max(tabuladoB4.MxPosGobernanteKNmM, tabuladoB4.MxNegGobernanteKNmM);
        if (mxConservadorB4 >= tabuladoMxB4 - 1e-9 && mxAntiguoB4 < tabuladoMxB4 - 1e-9)
            Console.WriteLine($"  [OK  ] b/a=4.0: cota corregida q*a^2/2 ({mxConservadorB4:0.###}) >= tabulado |Mx| ({tabuladoMxB4:0.###}), y cota ANTIGUA q*a^2/6 ({mxAntiguoB4:0.###}) < tabulado -- la tabla es de carga UNIFORME y la cota anterior era no conservadora en el borde largo");
        else { fallos++; Console.WriteLine($"  [FAIL] b/a=4.0: cota {mxConservadorB4:0.###} vs tabulado {tabuladoMxB4:0.###} (antigua {mxAntiguoB4:0.###})"); }
    }

    Console.WriteLine("-- CORRECCION 2026-08-28 (auditoria externa tercera ronda, hallazgo N3/R3 \"cota conservadora sin regimen\"): --");
    Console.WriteLine("-- geometria b/a=2.96 (largo/bajo, eje a eje), c/a=2.16 (hueco de grilla, ni en la fila b/a=3.0 ni exacta) --");
    Console.WriteLine("-- My YA NO usa la franja horizontal sin acotar -- se acota con el valor REAL tabulado en b/a=4.0 --");
    var geoRegimenLargo = new Geometria(BAnchoM: 6.0, LLargoM: 8.0, HtAlturaM: 4.5, ConTapa: false, EmEspesorMuroM: 0.6, EfEspesorFondoM: 0.20, EtEspesorTapaM: 0.0, HLAlturaLiquidoM: 4.0, HmAlturaSueloSobreMuroM: 2.5, WextSobrecargaKNm2: 0.0);
    var proyectoRegimenLargo = new ProyectoTanque(geoRegimenLargo, matSint);
    var presionesRegimenLargo = PresionesLaterales.Calcular(proyectoRegimenLargo);
    var sismoHidroRegimenLargo = FuerzaSismicaHidrodinamica.Calcular(proyectoRegimenLargo, espectroSint);
    var sismoSueloRegimenLargo = FuerzaDinamicaSuelo.Calcular(proyectoRegimenLargo, sueloSint);
    var sismicoRegimenLargo = MurosRectangularesSismico.CalcularMuroLongitudinal(proyectoRegimenLargo, presionesRegimenLargo, sismoHidroRegimenLargo, sismoSueloRegimenLargo);

    totalAserciones++;
    if (sismicoRegimenLargo.Exterior is not null && sismicoRegimenLargo.Exterior.EsAproximacionConservadora
        && sismicoRegimenLargo.MotivoAproximacionExterior is not null
        && sismicoRegimenLargo.MotivoAproximacionExterior.Contains("b/a=2.96") && sismicoRegimenLargo.MotivoAproximacionExterior.Contains("c/a=2.16")
        && !sismicoRegimenLargo.MotivoAproximacionExterior.Contains("docstring"))
        Console.WriteLine("  [OK  ] Exterior (b/a=2.96,c/a=2.16, eje a eje): EsAproximacionConservadora=true, motivo documenta b/a y c/a, SIN remitir al codigo fuente (hallazgo N1 corregido)");
    else { fallos++; Console.WriteLine($"  [FAIL] Exterior: {(sismicoRegimenLargo.Exterior is null ? "null" : $"EsAproximacionConservadora={sismicoRegimenLargo.Exterior.EsAproximacionConservadora}")}, Motivo={sismicoRegimenLargo.MotivoAproximacionExterior}"); }

    // Verificacion INDEPENDIENTE del recorte de My (hallazgo N3/R3): recalculo en C#, a partir de
    // los MISMOS valores tabulados publicados del manual PCA (Capitulo 3, Caso 7, fila b/a=4.0,
    // columnas c/a=3.0 y c/a=2.0 -- copiados aqui literalmente de la fuente normativa, NO leidos de
    // Tanque.Core.Modulos.MurosCapitulo3Caso7Coeficientes ni de TryAcotarMyConTablaEnBSobreA4), con
    // interpolacion bilineal y combinacion Marcus reimplementadas desde cero -- misma tecnica de
    // "segunda derivacion cerrada que no reutiliza el propio codigo" ya usada para el hallazgo de
    // flexion (H-CRITICO-1) y para las formulas cerradas de la aproximacion conservadora (arriba).
    var qExteriorRegimenLargo = MurosRectangularesSismico.CalcularCargaSismicaExterior(presionesRegimenLargo.Ps2MaximaKNm2, sismoSueloRegimenLargo.QaeKNm2);
    var aRegimenLargo = geoRegimenLargo.HmAlturaSueloSobreMuroM; // "a" para la condicion exterior
    double[][] largoMyB4 =
    [
        [-338,-41,51,64,58,54], [-373,-38,47,58,51,48], [-303,-34,41,50,43,40], [-247,-29,35,40,33,30],
        [-197,-24,27,28,21,18], [-150,-20,17,15,7,4], [-105,-17,5,0,-9,-12], [-64,-14,-7,-17,-26,-29],
        [-28,-14,-22,-35,-44,-47], [-5,-16,-37,-54,-63,-66], [0,-22,-53,-73,-84,-87],
    ]; // c/a=3.0
    double[][] largoMyB4C2 =
    [
        [-281,-28,53,63,56,52], [-323,-26,49,57,50,46], [-267,-23,43,49,42,38], [-221,-20,36,39,32,28],
        [-180,-17,28,27,20,16], [-140,-14,17,14,6,3], [-100,-13,5,-1,-10,-13], [-63,-12,-8,-18,-27,-30],
        [-29,-14,-23,-36,-45,-48], [-5,-18,-39,-55,-64,-67], [0,-24,-55,-75,-84,-87],
    ]; // c/a=2.0
    double[][] largoMxyB4 =
    [
        [4,83,74,50,24,0], [10,79,74,50,24,0], [9,79,74,50,24,0], [8,79,73,50,24,0],
        [8,79,72,48,23,0], [7,76,69,45,21,0], [6,71,64,41,19,0], [5,62,55,34,15,0],
        [4,49,42,25,11,0], [2,29,24,14,6,0], [0,0,0,0,0,0],
    ]; // c/a=3.0
    double[][] largoMxyB4C2 =
    [
        [15,84,73,49,23,0], [25,81,73,49,24,0], [24,80,73,49,23,0], [23,80,72,48,23,0],
        [21,80,71,47,22,0], [19,77,68,44,20,0], [17,72,63,39,18,0], [14,63,54,33,15,0],
        [10,50,41,24,11,0], [5,30,23,13,6,0], [0,0,0,0,0,0],
    ]; // c/a=2.0
    var cSobreARegimenLargo = (geoRegimenLargo.BAnchoM - geoRegimenLargo.EmEspesorMuroM) / aRegimenLargo; // (6.0-0.6)/2.5 = 2.16 (eje a eje)
    var tInterp = (cSobreARegimenLargo - 2.0) / (3.0 - 2.0);
    var escalaRegimenLargo = qExteriorRegimenLargo * aRegimenLargo * aRegimenLargo / 1000.0;
    var myPosGobEsperado = 0.0;
    var myNegGobEsperado = 0.0;
    for (var fila = 0; fila < 11; fila++)
    {
        for (var col = 0; col < 6; col++)
        {
            var myInterp = largoMyB4C2[fila][col] + tInterp * (largoMyB4[fila][col] - largoMyB4C2[fila][col]);
            var mxyInterp = largoMxyB4C2[fila][col] + tInterp * (largoMxyB4[fila][col] - largoMxyB4C2[fila][col]);
            var myPos = Math.Max(0, myInterp + mxyInterp) * escalaRegimenLargo;
            var myNeg = Math.Min(0, myInterp - mxyInterp) * escalaRegimenLargo;
            myPosGobEsperado = Math.Max(myPosGobEsperado, myPos);
            myNegGobEsperado = Math.Max(myNegGobEsperado, -myNeg);
        }
    }
    var myAcotadoEsperado = Math.Max(myPosGobEsperado, myNegGobEsperado);
    var mxBaseEsperadoRL = qExteriorRegimenLargo * aRegimenLargo * aRegimenLargo / 2.0;
    var myFranjaSinAcotarRL = qExteriorRegimenLargo * (geoRegimenLargo.LLargoM - geoRegimenLargo.EmEspesorMuroM) * (geoRegimenLargo.LLargoM - geoRegimenLargo.EmEspesorMuroM) / 8.0; // valor ANTERIOR (N3) con luz eje a eje, ya NO debe aparecer

    AssertTol("Exterior conservador (b/a=2.96): Mx+ = Mx- = q*a^2/2 (cantiliever UNIFORME)", sismicoRegimenLargo.Exterior!.MxPosGobernanteKNmM, mxBaseEsperadoRL, atol: 1e-6);
    AssertTol("Exterior conservador (b/a=2.96): My acotado con la tabla real en b/a=4.0,c/a=2.16 (recalculo independiente C#)", sismicoRegimenLargo.Exterior.MyPosGobernanteKNmM, myAcotadoEsperado, atol: 1e-6);
    AssertTol("Exterior conservador (b/a=2.96): My- = misma cota (MyNegGobernanteKNmM)", sismicoRegimenLargo.Exterior.MyNegGobernanteKNmM, myAcotadoEsperado, atol: 1e-6);
    totalAserciones++;
    if (sismicoRegimenLargo.Exterior.MyPosGobernanteKNmM < myFranjaSinAcotarRL - 1e-6)
        Console.WriteLine($"  [OK  ] My acotado ({sismicoRegimenLargo.Exterior.MyPosGobernanteKNmM:0.###}) < franja sin acotar del comportamiento ANTERIOR a N3 ({myFranjaSinAcotarRL:0.###}) -- el sobredimensionamiento reportado por la auditoria ya no ocurre");
    else { fallos++; Console.WriteLine($"  [FAIL] My acotado ({sismicoRegimenLargo.Exterior.MyPosGobernanteKNmM:0.###}) NO es menor que la franja sin acotar ({myFranjaSinAcotarRL:0.###})"); }

    // Verificacion INDEPENDIENTE del recorte de V (cortante lateral), hallazgo menor de la cuarta
    // ronda de auditoria (2026-08-28): el cortante lateral de la cota conservadora usaba la franja
    // completa q*L/2 SIN acotar en el regimen largo/bajo, igual que hacia My antes de N3. Ahora V
    // se acota, de forma analoga, al valor REAL tabulado del Capitulo 3 en b/a=4.0 con el mismo c/a
    // (TryAcotarVCortanteConTablaEnBSobreA4). Para el LadoLargo en b/a=4.0 el coeficiente Cs
    // "side edge -- maximum" publicado por el manual PCA es 0.38 (primer valor tabulado, r=4.0),
    // copiado aqui literalmente de la fuente normativa, NO leido del propio codigo.
    var vAcotadoEsperadoRL = 0.38 * qExteriorRegimenLargo * aRegimenLargo;
    var vFranjaSinAcotarRL = qExteriorRegimenLargo * (geoRegimenLargo.LLargoM - geoRegimenLargo.EmEspesorMuroM) / 2.0; // valor ANTERIOR (q*(L-em)/2), ya NO debe aparecer
    AssertTol("Exterior conservador (b/a=2.96): V lateral acotado al valor REAL tabulado en b/a=4.0 (Cs=0.38, lado largo)", sismicoRegimenLargo.Exterior!.VSideMaxKNm, vAcotadoEsperadoRL, atol: 1e-6);
    AssertTol("Exterior conservador (b/a=2.96): V lateral medio = misma cota (VSideMidKNm)", sismicoRegimenLargo.Exterior.VSideMidKNm, vAcotadoEsperadoRL, atol: 1e-6);
    totalAserciones++;
    if (sismicoRegimenLargo.Exterior.VSideMaxKNm < vFranjaSinAcotarRL - 1e-6)
        Console.WriteLine($"  [OK  ] V lateral acotado ({sismicoRegimenLargo.Exterior.VSideMaxKNm:0.###}) < franja sin acotar del comportamiento ANTERIOR ({vFranjaSinAcotarRL:0.###}) -- el sobredimensionamiento remanente del cortante lateral ya no ocurre");
    else { fallos++; Console.WriteLine($"  [FAIL] V lateral acotado ({sismicoRegimenLargo.Exterior.VSideMaxKNm:0.###}) NO es menor que la franja sin acotar ({vFranjaSinAcotarRL:0.###})"); }

    Console.WriteLine("-- geometria totalmente fuera de dominio incluso en el extremo b/a=4.0 (c/a=3.5>3.0) -- ultimo recurso My=Mx --");
    var geoRegimenLargoExtremo = new Geometria(BAnchoM: 8.75, LLargoM: 11.25, HtAlturaM: 3.0, ConTapa: false, EmEspesorMuroM: 0.6, EfEspesorFondoM: 0.20, EtEspesorTapaM: 0.0, HLAlturaLiquidoM: 2.0, HmAlturaSueloSobreMuroM: 2.5, WextSobrecargaKNm2: 0.0);
    var proyectoRegimenLargoExtremo = new ProyectoTanque(geoRegimenLargoExtremo, matSint);
    var presionesRegimenLargoExtremo = PresionesLaterales.Calcular(proyectoRegimenLargoExtremo);
    var sismoHidroRegimenLargoExtremo = FuerzaSismicaHidrodinamica.Calcular(proyectoRegimenLargoExtremo, espectroSint);
    var sismoSueloRegimenLargoExtremo = FuerzaDinamicaSuelo.Calcular(proyectoRegimenLargoExtremo, sueloSint);
    var sismicoRegimenLargoExtremo = MurosRectangularesSismico.CalcularMuroLongitudinal(proyectoRegimenLargoExtremo, presionesRegimenLargoExtremo, sismoHidroRegimenLargoExtremo, sismoSueloRegimenLargoExtremo);
    // b/a = (11.25-0.6)/2.5 = 4.26 (fuera del dominio incluso en b) ; c/a = (8.75-0.6)/2.5 = 3.26 (>3.0, tampoco
    // cabe en la fila b/a=4.0) -- TryAcotarMyConTablaEnBSobreA4 debe devolver null y el ultimo
    // recurso cerrado (My=Mx) debe aplicarse.
    var qExteriorExtremo = MurosRectangularesSismico.CalcularCargaSismicaExterior(presionesRegimenLargoExtremo.Ps2MaximaKNm2, sismoSueloRegimenLargoExtremo.QaeKNm2);
    var mxEsperadoExtremo = qExteriorExtremo * geoRegimenLargoExtremo.HmAlturaSueloSobreMuroM * geoRegimenLargoExtremo.HmAlturaSueloSobreMuroM / 2.0;
    AssertTol("Exterior conservador, c/a fuera incluso del extremo b/a=4.0: My = Mx (ultimo recurso cerrado)", sismicoRegimenLargoExtremo.Exterior!.MyPosGobernanteKNmM, mxEsperadoExtremo, atol: 1e-6);
    var vBaseEsperadoExtremo = qExteriorExtremo * geoRegimenLargoExtremo.HmAlturaSueloSobreMuroM / 2.0; // q*a/2
    AssertTol("Exterior conservador, c/a fuera incluso del extremo b/a=4.0: V lateral = vBase (ultimo recurso cerrado)", sismicoRegimenLargoExtremo.Exterior.VSideMaxKNm, vBaseEsperadoExtremo, atol: 1e-6);

    Console.WriteLine("-- banda intermedia 1.0<=b/a<2.0 (REFINAMIENTO 2026-09-02: cota tabulada en b/a=3.0, mínimo con la franja) --");
    // b/a=(6.75-0.6)/4.5=1.367 (eje a eje, antes 1.5), c/a=(5.4-0.6)/4.5=1.067 fuera del hueco de
    // grilla de esa fila (b/a<2.0 solo tabula c/a hasta 1.0) -- fuerza la degradacion sin salirse del
    // rango nominal [1.0,4.0] de b/a. La fila b/a=3.0 del Capítulo 3 cubre c/a=1.067 (interpola
    // entre 1.0 y 1.5), y la monotonicidad verificada (|Mx| y |My| crecen con b/a y con c/a)
    // garantiza que la cota tabulada es una cota superior válida.
    var geoBandaIntermedia = new Geometria(BAnchoM: 5.4, LLargoM: 6.75, HtAlturaM: 5.5, ConTapa: false, EmEspesorMuroM: 0.6, EfEspesorFondoM: 0.20, EtEspesorTapaM: 0.0, HLAlturaLiquidoM: 3.0, HmAlturaSueloSobreMuroM: 4.5, WextSobrecargaKNm2: 0.0);
    var proyectoBandaIntermedia = new ProyectoTanque(geoBandaIntermedia, matSint);
    var presionesBandaIntermedia = PresionesLaterales.Calcular(proyectoBandaIntermedia);
    var sismoHidroBandaIntermedia = FuerzaSismicaHidrodinamica.Calcular(proyectoBandaIntermedia, espectroSint);
    var sismoSueloBandaIntermedia = FuerzaDinamicaSuelo.Calcular(proyectoBandaIntermedia, sueloSint);
    var sismicoBandaIntermedia = MurosRectangularesSismico.CalcularMuroLongitudinal(proyectoBandaIntermedia, presionesBandaIntermedia, sismoHidroBandaIntermedia, sismoSueloBandaIntermedia);
    var qExteriorBandaIntermedia = MurosRectangularesSismico.CalcularCargaSismicaExterior(presionesBandaIntermedia.Ps2MaximaKNm2, sismoSueloBandaIntermedia.QaeKNm2);
    var luzEjeAejeBanda = geoBandaIntermedia.LLargoM - geoBandaIntermedia.EmEspesorMuroM;
    var cSobreABanda = (geoBandaIntermedia.BAnchoM - geoBandaIntermedia.EmEspesorMuroM) / geoBandaIntermedia.HmAlturaSueloSobreMuroM;
    var tabBanda = MurosRectangularesSismico.Calcular(3.0, cSobreABanda, qExteriorBandaIntermedia, geoBandaIntermedia.HmAlturaSueloSobreMuroM).LadoLargo;
    var myTabBanda = Math.Max(Math.Abs(tabBanda.MyPosGobernanteKNmM), Math.Abs(tabBanda.MyNegGobernanteKNmM));
    var mxTabBanda = Math.Max(Math.Abs(tabBanda.MxPosGobernanteKNmM), Math.Abs(tabBanda.MxNegGobernanteKNmM));
    var mxCantilieverBanda = qExteriorBandaIntermedia * geoBandaIntermedia.HmAlturaSueloSobreMuroM * geoBandaIntermedia.HmAlturaSueloSobreMuroM / 2.0;
    var myEsperadaBanda = Math.Min(qExteriorBandaIntermedia * luzEjeAejeBanda * luzEjeAejeBanda / 8.0, myTabBanda);
    var vTabBanda = tabBanda.VSideMaxKNm;
    var vEsperadaBanda = Math.Min(qExteriorBandaIntermedia * luzEjeAejeBanda / 2.0, vTabBanda);
    totalAserciones++;
    if (sismicoBandaIntermedia.Exterior is not null && sismicoBandaIntermedia.Exterior.EsAproximacionConservadora)
    {
        AssertTol("Banda 1.0<=b/a<2.0: Mx = min(q*a^2/2, |Mx| tabulado (3.0,c/a)) [refinamiento 2026-09-02]", sismicoBandaIntermedia.Exterior.MxPosGobernanteKNmM, Math.Min(mxCantilieverBanda, mxTabBanda), atol: 1e-6);
        AssertTol("Banda 1.0<=b/a<2.0: My = min(q*L^2/8, |My| tabulado (3.0,c/a)) [refinamiento 2026-09-02]", sismicoBandaIntermedia.Exterior.MyPosGobernanteKNmM, myEsperadaBanda, atol: 1e-6);
    }
    else { fallos++; Console.WriteLine($"  [FAIL] Se esperaba aproximacion conservadora en banda intermedia: Exterior={(sismicoBandaIntermedia.Exterior is null ? "null" : "no conservadora")}"); }
    AssertTol("Banda 1.0<=b/a<2.0: V lateral = min(q*L/2, V tabulado (3.0,c/a)) [refinamiento 2026-09-02]", sismicoBandaIntermedia.Exterior!.VSideMaxKNm, vEsperadaBanda, atol: 1e-6);
    // La cota refinada NUNCA debe aflojar respecto de la franja (<= franja); para esta geometría
    // de banda (b/a=1.367, c/a=1.067) la cota tabulada (3.0,c/a) queda ≈ al valor de la franja, así
    // que no se exige que sea estrictamente menor (sí lo es para c/a menores de la banda; el
    // refuerzo vertical Mx sí queda estrictamente refinado en todos los casos).
    totalAserciones++;
    if (myEsperadaBanda <= qExteriorBandaIntermedia * luzEjeAejeBanda * luzEjeAejeBanda / 8.0 + 1e-9 && mxTabBanda < mxCantilieverBanda - 1e-9)
        Console.WriteLine($"  [OK  ] Banda: la cota tabulada nunca afloja respecto de la franja (My={myEsperadaBanda:0.###} <= {qExteriorBandaIntermedia * luzEjeAejeBanda * luzEjeAejeBanda / 8.0:0.###}) y Mx queda estrictamente refinado ({mxTabBanda:0.###} < {mxCantilieverBanda:0.###})");
    else { fallos++; Console.WriteLine("  [FAIL] Banda: la cota tabulada afloja respecto de la franja o Mx no se refina"); }

    // FUNDAMENTO DEL REFINAMIENTO DE BANDA (2026-09-02): monotonicidad de |Mx| y |My| de esquina
    // en b/a y en c/a sobre las 15 combinaciones tabuladas del Capítulo 3, Caso 7 (lado largo,
    // escala unitaria).
    totalAserciones++;
    {
        var qM = 1.0; var aM = 1.0;
        double[] bVals = { 4.0, 3.0, 2.0, 1.5, 1.0 };
        double[][] cRows = { new[] { 3.0, 2.0, 1.5, 1.0, 0.5 }, new[] { 2.0, 1.5, 1.0, 0.5 }, new[] { 1.5, 1.0, 0.5 }, new[] { 1.0, 0.5 }, new[] { 0.5 } };
        var mx = new double[5, 5]; var my = new double[5, 5];
        for (int i = 0; i < 5; i++)
            for (int j = 0; j < cRows[i].Length; j++)
            {
                var lado = MurosRectangularesSismico.Calcular(bVals[i], cRows[i][j], qM, aM).LadoLargo;
                mx[i, j] = Math.Max(Math.Abs(lado.MxPosGobernanteKNmM), Math.Abs(lado.MxNegGobernanteKNmM));
                my[i, j] = Math.Max(Math.Abs(lado.MyPosGobernanteKNmM), Math.Abs(lado.MyNegGobernanteKNmM));
            }
        // c/a=0.5 presente en todas las filas: |Mx| y |My| crecen con b/a (1.0 -> 4.0). bVals va
        // de b/a=4.0 (i=0) a b/a=1.0 (i=4), así que al avanzar i (b/a decreciente) los valores no
        // deben crecer.
        bool monoB = true;
        for (int i = 0; i < 4; i++) { monoB &= mx[i, cRows[i].Length - 1] >= mx[i + 1, cRows[i + 1].Length - 1] - 1e-9; monoB &= my[i, cRows[i].Length - 1] >= my[i + 1, cRows[i + 1].Length - 1] - 1e-9; }
        // b/a=4.0: |My| crece con c/a (0.5 -> 3.0): cRows[0] va de c/a=3.0 a c/a=0.5, así que al
        // avanzar j (c/a decreciente) |My| no debe crecer. |Mx| se acota con el propio c/a del
        // panel (no se toma max en c/a), así que no se exige nada en esa dirección.
        bool monoC = true;
        for (int j = 0; j < 4; j++) monoC &= my[0, j] >= my[0, j + 1] - 1e-9;
        if (monoB && monoC)
            Console.WriteLine("  [OK  ] Monotonicidad verificada: |Mx| y |My| crecen con b/a (c/a=0.5) y |My| crece con c/a (b/a=4.0) -- fundamento del refinamiento de banda");
        else { fallos++; Console.WriteLine($"  [FAIL] Monotonicidad: monoB={monoB}, monoC={monoC}"); }
    }

    Console.WriteLine("-- Extremo a extremo: DisenarMuroLongitudinal con ambas condiciones fuera de dominio --");
    totalAserciones++;
    try
    {
        var disenoFueraDominio = DisenoMuros.DisenarMuroLongitudinal(proyectoFueraDominio, presionesFueraDominio, sismoHidroFueraDominio, sismoSueloFueraDominio);
        if (disenoFueraDominio.SismoIncluido && disenoFueraDominio.MotivoSismoOmitido is null)
            Console.WriteLine("  [OK  ] b/a<1.0: SismoIncluido=true (YA NO se omite), MotivoSismoOmitido=null");
        else { fallos++; Console.WriteLine($"  [FAIL] SismoIncluido={disenoFueraDominio.SismoIncluido}, MotivoSismoOmitido={disenoFueraDominio.MotivoSismoOmitido}"); }

        totalAserciones++;
        if (disenoFueraDominio.NotaAproximacionSismicaInterior is not null && disenoFueraDominio.NotaAproximacionSismicaExterior is not null)
            Console.WriteLine("  [OK  ] NotaAproximacionSismicaInterior y NotaAproximacionSismicaExterior no-null (ambas condiciones fuera de dominio en esta geometria)");
        else { fallos++; Console.WriteLine($"  [FAIL] NotaInterior={disenoFueraDominio.NotaAproximacionSismicaInterior}, NotaExterior={disenoFueraDominio.NotaAproximacionSismicaExterior}"); }

        // Cualquier direccion/cortante cuyo ComboGobernante sea sismico DEBE llevar el sufijo de
        // aproximacion conservadora en esta geometria concreta (interior Y exterior son ambas
        // conservadoras aqui, asi que no hay ningun candidato sismico "tabulado" que pudiera ganar
        // sin el sufijo).
        totalAserciones++;
        var direccionesFD = new[] { disenoFueraDominio.VerticalPositivo.ComboGobernante, disenoFueraDominio.VerticalNegativo.ComboGobernante, disenoFueraDominio.HorizontalPositivo.ComboGobernante, disenoFueraDominio.HorizontalNegativo.ComboGobernante };
        var cortantesFD = new[] { disenoFueraDominio.CortanteFondo.ComboGobernante, disenoFueraDominio.CortanteLateralMaximo.ComboGobernante, disenoFueraDominio.CortanteLateralMedio.ComboGobernante };
        var todosSismicosMarcados = direccionesFD.Concat(cortantesFD).All(c => !c.StartsWith("Sísmico") || c.Contains("aproximación conservadora"));
        if (todosSismicosMarcados) Console.WriteLine("  [OK  ] Todo ComboGobernante que empieza con \"Sísmico\" incluye el sufijo \"[aproximación conservadora...]\" en esta geometria");
        else { fallos++; Console.WriteLine("  [FAIL] Algun ComboGobernante sismico NO incluye el sufijo de aproximacion conservadora"); }

        // Correccion 2026-08-28 (auditoria externa del usuario, hallazgo H2): el motivo NO debe
        // filtrar el mensaje crudo de una excepcion .NET interna (nombre de parametro, "Parameter",
        // etc.) -- el mismo principio se mantiene para las notas de aproximacion conservadora.
        totalAserciones++;
        if (!disenoFueraDominio.NotaAproximacionSismicaInterior!.Contains("Parameter") && !disenoFueraDominio.NotaAproximacionSismicaExterior!.Contains("Parameter"))
            Console.WriteLine("  [OK  ] NotaAproximacionSismicaInterior/Exterior NO filtran el mensaje crudo de una excepcion .NET interna (sin \"Parameter\")");
        else { fallos++; Console.WriteLine("  [FAIL] Alguna nota filtra detalle interno de excepcion"); }
    }
    catch (Exception ex) { fallos++; Console.WriteLine($"  [FAIL] DisenarMuroLongitudinal lanzo excepcion inesperada: {ex.Message}"); }

    Console.WriteLine("-- R2: interior DENTRO de dominio, exterior FUERA de dominio -- uno no debe descartar al otro --");
    // Mismo plan (L=6.0,B=4.5) y HL=3.0 que geoSint (b/a=2.0,c/a=1.5, dentro de dominio para
    // interior), pero con Hm=7.5 (mucho mayor que HL): b/a=L/Hm=6.0/7.5=0.8<1.0, fuera del dominio
    // SISMICO (que exige b/a>=1.0) pero DENTRO del dominio ESTATICO, mas ancho ([0.5,4.0]) --
    // r=L/Hm=0.8 no dispara ningun rechazo en MurosRectangulares (motor estatico). Antes de esta
    // correccion, el try/catch UNICO de IntentarSismico habria descartado TAMBIEN el interior
    // valido -- ahora cada condicion se resuelve por separado. Em generoso (0.6m), mismo motivo que
    // geoFueraDominio arriba (la cota conservadora es intencionalmente exigente).
    var geoMixta = new Geometria(BAnchoM: 5.1, LLargoM: 6.6, HtAlturaM: 8.0, ConTapa: false, EmEspesorMuroM: 0.6, EfEspesorFondoM: 0.20, EtEspesorTapaM: 0.0, HLAlturaLiquidoM: 3.0, HmAlturaSueloSobreMuroM: 6.2, WextSobrecargaKNm2: 0.0); // Hm=6.2 (no 7.5): exterior sigue fuera de dominio (b/a=6.0/6.2=0.97<1.0) y con la cota corregida Mx=q·a²/2 la pared de 0.6 m resiste (ρ≈0.0125<ρmax)
    var proyectoMixto = new ProyectoTanque(geoMixta, matSint);
    var presionesMixto = PresionesLaterales.Calcular(proyectoMixto);
    var sismoHidroMixto = FuerzaSismicaHidrodinamica.Calcular(proyectoMixto, espectroSint);
    var sismoSueloMixto = FuerzaDinamicaSuelo.Calcular(proyectoMixto, sueloSint);
    var sismicoMixto = MurosRectangularesSismico.CalcularMuroLongitudinal(proyectoMixto, presionesMixto, sismoHidroMixto, sismoSueloMixto);

    totalAserciones++;
    if (!sismicoMixto.Interior.EsAproximacionConservadora && sismicoMixto.MotivoAproximacionInterior is null)
        Console.WriteLine("  [OK  ] Interior (b/a=2.0,c/a=1.5, dentro de dominio): NO usa aproximacion conservadora -- tabla del Capitulo 3 normal");
    else { fallos++; Console.WriteLine($"  [FAIL] Interior deberia venir de la tabla, no de la aproximacion: EsAproximacionConservadora={sismicoMixto.Interior.EsAproximacionConservadora}"); }

    totalAserciones++;
    if (sismicoMixto.Exterior is not null && sismicoMixto.Exterior.EsAproximacionConservadora && sismicoMixto.MotivoAproximacionExterior is not null)
        Console.WriteLine("  [OK  ] Exterior (b/a=7.5, fuera de dominio): SI usa aproximacion conservadora, y NO descarta el interior valido de arriba");
    else { fallos++; Console.WriteLine($"  [FAIL] Exterior deberia usar la aproximacion conservadora: {(sismicoMixto.Exterior is null ? "null" : $"EsAproximacionConservadora={sismicoMixto.Exterior.EsAproximacionConservadora}")}"); }

    // Verificacion independiente del interior tabulado (recalculado directamente contra Calcular,
    // sin pasar por CalcularMuroLongitudinal) -- confirma que el interior es EXACTAMENTE el mismo
    // resultado que produciria el motor de tabla normal, sin ninguna contaminacion del exterior
    // fuera de dominio.
    var qInteriorMixto = MurosRectangularesSismico.CalcularCargaSismicaInterior(
        presionesMixto.PhMaximaKNm2,
        sismoHidroMixto.MuroLongitudinal.PresionImpulsiva.FondoKNm2,
        sismoHidroMixto.MuroLongitudinal.PresionConvectiva.FondoKNm2);
    var interiorTabuladoDirecto = MurosRectangularesSismico.Calcular((geoMixta.LLargoM - geoMixta.EmEspesorMuroM) / geoMixta.HLAlturaLiquidoM, (geoMixta.BAnchoM - geoMixta.EmEspesorMuroM) / geoMixta.HLAlturaLiquidoM, qInteriorMixto, geoMixta.HLAlturaLiquidoM).LadoLargo;
    AssertTol("Interior (R2): coincide exacto con Calcular() directo, sin contaminacion del exterior fuera de dominio", sismicoMixto.Interior.MxPosGobernanteKNmM, interiorTabuladoDirecto.MxPosGobernanteKNmM, atol: 1e-9);

    var disenoMixto = DisenoMuros.DisenarMuroLongitudinal(proyectoMixto, presionesMixto, sismoHidroMixto, sismoSueloMixto);
    totalAserciones++;
    if (disenoMixto.SismoIncluido && disenoMixto.MotivoSismoOmitido is null && disenoMixto.NotaAproximacionSismicaInterior is null && disenoMixto.NotaAproximacionSismicaExterior is not null)
        Console.WriteLine("  [OK  ] DisenarMuroLongitudinal (R2): sismo incluido, nota conservadora SOLO en Exterior, Interior limpio -- ninguna condicion descarta a la otra");
    else { fallos++; Console.WriteLine($"  [FAIL] SismoIncluido={disenoMixto.SismoIncluido}, MotivoSismoOmitido={disenoMixto.MotivoSismoOmitido}, NotaInterior={disenoMixto.NotaAproximacionSismicaInterior}, NotaExterior={disenoMixto.NotaAproximacionSismicaExterior}"); }

    // Hallazgo confirmado 2026-08-26 via prueba real de usuario: TipoTanque.Superficial (Hm=0 por
    // definicion, ver Geometria.Validar) lanzaba ArgumentOutOfRangeException("a debe ser mayor que
    // 0", parametro "aM") sin capturar, porque MurosRectangulares intentaba construir el panel
    // exterior con a=Hm=0. Corregido para devolver Exterior=null (CalcularExteriorOSinSuelo) y
    // propagar la ausencia hasta DisenoMuros -- esta asercion reproduce exactamente el caso de uso
    // del usuario extremo a extremo (Geometria -> DisenarMuroLongitudinal/Transversal) para que
    // nunca vuelva a pasar inadvertido.
    Console.WriteLine("-- TipoTanque.Superficial (Hm=0): la condicion exterior no existe, no debe lanzar --");
    var geoSuperficial = new Geometria(BAnchoM: 6.0, LLargoM: 8.0, HtAlturaM: 4.5, ConTapa: true, EmEspesorMuroM: 0.3, EfEspesorFondoM: 0.35, EtEspesorTapaM: 0.2, HLAlturaLiquidoM: 4.0, HmAlturaSueloSobreMuroM: 0.0, WextSobrecargaKNm2: 0.0, Tipo: TipoTanque.Superficial);
    var proyectoSuperficial = new ProyectoTanque(geoSuperficial, matSint);
    var presionesSuperficial = PresionesLaterales.Calcular(proyectoSuperficial);

    totalAserciones++;
    try
    {
        var estaticoSuperficialL = MurosRectangulares.CalcularMuroLongitudinal(proyectoSuperficial, presionesSuperficial);
        var estaticoSuperficialT = MurosRectangulares.CalcularMuroTransversal(proyectoSuperficial, presionesSuperficial);
        if (estaticoSuperficialL.Exterior is null && estaticoSuperficialT.Exterior is null && estaticoSuperficialL.Interior is not null && estaticoSuperficialT.Interior is not null)
            Console.WriteLine("  [OK  ] MurosRectangulares.CalcularMuroLongitudinal/Transversal: Exterior=null, Interior calculado normalmente, sin lanzar");
        else { fallos++; Console.WriteLine("  [FAIL] Exterior/Interior no tienen el nullability esperado para Hm=0"); }
    }
    catch (Exception ex) { fallos++; Console.WriteLine($"  [FAIL] MurosRectangulares lanzo para TipoTanque.Superficial (Hm=0): {ex.GetType().Name}: {ex.Message}"); }

    totalAserciones++;
    try
    {
        var disenoSuperficialL = DisenoMuros.DisenarMuroLongitudinal(proyectoSuperficial, presionesSuperficial, null, null);
        var disenoSuperficialT = DisenoMuros.DisenarMuroTransversal(proyectoSuperficial, presionesSuperficial, null, null);
        if (disenoSuperficialL.MotivoExteriorOmitido is not null && disenoSuperficialL.MotivoExteriorOmitido.Contains("Hm=0")
            && !disenoSuperficialL.MotivoExteriorOmitido.Contains("CalcularExteriorOSinSuelo")
            && disenoSuperficialT.MotivoExteriorOmitido is not null
            && disenoSuperficialL.VerticalPositivo.ComboGobernante.StartsWith("Estático interior")
            && disenoSuperficialL.VerticalPositivo.MuKNm > 0)
            Console.WriteLine($"  [OK  ] DisenarMuroLongitudinal/Transversal completan sin lanzar para Superficial: MotivoExteriorOmitido explicito, envolvente gobernada por 'Estático interior' (Mx+={disenoSuperficialL.VerticalPositivo.MuKNm:0.###} kNm/m)");
        else { fallos++; Console.WriteLine($"  [FAIL] MotivoExteriorOmitido={disenoSuperficialL.MotivoExteriorOmitido}, ComboGobernante={disenoSuperficialL.VerticalPositivo.ComboGobernante}"); }
    }
    catch (Exception ex) { fallos++; Console.WriteLine($"  [FAIL] DisenoMuros lanzo para TipoTanque.Superficial (Hm=0): {ex.GetType().Name}: {ex.Message}"); }
}

Console.WriteLine();
Console.WriteLine("=== Modulo 8 conectado a placas (DisenoPlacas: cubierta y fondo, recubrimiento mixto en fondo) ===");
{
    var geoTapa = new Geometria(BAnchoM: 4.5, LLargoM: 6.0, HtAlturaM: 3.8, ConTapa: true, EmEspesorMuroM: 0.25, EfEspesorFondoM: 0.20, EtEspesorTapaM: 0.20, HLAlturaLiquidoM: 3.0, HmAlturaSueloSobreMuroM: 3.0, WextSobrecargaKNm2: 0.0);
    var matTapa = new Materiales(FcMPa: 21, FyMPa: 420, GammaConcretoKNm3: 24, GammaLiquidoKNm3: 9.81, GammaSueloKNm3: 16, PhiGradosAnguloFriccionSuelo: 30);
    var proyectoTapa = new ProyectoTanque(geoTapa, matTapa);
    var cargasTapa = CargasGravitacionales.Calcular(proyectoTapa, cargaVivaCubiertaKNm2: 1.5, cargaAdicionalCubiertaKNm2: 0.5);

    Console.WriteLine("-- Placa de cubierta: ambas caras formadas (mismo recubrimiento) --");
    var disenoCubierta = DisenoPlacas.DisenarPlacaCubierta(proyectoTapa, cargasTapa, cvKNm2: 1.5, cgKNm2: 0.5);
    AssertTol("Cubierta: dInferior == dSuperior (misma recubrimiento formado en ambas caras)",
        disenoCubierta.MxPositivo.DEfectivoM, disenoCubierta.MxNegativo.DEfectivoM, atol: 1e-9);
    AssertTol("Cubierta: d = espesor - 0.050 - Ø/2 (et=0.20m)", disenoCubierta.MxPositivo.DEfectivoM,
        RecubrimientosNSR10.CalcularDEfectivo(geoTapa.EtEspesorTapaM, RecubrimientosNSR10.RecubrimientoFormadoM, CatalogoBarras.DiametroPredeterminadoBarraMm), atol: 1e-9);

    totalAserciones++;
    var msMenorQueMuCubierta = new[] { disenoCubierta.MxPositivo, disenoCubierta.MxNegativo, disenoCubierta.MyPositivo, disenoCubierta.MyNegativo }
        .All(d => d.MsKNm <= d.MuKNm + 1e-9 && d.MsKNm >= 0);
    if (msMenorQueMuCubierta) Console.WriteLine("  [OK  ] Cubierta: Ms (servicio, sin mayorar) <= Mu (mayorado) en las 4 direcciones, ambos no negativos");
    else { fallos++; Console.WriteLine("  [FAIL] Cubierta: algun Ms no cumple 0 <= Ms <= Mu"); }

    // Diseno a flexion (+ segunda pasada de fisuracion, fix del 2026-08-26) autoconsistente -- ver
    // el bloque analogo de DisenoMuros mas arriba para el razonamiento completo.
    var ecTapa = 4700.0 * Math.Sqrt(matTapa.FcMPa);
    var (flexionDirectaCubierta, _) = DisenoFlexionCortanteFisuracion.DisenarFlexionConControlFisuracion(
        disenoCubierta.MxPositivo.MuKNm, disenoCubierta.MxPositivo.MsKNm, disenoCubierta.MxPositivo.DEfectivoM, 1.0,
        matTapa.FyMPa, matTapa.FcMPa, 200000.0, ecTapa, geoTapa.EtEspesorTapaM,
        diametroBarraMm: CatalogoBarras.DiametroPredeterminadoBarraMm,
        cuantiaMinima: CatalogoBarras.CuantiaMinimaRetracionTemperatura(geoTapa.LLargoM, matTapa.FyMPa),
        espaciamientoMaximoM: CatalogoBarras.EspaciamientoMaximoMuroM);
    AssertTol("Cubierta: Flexion.Rho (Mx+) autoconsistente con DisenarFlexionConControlFisuracion(Mu,Ms,d,...) directo", disenoCubierta.MxPositivo.Flexion.Rho, flexionDirectaCubierta.Rho, atol: 1e-9);

    Console.WriteLine("-- Placa de fondo: cara inferior contra el suelo (75mm), cara superior formada (50mm) --");
    var disenoFondo = DisenoPlacas.DisenarPlacaFondo(proyectoTapa, cargasTapa, cvKNm2: 0.0);
    totalAserciones++;
    if (disenoFondo.MxPositivo.DEfectivoM < disenoFondo.MxNegativo.DEfectivoM)
        Console.WriteLine($"  [OK  ] Fondo: dInferior ({disenoFondo.MxPositivo.DEfectivoM:0.####}m, recub. 75mm contra suelo) < dSuperior ({disenoFondo.MxNegativo.DEfectivoM:0.####}m, recub. 50mm formado)");
    else { fallos++; Console.WriteLine($"  [FAIL] Fondo: dInferior ({disenoFondo.MxPositivo.DEfectivoM}) deberia ser MENOR que dSuperior ({disenoFondo.MxNegativo.DEfectivoM})"); }

    AssertTol("Fondo: dInferior = espesor - 0.075 - Ø/2", disenoFondo.MxPositivo.DEfectivoM,
        RecubrimientosNSR10.CalcularDEfectivo(geoTapa.EfEspesorFondoM, RecubrimientosNSR10.RecubrimientoContraSueloM, CatalogoBarras.DiametroPredeterminadoBarraMm), atol: 1e-9);
    AssertTol("Fondo: dSuperior = espesor - 0.050 - Ø/2", disenoFondo.MxNegativo.DEfectivoM,
        RecubrimientosNSR10.CalcularDEfectivo(geoTapa.EfEspesorFondoM, RecubrimientosNSR10.RecubrimientoFormadoM, CatalogoBarras.DiametroPredeterminadoBarraMm), atol: 1e-9);
    AssertTol("Fondo: dCortante = min(dInferior, dSuperior) -- conservador", disenoFondo.CortanteX.DEfectivoM,
        Math.Min(disenoFondo.MxPositivo.DEfectivoM, disenoFondo.MxNegativo.DEfectivoM), atol: 1e-9);

    totalAserciones++;
    var cuantiasFondoEnRango = new[] { disenoFondo.MxPositivo, disenoFondo.MxNegativo, disenoFondo.MyPositivo, disenoFondo.MyNegativo }
        .All(d => d.Flexion.Rho >= DisenoFlexionCortanteFisuracion.CuantiaMinima - 1e-9 && d.Flexion.Rho <= DisenoFlexionCortanteFisuracion.CuantiaMaxima + 1e-9);
    if (cuantiasFondoEnRango) Console.WriteLine("  [OK  ] Fondo: las 4 cuantias de diseno caen en [CuantiaMinima, CuantiaMaxima]");
    else { fallos++; Console.WriteLine("  [FAIL] Fondo: alguna cuantia cae fuera de [CuantiaMinima, CuantiaMaxima]"); }

    Console.WriteLine("-- Placa de cubierta sobre tanque sin tapa: DisenarPlacaCubierta debe lanzar --");
    var geoSinTapa = geoTapa with { ConTapa = false, EtEspesorTapaM = 0.0 };
    var proyectoSinTapa = new ProyectoTanque(geoSinTapa, matTapa);
    var cargasSinTapa = CargasGravitacionales.Calcular(proyectoSinTapa);
    totalAserciones++;
    try { DisenoPlacas.DisenarPlacaCubierta(proyectoSinTapa, cargasSinTapa, 0.0, 0.0); fallos++; Console.WriteLine("  [FAIL] DisenarPlacaCubierta sobre tanque sin tapa no lanzo"); }
    catch (ArgumentException) { Console.WriteLine("  [OK  ] DisenarPlacaCubierta sobre tanque sin tapa (ConTapa=false) lanza ArgumentException, propagada de PlacasRectangulares.CalcularPlacaCubierta"); }
}

Console.WriteLine();
Console.WriteLine("=== TipoTanque y Flotabilidad (backlog v2, item 2/6 -- ACI 350.4R-04 §3.1.2) ===");
{
    var matFlot = new Materiales(FcMPa: 21, FyMPa: 420, GammaConcretoKNm3: 24, GammaLiquidoKNm3: 9.81, GammaSueloKNm3: 16, PhiGradosAnguloFriccionSuelo: 30);
    var geoBase = new Geometria(BAnchoM: 6.0, LLargoM: 8.0, HtAlturaM: 4.5, ConTapa: true, EmEspesorMuroM: 0.3, EfEspesorFondoM: 0.35, EtEspesorTapaM: 0.2, HLAlturaLiquidoM: 4.0, HmAlturaSueloSobreMuroM: 3.0, WextSobrecargaKNm2: 0.0);

    Console.WriteLine("-- Geometria.Validar(): reglas de TipoTanque/AlturaNivelFreaticoM --");
    totalAserciones++;
    try { (geoBase with { Tipo = TipoTanque.Superficial }).Validar(); fallos++; Console.WriteLine("  [FAIL] Superficial con Hm=3.0 (>0) no lanzo"); }
    catch (ArgumentException) { Console.WriteLine("  [OK  ] TipoTanque.Superficial con Hm>0 lanza ArgumentException (sin relleno de suelo contra el muro por definicion)"); }

    var geoSuperficial = geoBase with { Tipo = TipoTanque.Superficial, HmAlturaSueloSobreMuroM = 0.0 };
    totalAserciones++;
    try { geoSuperficial.Validar(); Console.WriteLine("  [OK  ] TipoTanque.Superficial con Hm=0 valida sin lanzar"); }
    catch (Exception ex) { fallos++; Console.WriteLine($"  [FAIL] Superficial con Hm=0 lanzo inesperadamente: {ex.Message}"); }

    Console.WriteLine("-- Superficial (Hm=0): la condicion exterior de MurosRectangulares no gobierna nunca -- verificacion estructural, sin gating adicional en el modulo --");
    var proyectoSuperficial = new ProyectoTanque(geoSuperficial, matFlot);
    var presionesSuperficial = PresionesLaterales.Calcular(proyectoSuperficial);
    totalAserciones++;
    if (presionesSuperficial.Ps2MaximaKNm2 == 0.0) Console.WriteLine("  [OK  ] Ps2Maxima = 0 con Hm=0 -- la combinacion exterior de suelo queda identicamente nula por la fisica de la carga, no por un gating explicito");
    else { fallos++; Console.WriteLine($"  [FAIL] Ps2Maxima esperado 0, actual {presionesSuperficial.Ps2MaximaKNm2}"); }

    totalAserciones++;
    try { (geoBase with { Tipo = TipoTanque.EnterradoConNivelFreatico }).Validar(); fallos++; Console.WriteLine("  [FAIL] EnterradoConNivelFreatico sin AlturaNivelFreaticoM no lanzo"); }
    catch (ArgumentException) { Console.WriteLine("  [OK  ] TipoTanque.EnterradoConNivelFreatico sin AlturaNivelFreaticoM lanza ArgumentException"); }

    totalAserciones++;
    try { (geoBase with { Tipo = TipoTanque.EnterradoConNivelFreatico, AlturaNivelFreaticoM = geoBase.HmAlturaSueloSobreMuroM + 0.5 }).Validar(); fallos++; Console.WriteLine("  [FAIL] AlturaNivelFreaticoM > Hm no lanzo"); }
    catch (ArgumentOutOfRangeException) { Console.WriteLine("  [OK  ] AlturaNivelFreaticoM > Hm lanza ArgumentOutOfRangeException (el nivel freatico no puede superar la altura de suelo sobre el muro)"); }

    totalAserciones++;
    try { (geoBase with { AlturaNivelFreaticoM = 1.0 }).Validar(); fallos++; Console.WriteLine("  [FAIL] AlturaNivelFreaticoM no-null con Tipo=EnterradoSinNivelFreatico (default) no lanzo"); }
    catch (ArgumentException) { Console.WriteLine("  [OK  ] AlturaNivelFreaticoM no-null fuera de EnterradoConNivelFreatico lanza ArgumentException"); }

    Console.WriteLine("-- V-1 (cuarta ronda): regla simetrica Enterrado -> Hm>0, y Hm no negativo --");
    totalAserciones++;
    try { (geoBase with { HmAlturaSueloSobreMuroM = 0.0 }).Validar(); fallos++; Console.WriteLine("  [FAIL] EnterradoSinNivelFreatico (default) con Hm=0 no lanzo"); }
    catch (ArgumentException) { Console.WriteLine("  [OK  ] EnterradoSinNivelFreatico con Hm=0 lanza ArgumentException (el tanque enterrado tiene relleno de suelo contra el muro)"); }

    totalAserciones++;
    try { (geoBase with { Tipo = TipoTanque.EnterradoConNivelFreatico, HmAlturaSueloSobreMuroM = 0.0, AlturaNivelFreaticoM = 1.0 }).Validar(); fallos++; Console.WriteLine("  [FAIL] EnterradoConNivelFreatico con Hm=0 no lanzo"); }
    catch (ArgumentException) { Console.WriteLine("  [OK  ] EnterradoConNivelFreatico con Hm=0 lanza ArgumentException (Hm>0 exigido, antes de evaluar el nivel freatico)"); }

    totalAserciones++;
    try { (geoBase with { HmAlturaSueloSobreMuroM = -1.0 }).Validar(); fallos++; Console.WriteLine("  [FAIL] Hm negativo no lanzo"); }
    catch (ArgumentOutOfRangeException) { Console.WriteLine("  [OK  ] Hm negativo lanza ArgumentOutOfRangeException"); }

    // N1 (tercera/cuarta ronda): los mensajes de validacion emitidos al usuario NO deben remitir
    // al codigo fuente (nombres de archivo .cs, de clase o de metodo) -- regresion de las fugas
    // N1-R1 (Geometria.cs) y N1-R2 (DisenoMuros.cs).
    totalAserciones++;
    try { (geoBase with { Tipo = TipoTanque.Superficial }).Validar(); fallos++; Console.WriteLine("  [FAIL] Superficial con Hm>0 no lanzo (re-chequeo para mensaje N1)"); }
    catch (ArgumentException ex)
    {
        if (ex.Message.Contains("TipoTanque.cs") || ex.Message.Contains("Dominio"))
        { fallos++; Console.WriteLine($"  [FAIL] Mensaje de validacion remite al codigo fuente: {ex.Message}"); }
        else Console.WriteLine("  [OK  ] Mensaje de validacion autoexplicativo (sin remision a Dominio/TipoTanque.cs)");
    }

    Console.WriteLine("-- Flotabilidad.Verificar: rechazo explicito fuera de EnterradoConNivelFreatico --");
    var cargasBase = CargasGravitacionales.Calcular(new ProyectoTanque(geoBase, matFlot), cargaVivaCubiertaKNm2: 0.0, cargaAdicionalCubiertaKNm2: 0.0);
    totalAserciones++;
    try { Flotabilidad.Verificar(new ProyectoTanque(geoBase, matFlot), cargasBase); fallos++; Console.WriteLine("  [FAIL] Flotabilidad.Verificar sobre TipoTanque.EnterradoSinNivelFreatico (default) no lanzo"); }
    catch (InvalidOperationException) { Console.WriteLine("  [OK  ] Flotabilidad.Verificar sobre TipoTanque != EnterradoConNivelFreatico lanza InvalidOperationException"); }

    Console.WriteLine("-- Flotabilidad.Verificar: caso que CUMPLE (nivel freatico somero, h=1.0m) --");
    var geoFlotOk = geoBase with { Tipo = TipoTanque.EnterradoConNivelFreatico, AlturaNivelFreaticoM = 1.0 };
    var proyectoFlotOk = new ProyectoTanque(geoFlotOk, matFlot);
    var cargasFlotOk = CargasGravitacionales.Calcular(proyectoFlotOk, cargaVivaCubiertaKNm2: 0.0, cargaAdicionalCubiertaKNm2: 0.0);
    var flotOk = Flotabilidad.Verificar(proyectoFlotOk, cargasFlotOk);

    var areaPlanta = geoBase.BAnchoM * geoBase.LLargoM; // 48 m2
    AssertTol("PesoPropioKN == PttTotalKN (sin mayorar, reutilizado de CargasGravitacionales)", flotOk.PesoPropioKN, cargasFlotOk.PttTotalKN, atol: 1e-9);
    AssertTol("SubpresionKN = gammaAgua x Area x h (h=1.0m)", flotOk.SubpresionKN, Flotabilidad.GammaAguaKNm3 * areaPlanta * 1.0, atol: 1e-6);
    AssertTol("FS = PesoPropio / Subpresion", flotOk.FS, flotOk.PesoPropioKN / flotOk.SubpresionKN, atol: 1e-9);
    totalAserciones++;
    if (flotOk.Cumple && flotOk.FS >= Flotabilidad.FactorSeguridadMinimo && flotOk.DeficitPesoKN == 0.0)
        Console.WriteLine($"  [OK  ] h=1.0m: Cumple=true, FS={flotOk.FS:0.###} >= {Flotabilidad.FactorSeguridadMinimo}, DeficitPesoKN=0");
    else { fallos++; Console.WriteLine($"  [FAIL] h=1.0m: Cumple={flotOk.Cumple}, FS={flotOk.FS}, DeficitPesoKN={flotOk.DeficitPesoKN}"); }

    Console.WriteLine("-- Flotabilidad.Verificar: caso que NO CUMPLE (nivel freatico al maximo permitido, h=Hm=3.0m) --");
    var geoFlotFail = geoBase with { Tipo = TipoTanque.EnterradoConNivelFreatico, AlturaNivelFreaticoM = geoBase.HmAlturaSueloSobreMuroM };
    var proyectoFlotFail = new ProyectoTanque(geoFlotFail, matFlot);
    var cargasFlotFail = CargasGravitacionales.Calcular(proyectoFlotFail, cargaVivaCubiertaKNm2: 0.0, cargaAdicionalCubiertaKNm2: 0.0);
    var flotFail = Flotabilidad.Verificar(proyectoFlotFail, cargasFlotFail);

    AssertTol("SubpresionKN = gammaAgua x Area x h (h=Hm=3.0m)", flotFail.SubpresionKN, Flotabilidad.GammaAguaKNm3 * areaPlanta * geoBase.HmAlturaSueloSobreMuroM, atol: 1e-6);
    totalAserciones++;
    if (!flotFail.Cumple && flotFail.FS < Flotabilidad.FactorSeguridadMinimo)
        Console.WriteLine($"  [OK  ] h=3.0m: Cumple=false, FS={flotFail.FS:0.###} < {Flotabilidad.FactorSeguridadMinimo}");
    else { fallos++; Console.WriteLine($"  [FAIL] h=3.0m: se esperaba Cumple=false, actual Cumple={flotFail.Cumple}, FS={flotFail.FS}"); }

    AssertTol("DeficitPesoKN = FactorSeguridadMinimo x Subpresion - PesoPropio (caso que no cumple)",
        flotFail.DeficitPesoKN, Flotabilidad.FactorSeguridadMinimo * flotFail.SubpresionKN - flotFail.PesoPropioKN, atol: 1e-6);
    totalAserciones++;
    if (flotFail.DeficitPesoKN > 0 && (flotFail.PesoPropioKN + flotFail.DeficitPesoKN) / flotFail.SubpresionKN >= Flotabilidad.FactorSeguridadMinimo - 1e-9)
        Console.WriteLine($"  [OK  ] DeficitPesoKN={flotFail.DeficitPesoKN:0.###} kN -- sumado al peso propio alcanza exactamente el FS minimo");
    else { fallos++; Console.WriteLine($"  [FAIL] DeficitPesoKN={flotFail.DeficitPesoKN} no lleva el FS al minimo exigido"); }

    Console.WriteLine("-- Materiales.Validar(): GammaSueloSaturadoKNm3, nueva regla (backlog v2, sobreancho) --");
    totalAserciones++;
    try { (matFlot with { GammaSueloSaturadoKNm3 = -1.0 }).Validar(); fallos++; Console.WriteLine("  [FAIL] GammaSueloSaturadoKNm3 negativo no lanzo"); }
    catch (ArgumentOutOfRangeException) { Console.WriteLine("  [OK  ] GammaSueloSaturadoKNm3 <= 0 lanza ArgumentOutOfRangeException"); }
    totalAserciones++;
    try { matFlot.Validar(); Console.WriteLine("  [OK  ] GammaSueloSaturadoKNm3=null (valor por defecto) valida sin lanzar -- retrocompatible"); }
    catch (Exception ex) { fallos++; Console.WriteLine($"  [FAIL] Materiales sin GammaSueloSaturadoKNm3 lanzo inesperadamente: {ex.Message}"); }

    Console.WriteLine("-- Flotabilidad.CalcularSobreancho: rechazos explicitos --");
    totalAserciones++;
    try { Flotabilidad.CalcularSobreancho(proyectoFlotOk, cargasFlotOk, flotOk); fallos++; Console.WriteLine("  [FAIL] CalcularSobreancho sobre un resultado que ya Cumple no lanzo"); }
    catch (InvalidOperationException) { Console.WriteLine("  [OK  ] CalcularSobreancho sobre un resultado que ya Cumple lanza InvalidOperationException (no hay deficit que cubrir)"); }
    totalAserciones++;
    try { Flotabilidad.CalcularSobreancho(proyectoFlotFail, cargasFlotFail, flotFail); fallos++; Console.WriteLine("  [FAIL] CalcularSobreancho sin GammaSueloSaturadoKNm3 no lanzo"); }
    catch (ArgumentException) { Console.WriteLine("  [OK  ] CalcularSobreancho sin Materiales.GammaSueloSaturadoKNm3 lanza ArgumentException"); }

    Console.WriteLine("-- Flotabilidad.CalcularSobreancho: caso con solucion (deficit real, submersion parcial de la proyeccion) --");
    var matSobreOk = new Materiales(FcMPa: 21, FyMPa: 420, GammaConcretoKNm3: 24, GammaLiquidoKNm3: 9.81, GammaSueloKNm3: 17, PhiGradosAnguloFriccionSuelo: 30, GammaSueloSaturadoKNm3: 20.0);
    var geoSobreBase = new Geometria(BAnchoM: 8.0, LLargoM: 8.0, HtAlturaM: 4.0, ConTapa: false, EmEspesorMuroM: 0.3, EfEspesorFondoM: 0.3, EtEspesorTapaM: 0.0, HLAlturaLiquidoM: 3.5, HmAlturaSueloSobreMuroM: 2.5, WextSobrecargaKNm2: 0.0,
        Tipo: TipoTanque.EnterradoConNivelFreatico, AlturaNivelFreaticoM: 2.0);
    var proyectoSobreOk = new ProyectoTanque(geoSobreBase, matSobreOk);
    var cargasSobreOk = CargasGravitacionales.Calcular(proyectoSobreOk);
    var flotSobreOk = Flotabilidad.Verificar(proyectoSobreOk, cargasSobreOk);
    totalAserciones++;
    if (!flotSobreOk.Cumple) Console.WriteLine($"  [OK  ] Geometria sintetica construida deliberadamente sin cumplir flotabilidad: FS={flotSobreOk.FS:0.###} < {Flotabilidad.FactorSeguridadMinimo}, DeficitPesoKN={flotSobreOk.DeficitPesoKN:0.###}");
    else { fallos++; Console.WriteLine($"  [FAIL] Se esperaba un deficit de flotabilidad para poder probar el sobreancho, pero Cumple=true (FS={flotSobreOk.FS})"); }

    var sobre = Flotabilidad.CalcularSobreancho(proyectoSobreOk, cargasSobreOk, flotSobreOk);
    totalAserciones++;
    if (sobre.EsPosible && sobre.SobreanchoRequeridoM is > 0) Console.WriteLine($"  [OK  ] EsPosible=true, SobreanchoRequeridoM={sobre.SobreanchoRequeridoM:0.####}m");
    else { fallos++; Console.WriteLine($"  [FAIL] Se esperaba EsPosible=true con un sobreancho positivo; EsPosible={sobre.EsPosible}, x={sobre.SobreanchoRequeridoM}"); }

    // Recalculo independiente (mismas formulas, escritas de nuevo aqui) para verificar la implementacion, no solo autoconsistencia interna.
    var hVerif = geoSobreBase.AlturaNivelFreaticoM!.Value;
    var wUnitEsperado = matSobreOk.GammaConcretoKNm3 * geoSobreBase.EfEspesorFondoM
        + (geoSobreBase.HmAlturaSueloSobreMuroM - hVerif) * matSobreOk.GammaSueloKNm3
        + hVerif * (matSobreOk.GammaSueloSaturadoKNm3!.Value - Flotabilidad.GammaAguaKNm3);
    var uUnitEsperado = Flotabilidad.GammaAguaKNm3 * hVerif;
    AssertTol("PesoConcretoUnitarioKNm2 = gammaConcreto*ef + (Hm-h)*gammaSuelo + h*(gammaSueloSat-gammaAgua)", sobre.PesoConcretoUnitarioKNm2, wUnitEsperado, atol: 1e-9);
    AssertTol("SubpresionUnitariaKNm2 = gammaAgua*h", sobre.SubpresionUnitariaKNm2, uUnitEsperado, atol: 1e-9);

    var areaDeltaEsperada = flotSobreOk.DeficitPesoKN / (wUnitEsperado - Flotabilidad.FactorSeguridadMinimo * uUnitEsperado);
    AssertTol("AreaProyeccionM2 = DeficitPesoKN / (w - FSmin*u)", sobre.AreaProyeccionM2!.Value, areaDeltaEsperada, atol: 1e-6);

    var bMasL = geoSobreBase.BAnchoM + geoSobreBase.LLargoM;
    var xEsperado = (-bMasL + Math.Sqrt(bMasL * bMasL + 4 * areaDeltaEsperada)) / 4;
    AssertTol("SobreanchoRequeridoM resuelve 4x^2+2(B+L)x-AreaDelta=0 (raiz positiva)", sobre.SobreanchoRequeridoM!.Value, xEsperado, atol: 1e-6);

    // Verificacion de que ΔArea(x) reproduce el area de proyeccion devuelta (marco perimetral).
    var deltaAreaDesdeX = 2 * xEsperado * bMasL + 4 * xEsperado * xEsperado;
    AssertTol("Marco perimetral: 2x(B+L)+4x^2 coincide con AreaProyeccionM2", deltaAreaDesdeX, sobre.AreaProyeccionM2!.Value, atol: 1e-6);

    totalAserciones++;
    if (sobre.FSConProyeccion is not null && Math.Abs(sobre.FSConProyeccion.Value - Flotabilidad.FactorSeguridadMinimo) < 1e-6)
        Console.WriteLine($"  [OK  ] FSConProyeccion={sobre.FSConProyeccion:0.#####} coincide exactamente con FactorSeguridadMinimo (valor limite, por construccion)");
    else { fallos++; Console.WriteLine($"  [FAIL] FSConProyeccion esperado {Flotabilidad.FactorSeguridadMinimo}, actual {sobre.FSConProyeccion}"); }

    AssertTol("PesoPropioConProyeccionKN = PesoPropio0 + PesoConcretoProyeccionKN + PesoSueloSobreProyeccionKN",
        sobre.PesoPropioConProyeccionKN!.Value, flotSobreOk.PesoPropioKN + sobre.PesoConcretoProyeccionKN!.Value + sobre.PesoSueloSobreProyeccionKN!.Value, atol: 1e-6);
    AssertTol("SubpresionConProyeccionKN = gammaAgua*(Area0+AreaProyeccion)*h",
        sobre.SubpresionConProyeccionKN!.Value, Flotabilidad.GammaAguaKNm3 * (geoSobreBase.BAnchoM * geoSobreBase.LLargoM + sobre.AreaProyeccionM2!.Value) * hVerif, atol: 1e-6);
    AssertTol("PesoConcretoProyeccionKN = gammaConcreto*ef*AreaProyeccion",
        sobre.PesoConcretoProyeccionKN!.Value, matSobreOk.GammaConcretoKNm3 * geoSobreBase.EfEspesorFondoM * sobre.AreaProyeccionM2!.Value, atol: 1e-6);

    Console.WriteLine("-- Flotabilidad.CalcularSobreancho: caso SIN solucion (suelo sumergido casi tan liviano como el agua -- ensanchar no ayuda) --");
    var matSobreImposible = matSobreOk with { GammaSueloSaturadoKNm3 = 9.85 }; // sumergido = 0.04 kN/m3, casi nulo
    var proyectoSobreImposible = new ProyectoTanque(geoSobreBase, matSobreImposible);
    var cargasSobreImposible = CargasGravitacionales.Calcular(proyectoSobreImposible);
    var flotSobreImposible = Flotabilidad.Verificar(proyectoSobreImposible, cargasSobreImposible);
    totalAserciones++;
    if (!flotSobreImposible.Cumple) Console.WriteLine($"  [OK  ] Mismo deficit de flotabilidad (el peso propio no cambia con GammaSueloSaturadoKNm3): FS={flotSobreImposible.FS:0.###}");
    else { fallos++; Console.WriteLine("  [FAIL] Se esperaba deficit tambien en este caso"); }

    var sobreImposible = Flotabilidad.CalcularSobreancho(proyectoSobreImposible, cargasSobreImposible, flotSobreImposible);
    totalAserciones++;
    if (!sobreImposible.EsPosible && sobreImposible.SobreanchoRequeridoM is null && sobreImposible.Mensaje.Contains("Ningún sobreancho finito"))
        Console.WriteLine("  [OK  ] EsPosible=false, SobreanchoRequeridoM=null, Mensaje explica que ensanchar no ayuda (suelo sumergido demasiado liviano frente al FS exigido)");
    else { fallos++; Console.WriteLine($"  [FAIL] Se esperaba EsPosible=false con mensaje explicativo; EsPosible={sobreImposible.EsPosible}, Mensaje={sobreImposible.Mensaje}"); }
    totalAserciones++;
    var margenImposible = sobreImposible.PesoConcretoUnitarioKNm2 - Flotabilidad.FactorSeguridadMinimo * sobreImposible.SubpresionUnitariaKNm2;
    if (margenImposible <= 0) Console.WriteLine($"  [OK  ] Confirmado numericamente: w-FSmin*u={margenImposible:0.####} <= 0 (w={sobreImposible.PesoConcretoUnitarioKNm2:0.###}, u={sobreImposible.SubpresionUnitariaKNm2:0.###})");
    else { fallos++; Console.WriteLine($"  [FAIL] margen esperado <= 0, actual {margenImposible}"); }

    Console.WriteLine("-- DisenoLosaFondoSubpresion.Disenar: rechazo explicito fuera de EnterradoConNivelFreatico --");
    totalAserciones++;
    try { DisenoLosaFondoSubpresion.Disenar(new ProyectoTanque(geoBase, matFlot), cargasBase); fallos++; Console.WriteLine("  [FAIL] Disenar sobre TipoTanque.EnterradoSinNivelFreatico (default) no lanzo"); }
    catch (InvalidOperationException) { Console.WriteLine("  [OK  ] Disenar sobre TipoTanque != EnterradoConNivelFreatico lanza InvalidOperationException"); }

    Console.WriteLine("-- DisenoLosaFondoSubpresion.Disenar: NO Aplica (nivel freatico somero, h=1.0m -- el peso propio ya contrarresta la subpresion mayorada) --");
    var dAreal1 = cargasFlotOk.PttTotalKN / (geoFlotOk.BAnchoM * geoFlotOk.LLargoM);
    var qMayorado1Esperado = 1.4 * Flotabilidad.GammaAguaKNm3 * 1.0 - 0.9 * dAreal1;
    totalAserciones++;
    if (qMayorado1Esperado <= 0) Console.WriteLine($"  [OK  ] Caso construido deliberadamente sin gobernar localmente: q_neto_mayorado esperado={qMayorado1Esperado:0.###} kN/m2 <= 0");
    else { fallos++; Console.WriteLine($"  [FAIL] se esperaba q_neto_mayorado<=0 para este caso, actual {qMayorado1Esperado}"); }

    var disenoSubNoAplica = DisenoLosaFondoSubpresion.Disenar(proyectoFlotOk, cargasFlotOk);
    AssertTol("QNetoMayoradoKNm2 (h=1.0m, no gobierna)", disenoSubNoAplica.QNetoMayoradoKNm2, qMayorado1Esperado, atol: 1e-6);
    totalAserciones++;
    if (!disenoSubNoAplica.Aplica && disenoSubNoAplica.MxCaraSuperior is null && disenoSubNoAplica.CortanteX is null && disenoSubNoAplica.Mensaje.Contains("no gobierna"))
        Console.WriteLine("  [OK  ] Aplica=false, todos los campos de diseno en null, Mensaje explica que el peso propio ya contrarresta la subpresion");
    else { fallos++; Console.WriteLine($"  [FAIL] se esperaba Aplica=false con campos null; Aplica={disenoSubNoAplica.Aplica}, Mensaje={disenoSubNoAplica.Mensaje}"); }

    Console.WriteLine("-- DisenoLosaFondoSubpresion.Disenar: SI Aplica (nivel freatico al maximo permitido, h=Hm=3.0m) -- recalculo independiente completo --");
    // Corrección 2026-08-31 (Opción A, PCA pág. 173): la flexión usa la luz EJE A EJE (a=B-em,
    // b=L-em), no la dimensión exterior; la huella para repartir el peso (q) sigue siendo EXTERIOR.
    var aFondoVerif = geoFlotFail.BAnchoM - geoFlotFail.EmEspesorMuroM; // a = B - em (eje a eje)
    var bFondoVerif = geoFlotFail.LLargoM - geoFlotFail.EmEspesorMuroM; // b = L - em (eje a eje)
    var rFondoVerif = bFondoVerif / aFondoVerif; // r = L/B (eje a eje), misma convencion que CalcularPlacaFondo
    var dAreal2 = cargasFlotFail.PttTotalKN / (geoFlotFail.BAnchoM * geoFlotFail.LLargoM); // huella EXTERIOR
    var qMayorado2Esperado = 1.4 * Flotabilidad.GammaAguaKNm3 * geoFlotFail.HmAlturaSueloSobreMuroM - 0.9 * dAreal2;
    var qServicio2Esperado = Flotabilidad.GammaAguaKNm3 * geoFlotFail.HmAlturaSueloSobreMuroM - dAreal2;
    totalAserciones++;
    if (qMayorado2Esperado > 0) Console.WriteLine($"  [OK  ] Caso construido deliberadamente gobernando localmente: q_neto_mayorado esperado={qMayorado2Esperado:0.###} kN/m2 > 0 (q_neto_servicio esperado={qServicio2Esperado:0.###} kN/m2)");
    else { fallos++; Console.WriteLine($"  [FAIL] se esperaba q_neto_mayorado>0 para este caso, actual {qMayorado2Esperado}"); }

    var disenoSubAplica = DisenoLosaFondoSubpresion.Disenar(proyectoFlotFail, cargasFlotFail);
    AssertTol("QNetoMayoradoKNm2 (h=3.0m) = 1.4xGammaAguaxh - 0.9xPttTotal/Area", disenoSubAplica.QNetoMayoradoKNm2, qMayorado2Esperado, atol: 1e-6);
    AssertTol("QNetoServicioKNm2 (h=3.0m) = GammaAguaxh - PttTotal/Area", disenoSubAplica.QNetoServicioKNm2, qServicio2Esperado, atol: 1e-6);
    totalAserciones++;
    if (disenoSubAplica.Aplica) Console.WriteLine("  [OK  ] Aplica=true");
    else { fallos++; Console.WriteLine("  [FAIL] se esperaba Aplica=true"); }

    // Recalculo INDEPENDIENTE: se llama PlacasRectangulares.Calcular directamente con el mismo r/q/a
    // (sin pasar por DisenoLosaFondoSubpresion) para confirmar que los campos mayorada/servicio
    // internos coinciden exactamente -- no solo autoconsistencia interna del propio modulo.
    var mayoradaVerif = PlacasRectangulares.Calcular(rFondoVerif, qMayorado2Esperado, aFondoVerif);
    var servicioVerif = PlacasRectangulares.Calcular(rFondoVerif, qServicio2Esperado, aFondoVerif);

    AssertTol("MxCaraSuperior.MuKNm == mayorada.MxPosGobernanteKNmM (campo 'positivo' = tension arriba bajo subpresion)",
        disenoSubAplica.MxCaraSuperior!.MuKNm, mayoradaVerif.MxPosGobernanteKNmM, atol: 1e-9);
    AssertTol("MxCaraInferior.MuKNm == mayorada.MxNegGobernanteKNmM (campo 'negativo' = tension abajo bajo subpresion)",
        disenoSubAplica.MxCaraInferior!.MuKNm, mayoradaVerif.MxNegGobernanteKNmM, atol: 1e-9);
    AssertTol("MyCaraSuperior.MuKNm == mayorada.MyPosGobernanteKNmM",
        disenoSubAplica.MyCaraSuperior!.MuKNm, mayoradaVerif.MyPosGobernanteKNmM, atol: 1e-9);
    AssertTol("MyCaraInferior.MuKNm == mayorada.MyNegGobernanteKNmM",
        disenoSubAplica.MyCaraInferior!.MuKNm, mayoradaVerif.MyNegGobernanteKNmM, atol: 1e-9);
    AssertTol("MxCaraSuperior.MsKNm == servicio.MxPosGobernanteKNmM (q_servicio>0 en este caso, control de fisuracion activo)",
        disenoSubAplica.MxCaraSuperior!.MsKNm!.Value, servicioVerif.MxPosGobernanteKNmM, atol: 1e-9);

    var dSuperiorVerif = RecubrimientosNSR10.CalcularDEfectivo(geoFlotFail.EfEspesorFondoM, RecubrimientosNSR10.RecubrimientoFormadoM, CatalogoBarras.DiametroPredeterminadoBarraMm);
    var dInferiorVerif = RecubrimientosNSR10.CalcularDEfectivo(geoFlotFail.EfEspesorFondoM, RecubrimientosNSR10.RecubrimientoContraSueloM, CatalogoBarras.DiametroPredeterminadoBarraMm);
    AssertTol("d cara superior (formada, 50mm) -- inversion respecto a DisenoPlacas.DisenarPlacaFondo, que usa 75mm para el campo 'positivo'",
        disenoSubAplica.MxCaraSuperior!.DEfectivoM, dSuperiorVerif, atol: 1e-9);
    AssertTol("d cara inferior (contra suelo, 75mm) -- inversion respecto a DisenoPlacas.DisenarPlacaFondo, que usa 50mm para el campo 'negativo'",
        disenoSubAplica.MxCaraInferior!.DEfectivoM, dInferiorVerif, atol: 1e-9);

    totalAserciones++;
    if (disenoSubAplica.CortanteX is not null && disenoSubAplica.CortanteY is not null
        && Math.Abs(disenoSubAplica.CortanteX.VuKN - mayoradaVerif.VxKNm) < 1e-9
        && Math.Abs(disenoSubAplica.CortanteY.VuKN - mayoradaVerif.VyKNm) < 1e-9)
        Console.WriteLine("  [OK  ] CortanteX/CortanteY.VuKN coinciden con mayorada.VxKNm/VyKNm");
    else { fallos++; Console.WriteLine("  [FAIL] CortanteX/CortanteY no coinciden con mayorada.VxKNm/VyKNm"); }

    Console.WriteLine("-- DisenoLosaFondoSubpresion.Disenar: r fuera de rango propaga la sugerencia de ajuste geometrico (mismo mecanismo que CalcularPlacaFondo) --");
    var geoRFueraDeRango = geoFlotFail with { BAnchoM = 20.0, LLargoM = 1.0 }; // r=L/B=0.05 << 0.5
    var proyectoRFueraDeRango = new ProyectoTanque(geoRFueraDeRango, matFlot);
    var cargasRFueraDeRango = CargasGravitacionales.Calcular(proyectoRFueraDeRango);
    totalAserciones++;
    try
    {
        DisenoLosaFondoSubpresion.Disenar(proyectoRFueraDeRango, cargasRFueraDeRango);
        fallos++;
        Console.WriteLine("  [FAIL] r fuera de rango no lanzo");
    }
    catch (ArgumentOutOfRangeException ex) when (ex.Message.Contains("Caso 10, placa de fondo bajo subpresión") && ex.Message.Contains("Ajuste la geometría"))
    {
        Console.WriteLine("  [OK  ] r=L/B fuera de [0.5,4] lanza ArgumentOutOfRangeException con la sugerencia de ajuste geometrico (SugerenciasGeometricas), mismo mecanismo que CalcularPlacaFondo");
    }
    catch (Exception ex) { fallos++; Console.WriteLine($"  [FAIL] se esperaba ArgumentOutOfRangeException con sugerencia de ajuste, se obtuvo {ex.GetType().Name}: {ex.Message}"); }

    Console.WriteLine("-- DisenoLosaFondoSubpresion.Envolver: subpresion NO Aplica -- el envolvente debe coincidir EXACTAMENTE con el gravitacional en las 4 caras y los 2 cortantes --");
    var disenoFondoFlotOk = DisenoPlacas.DisenarPlacaFondo(proyectoFlotOk, cargasFlotOk, cvKNm2: 0.0);
    var envolventeNoAplica = DisenoLosaFondoSubpresion.Envolver(disenoFondoFlotOk, disenoSubNoAplica);

    bool CaraCoincideConGravitacional(ResultadoEnvolventeCaraPlacaFondo e, ResultadoDisenoDireccionPlaca g) =>
        !e.GobernaSubpresion
        && Math.Abs(e.MuKNm - g.MuKNm) < 1e-9
        && Math.Abs(e.AsRequeridoMm2 - g.Flexion.AsRequeridoMm2) < 1e-9
        && Math.Abs(e.Rho - g.Flexion.Rho) < 1e-9
        && Math.Abs(e.DEfectivoM - g.DEfectivoM) < 1e-9;

    totalAserciones++;
    if (CaraCoincideConGravitacional(envolventeNoAplica.MxCaraInferior, disenoFondoFlotOk.MxPositivo)
        && CaraCoincideConGravitacional(envolventeNoAplica.MxCaraSuperior, disenoFondoFlotOk.MxNegativo)
        && CaraCoincideConGravitacional(envolventeNoAplica.MyCaraInferior, disenoFondoFlotOk.MyPositivo)
        && CaraCoincideConGravitacional(envolventeNoAplica.MyCaraSuperior, disenoFondoFlotOk.MyNegativo))
        Console.WriteLine("  [OK  ] Las 4 caras del envolvente == diseno gravitacional (Mu, As, Rho, d), GobernaSubpresion=false en todas");
    else { fallos++; Console.WriteLine("  [FAIL] alguna cara del envolvente no coincide con el gravitacional cuando Aplica=false"); }

    totalAserciones++;
    if (!envolventeNoAplica.CortanteX.GobernaSubpresion && !envolventeNoAplica.CortanteY.GobernaSubpresion
        && Math.Abs(envolventeNoAplica.CortanteX.VuKN - disenoFondoFlotOk.CortanteX.VuKN) < 1e-9
        && Math.Abs(envolventeNoAplica.CortanteY.VuKN - disenoFondoFlotOk.CortanteY.VuKN) < 1e-9)
        Console.WriteLine("  [OK  ] CortanteX/Y del envolvente == gravitacional, GobernaSubpresion=false");
    else { fallos++; Console.WriteLine("  [FAIL] CortanteX/Y del envolvente no coincide con el gravitacional cuando Aplica=false"); }

    Console.WriteLine("-- DisenoLosaFondoSubpresion.Envolver: subpresion SI Aplica -- cada cara debe elegir el candidato con mayor AsRequeridoMm2 --");
    var disenoFondoFlotFail = DisenoPlacas.DisenarPlacaFondo(proyectoFlotFail, cargasFlotFail, cvKNm2: 0.0);
    var envolventeAplica = DisenoLosaFondoSubpresion.Envolver(disenoFondoFlotFail, disenoSubAplica);

    bool CaraElaMayorAs(ResultadoEnvolventeCaraPlacaFondo e, ResultadoDisenoDireccionPlaca g, ResultadoDisenoCaraLosaFondoSubpresion s)
    {
        var gobernaSubEsperado = s.Flexion.AsRequeridoMm2 > g.Flexion.AsRequeridoMm2;
        if (e.GobernaSubpresion != gobernaSubEsperado) return false;
        var ganador = gobernaSubEsperado
            ? (s.MuKNm, s.Flexion.AsRequeridoMm2, s.Flexion.Rho, s.DEfectivoM)
            : (g.MuKNm, g.Flexion.AsRequeridoMm2, g.Flexion.Rho, g.DEfectivoM);
        return Math.Abs(e.MuKNm - ganador.Item1) < 1e-9 && Math.Abs(e.AsRequeridoMm2 - ganador.Item2) < 1e-9
            && Math.Abs(e.Rho - ganador.Item3) < 1e-9 && Math.Abs(e.DEfectivoM - ganador.Item4) < 1e-9;
    }

    totalAserciones++;
    if (CaraElaMayorAs(envolventeAplica.MxCaraInferior, disenoFondoFlotFail.MxPositivo, disenoSubAplica.MxCaraInferior!)
        && CaraElaMayorAs(envolventeAplica.MxCaraSuperior, disenoFondoFlotFail.MxNegativo, disenoSubAplica.MxCaraSuperior!)
        && CaraElaMayorAs(envolventeAplica.MyCaraInferior, disenoFondoFlotFail.MyPositivo, disenoSubAplica.MyCaraInferior!)
        && CaraElaMayorAs(envolventeAplica.MyCaraSuperior, disenoFondoFlotFail.MyNegativo, disenoSubAplica.MyCaraSuperior!))
        Console.WriteLine("  [OK  ] Las 4 caras del envolvente eligen el candidato (gravitacional/subpresion) con mayor AsRequeridoMm2, con GobernaSubpresion correcto");
    else { fallos++; Console.WriteLine("  [FAIL] alguna cara del envolvente no eligio correctamente el candidato con mayor As"); }

    // Cierre 2026-08-29 (observación del usuario): la envolvente debe propagar el DETALLADO (Ø/s)
    // del caso gobernante -- antes solo exponía Mu/As/ρ/d y el reporte no mostraba la separación.
    totalAserciones++;
    var envolventeConDetallado = new[] { envolventeAplica.MxCaraInferior, envolventeAplica.MxCaraSuperior, envolventeAplica.MyCaraInferior, envolventeAplica.MyCaraSuperior }
        .All(e => e.DiametroBarraMm is not null && e.SeparacionM is not null
            && Math.Abs(CatalogoBarras.AreaBarraMm2(e.DiametroBarraMm.Value) / e.SeparacionM.Value - e.AsRequeridoMm2) < 1e-6);
    if (envolventeConDetallado)
        Console.WriteLine("  [OK  ] Envolvente: las 4 caras exponen el detallado (Ø/s) del caso gobernante, con As == área(Ø)/s");
    else { fallos++; Console.WriteLine("  [FAIL] Envolvente: alguna cara no propaga el detallado (Ø/s) del caso gobernante"); }

    bool CortanteElMayorVu(ResultadoEnvolventeCortantePlacaFondo e, ResultadoDisenoCortantePlaca g, ResultadoDisenoCortanteLosaFondoSubpresion s)
    {
        var gobernaSubEsperado = s.VuKN > g.VuKN;
        if (e.GobernaSubpresion != gobernaSubEsperado) return false;
        var vuGanador = gobernaSubEsperado ? s.VuKN : g.VuKN;
        return Math.Abs(e.VuKN - vuGanador) < 1e-9;
    }

    totalAserciones++;
    if (CortanteElMayorVu(envolventeAplica.CortanteX, disenoFondoFlotFail.CortanteX, disenoSubAplica.CortanteX!)
        && CortanteElMayorVu(envolventeAplica.CortanteY, disenoFondoFlotFail.CortanteY, disenoSubAplica.CortanteY!))
        Console.WriteLine("  [OK  ] CortanteX/Y del envolvente eligen el candidato con mayor VuKN, con GobernaSubpresion correcto");
    else { fallos++; Console.WriteLine("  [FAIL] CortanteX/Y del envolvente no eligio correctamente el candidato con mayor Vu"); }

    // Backlog v2 (2026-08-27, a peticion del usuario tras revisar el reporte real): los diagramas de
    // momento por celda de la losa de fondo deben vivir bajo "DISEÑO FINAL DE LA LOSA DE FONDO" (el
    // envolvente), no bajo "PLACA DE FONDO" (solo el caso gravitacional) -- ver
    // DisenoLosaFondoSubpresion.EnvolverCampos, que envuelve los 4 campos crudos celda a celda por
    // MAGNITUD de momento mayorado (no por As -- ver el docstring del metodo para por que esa
    // aproximacion es deliberada y no afecta el diseno de refuerzo, que sigue usando Envolver).
    Console.WriteLine("-- DisenoLosaFondoSubpresion.EnvolverCampos: subpresion NO Aplica -- el diagrama envolvente debe coincidir EXACTAMENTE (celda a celda) con la magnitud del campo gravitacional --");
    var placaFondoFlotOk = PlacasRectangulares.CalcularPlacaFondo(proyectoFlotOk, cargasFlotOk, cvKNm2: 0.0);
    var camposNoAplica = DisenoLosaFondoSubpresion.EnvolverCampos(placaFondoFlotOk, disenoSubNoAplica);
    totalAserciones++;
    {
        var ok = true;
        for (var fila = 0; fila < 6 && ok; fila++)
            for (var col = 0; col < 6 && ok; col++)
            {
                if (Math.Abs(camposNoAplica.CampoMxCaraInferior[fila, col] - placaFondoFlotOk.CampoMxPos[fila, col]) > 1e-9) ok = false;
                if (Math.Abs(camposNoAplica.CampoMxCaraSuperior[fila, col] - (-placaFondoFlotOk.CampoMxNeg[fila, col])) > 1e-9) ok = false;
                if (Math.Abs(camposNoAplica.CampoMyCaraInferior[fila, col] - placaFondoFlotOk.CampoMyPos[fila, col]) > 1e-9) ok = false;
                if (Math.Abs(camposNoAplica.CampoMyCaraSuperior[fila, col] - (-placaFondoFlotOk.CampoMyNeg[fila, col])) > 1e-9) ok = false;
            }
        if (ok) Console.WriteLine("  [OK  ] Las 36 celdas de los 4 campos envolvente == magnitud del campo gravitacional (subpresion ausente en esta cara)");
        else { fallos++; Console.WriteLine("  [FAIL] alguna celda del diagrama envolvente no coincide con el gravitacional cuando Aplica=false"); }
    }

    Console.WriteLine("-- DisenoLosaFondoSubpresion.EnvolverCampos: subpresion SI Aplica -- cada celda es el maximo en magnitud entre ambos campos crudos (recalculo INDEPENDIENTE contra PlacasRectangulares.Calcular) --");
    var placaFondoFlotFail = PlacasRectangulares.CalcularPlacaFondo(proyectoFlotFail, cargasFlotFail, cvKNm2: 0.0);
    var camposAplica = DisenoLosaFondoSubpresion.EnvolverCampos(placaFondoFlotFail, disenoSubAplica);
    totalAserciones++;
    {
        var ok = true;
        var huboSubGanadoraEnAlgunaCelda = false;
        for (var fila = 0; fila < 6 && ok; fila++)
            for (var col = 0; col < 6 && ok; col++)
            {
                var esperadoInferior = Math.Max(placaFondoFlotFail.CampoMxPos[fila, col], -mayoradaVerif.CampoMxNeg[fila, col]);
                var esperadoSuperior = Math.Max(-placaFondoFlotFail.CampoMxNeg[fila, col], mayoradaVerif.CampoMxPos[fila, col]);
                if (Math.Abs(camposAplica.CampoMxCaraInferior[fila, col] - esperadoInferior) > 1e-9) ok = false;
                if (Math.Abs(camposAplica.CampoMxCaraSuperior[fila, col] - esperadoSuperior) > 1e-9) ok = false;
                if (-mayoradaVerif.CampoMxNeg[fila, col] > placaFondoFlotFail.CampoMxPos[fila, col]
                    || mayoradaVerif.CampoMxPos[fila, col] > -placaFondoFlotFail.CampoMxNeg[fila, col]) huboSubGanadoraEnAlgunaCelda = true;
            }
        if (ok) Console.WriteLine($"  [OK  ] Las 36 celdas de Mx (inferior+superior) == max(magnitud gravitacional, magnitud subpresion) recalculado independientemente (subpresion gana en al menos una celda: {huboSubGanadoraEnAlgunaCelda})");
        else { fallos++; Console.WriteLine("  [FAIL] alguna celda de Mx del diagrama envolvente no coincide con max(gravitacional, subpresion)"); }
    }
    totalAserciones++;
    {
        var ok = true;
        for (var fila = 0; fila < 6 && ok; fila++)
            for (var col = 0; col < 6 && ok; col++)
            {
                var esperadoInferior = Math.Max(placaFondoFlotFail.CampoMyPos[fila, col], -mayoradaVerif.CampoMyNeg[fila, col]);
                var esperadoSuperior = Math.Max(-placaFondoFlotFail.CampoMyNeg[fila, col], mayoradaVerif.CampoMyPos[fila, col]);
                if (Math.Abs(camposAplica.CampoMyCaraInferior[fila, col] - esperadoInferior) > 1e-9) ok = false;
                if (Math.Abs(camposAplica.CampoMyCaraSuperior[fila, col] - esperadoSuperior) > 1e-9) ok = false;
            }
        if (ok) Console.WriteLine("  [OK  ] Las 36 celdas de My (inferior+superior) == max(magnitud gravitacional, magnitud subpresion) recalculado independientemente");
        else { fallos++; Console.WriteLine("  [FAIL] alguna celda de My del diagrama envolvente no coincide con max(gravitacional, subpresion)"); }
    }
    totalAserciones++;
    {
        // Consistencia cruzada: el maximo de cada campo envolvente completo (el diagrama) nunca puede
        // ser MENOR que el Mu de la cara gobernante ya calculado por Envolver (que compara As, no Mu) --
        // matematicamente max_ij(max(a,b)) = max(max_ij(a), max_ij(b)) >= cualquiera de los dos, incluyendo
        // el candidato que Envolver eligio por As aunque no tenga el Mu mayor.
        double MaxCelda(double[,] campo) { var m = 0.0; for (var i = 0; i < 6; i++) for (var j = 0; j < 6; j++) m = Math.Max(m, campo[i, j]); return m; }
        var okCruce = MaxCelda(camposAplica.CampoMxCaraInferior) + 1e-6 >= envolventeAplica.MxCaraInferior.MuKNm
            && MaxCelda(camposAplica.CampoMxCaraSuperior) + 1e-6 >= envolventeAplica.MxCaraSuperior.MuKNm
            && MaxCelda(camposAplica.CampoMyCaraInferior) + 1e-6 >= envolventeAplica.MyCaraInferior.MuKNm
            && MaxCelda(camposAplica.CampoMyCaraSuperior) + 1e-6 >= envolventeAplica.MyCaraSuperior.MuKNm;
        if (okCruce) Console.WriteLine("  [OK  ] El maximo de cada campo envolvente (diagrama) es >= el Mu de la cara gobernante ya calculado por Envolver (consistencia cruzada diagrama/valor gobernante de refuerzo)");
        else { fallos++; Console.WriteLine("  [FAIL] el diagrama envolvente subestima el Mu de la cara gobernante ya calculado por Envolver"); }
    }
}

Console.WriteLine();
Console.WriteLine("=== Espesores minimos de muro NSR-10 C.23-C.14.6 (backlog v2, item 7) ===");
{
    var geoAlta = new Geometria(BAnchoM: 6.0, LLargoM: 8.0, HtAlturaM: 4.5, ConTapa: true, EmEspesorMuroM: 0.3, EfEspesorFondoM: 0.35, EtEspesorTapaM: 0.2, HLAlturaLiquidoM: 4.0, HmAlturaSueloSobreMuroM: 3.0, WextSobrecargaKNm2: 0.0);

    // Ht=4.5m > 3.0m -> activa C.23-C.14.6.2 (contacto con liquido, altura>3m): minimo 300mm.
    var okAlta = EspesoresMinimos.VerificarMuro(geoAlta);
    AssertTol("Ht=4.5m>3m: EspesorMinimoAplicableM = 300mm (C.23-C.14.6.2)", okAlta.EspesorMinimoAplicableM, 0.300, atol: 1e-9);
    totalAserciones++;
    if (okAlta.Cumple && okAlta.DeficitM == 0.0) Console.WriteLine("  [OK  ] Em=0.30m == minimo exacto (300mm): Cumple=true, DeficitM=0");
    else { fallos++; Console.WriteLine($"  [FAIL] se esperaba Cumple=true/DeficitM=0, actual Cumple={okAlta.Cumple}, DeficitM={okAlta.DeficitM}"); }

    var faltaAlta = EspesoresMinimos.VerificarMuro(geoAlta with { EmEspesorMuroM = 0.25 });
    totalAserciones++;
    if (!faltaAlta.Cumple) Console.WriteLine($"  [OK  ] Em=0.25m < 300mm: Cumple=false");
    else { fallos++; Console.WriteLine("  [FAIL] Em=0.25m<300mm deberia incumplir C.23-C.14.6.2"); }
    AssertTol("Em=0.25m (Ht>3m): DeficitM = 300mm-250mm = 0.05m", faltaAlta.DeficitM, 0.05, atol: 1e-9);

    // Ht=2.5m <= 3.0m -> NO activa C.23-C.14.6.2: solo rige el piso absoluto de C.23-C.14.6.1 (150mm).
    var geoBaja = geoAlta with { HtAlturaM = 2.5, HLAlturaLiquidoM = 2.0, HmAlturaSueloSobreMuroM = 1.5, EmEspesorMuroM = 0.1 };
    var faltaBaja = EspesoresMinimos.VerificarMuro(geoBaja);
    AssertTol("Ht=2.5m<=3m: EspesorMinimoAplicableM = 150mm (C.23-C.14.6.1, piso absoluto)", faltaBaja.EspesorMinimoAplicableM, 0.150, atol: 1e-9);
    totalAserciones++;
    if (!faltaBaja.Cumple) Console.WriteLine("  [OK  ] Em=0.10m < 150mm: Cumple=false");
    else { fallos++; Console.WriteLine("  [FAIL] Em=0.10m<150mm deberia incumplir C.23-C.14.6.1"); }
    AssertTol("Em=0.10m (Ht<=3m): DeficitM = 150mm-100mm = 0.05m", faltaBaja.DeficitM, 0.05, atol: 1e-9);

    var okBaja = EspesoresMinimos.VerificarMuro(geoBaja with { EmEspesorMuroM = 0.2 });
    totalAserciones++;
    if (okBaja.Cumple && okBaja.DeficitM == 0.0) Console.WriteLine("  [OK  ] Em=0.20m >= 150mm (Ht<=3m): Cumple=true, DeficitM=0");
    else { fallos++; Console.WriteLine($"  [FAIL] se esperaba Cumple=true/DeficitM=0, actual Cumple={okBaja.Cumple}, DeficitM={okBaja.DeficitM}"); }
}

Console.WriteLine();
Console.WriteLine("=== Sugerencia de ajuste geometrico cuando r cae fuera del rango tabulado PCA (backlog v2, hallazgo de UX 2026-08-26, quinta continuacion) ===");
{
    void AssertLanzaSugerencia(string nombre, Action accion, params string[] fragmentosEsperados)
    {
        totalAserciones++;
        try
        {
            accion();
            fallos++;
            Console.WriteLine($"  [FAIL] {nombre}: no lanzo ArgumentOutOfRangeException");
        }
        catch (ArgumentOutOfRangeException ex)
        {
            var faltantes = fragmentosEsperados.Where(f => !ex.Message.Contains(f)).ToList();
            if (faltantes.Count == 0)
                Console.WriteLine($"  [OK  ] {nombre}: mensaje incluye la sugerencia de ajuste esperada");
            else
            {
                fallos++;
                Console.WriteLine($"  [FAIL] {nombre}: mensaje no incluye [{string.Join(", ", faltantes)}]. Mensaje real: {ex.Message}");
            }
        }
    }

    // Reproduccion EXACTA de la geometria reportada por el usuario (2026-08-26, quinta continuacion):
    // B=2m, L=5m, tanque superficial -- r=B/L=0.375 (eje a eje: b=B-em=1.8m, a=L-em=4.8m) para la
    // placa de cubierta (Caso 10), fuera de [0.5,4]. Antes de esta sesion la aplicacion solo
    // mostraba "r=0.375 esta fuera del rango tabulado [0.5,4]...", sin decir al usuario que
    // dimension ajustar.
    var geoUsuario = new Geometria(
        BAnchoM: 2.0, LLargoM: 5.0, HtAlturaM: 2.0, ConTapa: true,
        EmEspesorMuroM: 0.2, EfEspesorFondoM: 0.25, EtEspesorTapaM: 0.15,
        HLAlturaLiquidoM: 1.8, HmAlturaSueloSobreMuroM: 0.0, WextSobrecargaKNm2: 0.0,
        Tipo: TipoTanque.Superficial);
    var matUsuario = new Materiales(FcMPa: 21, FyMPa: 420, GammaConcretoKNm3: 24, GammaLiquidoKNm3: 9.81, GammaSueloKNm3: 18, PhiGradosAnguloFriccionSuelo: 30);
    var proyectoUsuario = new ProyectoTanque(geoUsuario, matUsuario);
    var cargasUsuario = CargasGravitacionales.Calcular(proyectoUsuario);

    AssertLanzaSugerencia(
        "Reproduccion exacta del reporte de usuario -- placa de cubierta B=2m,L=5m (r=B/L=0.375<0.5, eje a eje)",
        () => PlacasRectangulares.CalcularPlacaCubierta(proyectoUsuario, cargasUsuario, 0.0, 0.0),
        "r=B/L=0.375", "[0.5, 4]", "Caso 10, placa de cubierta",
        "L (largo del tanque)=4.8m", "B (ancho del tanque) debe estar entre 2.4m y 19.2m",
        "B (ancho del tanque)=1.8m", "L (largo del tanque) debe estar entre 0.45m y 3.6m");

    // Caso espejo (r>4.0) sobre la misma placa de cubierta -- ejercita la otra rama del despeje
    // algebraico (numMax/denMax en vez de numMin/denMin).
    var geoCubiertaAncha = geoUsuario with { BAnchoM = 20.0, LLargoM = 1.0, EmEspesorMuroM = 0.1 };
    var cargasCubiertaAncha = CargasGravitacionales.Calcular(new ProyectoTanque(geoCubiertaAncha, matUsuario));
    AssertLanzaSugerencia(
        "Placa de cubierta B=20m,L=1m (r=B/L=22.111>4, eje a eje)",
        () => PlacasRectangulares.CalcularPlacaCubierta(new ProyectoTanque(geoCubiertaAncha, matUsuario), cargasCubiertaAncha, 0.0, 0.0),
        "r=B/L=22.111", "[0.5, 4]", "Caso 10, placa de cubierta",
        "B (ancho del tanque) debe estar entre 0.45m y 3.6m",
        "L (largo del tanque) debe estar entre 4.98m y 39.8m");

    // Placa de fondo: convencion invertida (r=L/B, ver PlacasRectangulares.CalcularPlacaFondo) --
    // B=10m, L=1m, em=0.1 -> b=L-em=0.9m, a=B-em=9.9m -> r=L/B=0.091<0.5 (eje a eje).
    var geoFondoAngosta = geoUsuario with { BAnchoM = 10.0, LLargoM = 1.0, EmEspesorMuroM = 0.1, ConTapa = false, EtEspesorTapaM = 0.0 };
    var proyectoFondoAngosta = new ProyectoTanque(geoFondoAngosta, matUsuario);
    var cargasFondoAngosta = CargasGravitacionales.Calcular(proyectoFondoAngosta);
    AssertLanzaSugerencia(
        "Placa de fondo B=10m,L=1m (r=L/B=0.091<0.5, eje a eje)",
        () => PlacasRectangulares.CalcularPlacaFondo(proyectoFondoAngosta, cargasFondoAngosta, 0.0),
        "r=L/B=0.091", "[0.5, 4]", "Caso 10, placa de fondo",
        "L (largo del tanque) debe estar entre 4.95m y 39.6m",
        "B (ancho del tanque) debe estar entre 0.23m y 1.8m");

    // Muro longitudinal, condicion INTERIOR: r=L/HL con L EJE A EJE (L-em). L=1m, em=0.2m,
    // HL=5m -> b=L-em=0.8m -> r=L/HL=0.16<0.5.
    var geoMuroAngosto = geoUsuario with { BAnchoM = 3.0, LLargoM = 1.0, HtAlturaM = 5.5, HLAlturaLiquidoM = 5.0, HmAlturaSueloSobreMuroM = 0.0, ConTapa = false, EtEspesorTapaM = 0.0, Tipo = TipoTanque.Superficial };
    var proyectoMuroAngosto = new ProyectoTanque(geoMuroAngosto, matUsuario);
    var presionesMuroAngosto = PresionesLaterales.Calcular(proyectoMuroAngosto);
    AssertLanzaSugerencia(
        "Muro longitudinal, condicion interior, L=1m,HL=5m (r=L/HL=0.16<0.5, eje a eje)",
        () => MurosRectangulares.CalcularMuroLongitudinal(proyectoMuroAngosto, presionesMuroAngosto),
        "r=L/HL=0.16", "[0.5, 4]", "Caso 3, muro longitudinal (condición interior)",
        "L (largo del tanque) debe estar entre 2.5m y 20m",
        "HL (altura de la lámina de líquido) debe estar entre 0.2m y 1.6m");

    // Muro transversal, condicion EXTERIOR (con suelo, Hm>0): r=B/Hm con B EJE A EJE (B-em).
    // B=6m, em=0.2m -> b=5.8m; HL=3m (r_interior=5.8/3=1.933, EN rango -- para que la excepcion se
    // origine en la condicion EXTERIOR, no en la interior, que se evalua primero), Hm=1m ->
    // r_exterior=B/Hm=5.8>4.
    var geoMuroExtAncho = geoUsuario with { BAnchoM = 6.0, LLargoM = 5.0, HtAlturaM = 3.5, HLAlturaLiquidoM = 3.0, HmAlturaSueloSobreMuroM = 1.0, EmEspesorMuroM = 0.2, ConTapa = false, EtEspesorTapaM = 0.0, Tipo = TipoTanque.EnterradoSinNivelFreatico };
    var proyectoMuroExtAncho = new ProyectoTanque(geoMuroExtAncho, matUsuario);
    var presionesMuroExtAncho = PresionesLaterales.Calcular(proyectoMuroExtAncho);
    AssertLanzaSugerencia(
        "Muro transversal, condicion exterior, B=6m,Hm=1m (r_interior=1.933 en rango; r_exterior=B/Hm=5.8>4, eje a eje)",
        () => MurosRectangulares.CalcularMuroTransversal(proyectoMuroExtAncho, presionesMuroExtAncho),
        "r=B/Hm=5.8", "[0.5, 4]", "Caso 3, muro transversal (condición exterior)",
        "B (ancho del tanque) debe estar entre 0.5m y 4m",
        "Hm (altura de suelo sobre el muro) debe estar entre 1.45m y 11.6m");

    // Confirma que DisenoPlacas/DisenoMuros (que llaman a las envolturas de arriba primero)
    // propagan la misma excepcion enriquecida, sin envolverla ni perder el mensaje -- mismo patron
    // ya verificado para el caso ConTapa=false mas arriba.
    totalAserciones++;
    try
    {
        DisenoPlacas.DisenarPlacaCubierta(proyectoUsuario, cargasUsuario, 0.0, 0.0);
        fallos++;
        Console.WriteLine("  [FAIL] DisenoPlacas.DisenarPlacaCubierta sobre geometria de usuario no lanzo");
    }
    catch (ArgumentOutOfRangeException ex) when (ex.Message.Contains("Caso 10, placa de cubierta"))
    {
        Console.WriteLine("  [OK  ] DisenoPlacas.DisenarPlacaCubierta propaga la misma sugerencia de PlacasRectangulares.CalcularPlacaCubierta, sin envolverla");
    }
}

Console.WriteLine();
Console.WriteLine("=== Modulo 12 (backlog v3, Fase A): CalculadorTanque + Tanque.Reportes.GenerarReporte ===");
{
    // Hallazgos H1/H3 del informe de auditoria externa del usuario (2026-08-28): la orquestacion
    // completa del calculo y el formateador de reportes vivian en Tanque.App (H1: en el
    // code-behind de MainWindow, sin ninguna referencia desde Tanque.Core; H3: ReporteResultados
    // acoplado a la UI aunque no dependia de Avalonia). Esta seccion verifica que la extraccion a
    // Tanque.Core.Modulos.CalculadorTanque + la nueva biblioteca Tanque.Reportes fue MECANICA -- es
    // decir, que produce EXACTAMENTE el mismo texto de reporte, caracter por caracter (salvo la
    // linea "Generado:", que depende de DateTime.Now), que la orquestacion manual equivalente que
    // antes vivia en MainWindow.EjecutarCalculo. Escenario: tanque enterrado con nivel freatico, con
    // tapa, con sismo y con diagramas -- el camino que ejercita las 19 secciones del reporte (todas
    // las ramas "incluido"/"aplica", no las de omision).
    var geoF12 = new Geometria(
        BAnchoM: 4.5, LLargoM: 6.0, HtAlturaM: 3.5, ConTapa: true,
        EmEspesorMuroM: 0.25, EfEspesorFondoM: 0.20, EtEspesorTapaM: 0.15,
        HLAlturaLiquidoM: 3.0, HmAlturaSueloSobreMuroM: 3.0, WextSobrecargaKNm2: 5.0,
        Tipo: TipoTanque.EnterradoConNivelFreatico, AlturaNivelFreaticoM: 2.0);
    var matF12 = new Materiales(
        FcMPa: 28, FyMPa: 420, GammaSueloKNm3: 16, GammaConcretoKNm3: 24, GammaLiquidoKNm3: 9.81,
        PhiGradosAnguloFriccionSuelo: 30, GammaSueloSaturadoKNm3: 18);
    var proyectoF12 = new ProyectoTanque(geoF12, matF12);
    proyectoF12.Validar();

    const double cvCubiertaF12 = 1.0, cgCubiertaF12 = 0.5, cvFondoF12 = 2.0, diametroBarraF12 = CatalogoBarras.DiametroPredeterminadoBarraMm;
    var espectroF12 = new ParametrosEspectroDiseno(Aa: 0.2, Av: 0.2, Fa: 1.3, Fv: 2.0, I: 1.0,
        CondicionBase: CondicionBaseMuro.Rigida, CondicionAnclaje: CondicionAnclajeBase.ArticuladaEmpotrada);
    var sueloF12 = new ParametrosSueloDinamico(KhCoeficienteSismicoHorizontal: 0.2, KvCoeficienteSismicoVertical: 0.0,
        DeltaGradosFriccionSueloMuro: 0.0, IGradosInclinacionRelleno: 0.0, BetaGradosInclinacionMuro: 90.0);

    var parametrosF12 = new ParametrosCalculoTanque(
        CvCubiertaKNm2: cvCubiertaF12, CgCubiertaKNm2: cgCubiertaF12, CvFondoKNm2: cvFondoF12,
        DiametrosBarra: new DiametrosBarraCalculo(diametroBarraF12, diametroBarraF12, diametroBarraF12, diametroBarraF12),
        MetodoInterpolacion: MetodoInterpolacion.Interpolar,
        IncluirDiagramas: true, Sismo: new ParametrosSismoCalculo(espectroF12, sueloF12));

    totalAserciones++;
    ResultadoCalculoTanque resultadoF12;
    string reporteNuevo;
    try
    {
        resultadoF12 = CalculadorTanque.Calcular(proyectoF12, parametrosF12);
        reporteNuevo = ReporteResultados.GenerarReporte(resultadoF12);
        Console.WriteLine("  [OK  ] CalculadorTanque.Calcular + ReporteResultados.GenerarReporte no lanzan sobre el escenario completo (tapa+sismo+nivel freatico+diagramas)");
    }
    catch (Exception ex)
    {
        fallos++;
        Console.WriteLine($"  [FAIL] CalculadorTanque.Calcular/GenerarReporte lanzaron excepcion inesperada: {ex}");
        goto FinModulo12;
    }

    // Replica, llamada por llamada, la orquestacion que antes vivia en
    // Tanque.App.MainWindow.EjecutarCalculo (tal como existia el 2026-08-28 antes de esta
    // extraccion) -- mismos modulos, mismos argumentos, mismo orden -- para producir el reporte de
    // forma independiente y compararlo con el de CalculadorTanque+GenerarReporte.
    {
        var cargasRef = CargasGravitacionales.Calcular(proyectoF12, cvCubiertaF12, cgCubiertaF12);
        var presionesRef = PresionesLaterales.Calcular(proyectoF12);
        var sismoHidroRef = FuerzaSismicaHidrodinamica.Calcular(proyectoF12, espectroF12);
        var sismoSueloRef = FuerzaDinamicaSuelo.Calcular(proyectoF12, sueloF12);
        var placaCubiertaRef = PlacasRectangulares.CalcularPlacaCubierta(proyectoF12, cargasRef, cvCubiertaF12, cgCubiertaF12, MetodoInterpolacion.Interpolar);
        var disenoCubiertaRef = DisenoPlacas.DisenarPlacaCubierta(proyectoF12, cargasRef, cvCubiertaF12, cgCubiertaF12, diametroBarraF12);
        var placaFondoRef = PlacasRectangulares.CalcularPlacaFondo(proyectoF12, cargasRef, cvFondoF12, MetodoInterpolacion.Interpolar);
        var disenoFondoRef = DisenoPlacas.DisenarPlacaFondo(proyectoF12, cargasRef, cvFondoF12, diametroBarraF12);
        var muroLongEstaticoRef = MurosRectangulares.CalcularMuroLongitudinal(proyectoF12, presionesRef, MetodoInterpolacion.Interpolar);
        var muroTransEstaticoRef = MurosRectangulares.CalcularMuroTransversal(proyectoF12, presionesRef, MetodoInterpolacion.Interpolar);
        var disenoMuroLongRef = DisenoMuros.DisenarMuroLongitudinal(proyectoF12, presionesRef, sismoHidroRef, sismoSueloRef, diametroBarraF12);
        var disenoMuroTransRef = DisenoMuros.DisenarMuroTransversal(proyectoF12, presionesRef, sismoHidroRef, sismoSueloRef, diametroBarraF12);
        var espesorMinimoRef = EspesoresMinimos.VerificarMuro(geoF12);
        var flotabilidadRef = Flotabilidad.Verificar(proyectoF12, cargasRef);
        var sobreanchoRef = !flotabilidadRef.Cumple && matF12.GammaSueloSaturadoKNm3 is not null
            ? Flotabilidad.CalcularSobreancho(proyectoF12, cargasRef, flotabilidadRef) : null;
        var losaSubpresionRef = DisenoLosaFondoSubpresion.Disenar(proyectoF12, cargasRef, MetodoInterpolacion.Interpolar, diametroBarraF12);
        var envolventeFondoRef = DisenoLosaFondoSubpresion.Envolver(disenoFondoRef, losaSubpresionRef);
        var camposEnvolventeFondoRef = DisenoLosaFondoSubpresion.EnvolverCampos(placaFondoRef, losaSubpresionRef);

        var sbRef = new System.Text.StringBuilder();
        sbRef.AppendLine("TANQUE.CORE -- REPORTE DE CÁLCULO");
        sbRef.AppendLine($"Generado: {DateTime.Now:yyyy-MM-dd HH:mm}");
        sbRef.AppendLine($"Desarrollador: {IdentidadDesarrollador.Nombre}");
        sbRef.AppendLine($"Afiliación: {IdentidadDesarrollador.Afiliacion}");
        sbRef.AppendLine($"Contacto: {IdentidadDesarrollador.Contacto} · ORCID: {IdentidadDesarrollador.Orcid}");
        sbRef.AppendLine(new string('=', 78));
        sbRef.AppendLine();
        sbRef.Append(ReporteResultados.CargasGravitacionales(cargasRef));
        sbRef.Append(ReporteResultados.PresionesLaterales(presionesRef));
        sbRef.Append(ReporteResultados.FuerzaSismicaHidrodinamica(sismoHidroRef));
        sbRef.Append(ReporteResultados.FuerzaDinamicaSuelo(sismoSueloRef));
        sbRef.Append(ReporteResultados.Placa("PLACA DE CUBIERTA (Formulario 7)", placaCubiertaRef, disenoCubiertaRef, true));
        sbRef.Append(ReporteResultados.Placa("PLACA DE FONDO (Formularios 10-11)", placaFondoRef, disenoFondoRef, false));
        sbRef.Append(ReporteResultados.Muro("MURO LONGITUDINAL (Formulario 8, span L)", muroLongEstaticoRef, disenoMuroLongRef, true));
        sbRef.Append(ReporteResultados.Muro("MURO TRANSVERSAL (Formulario 9, span B)", muroTransEstaticoRef, disenoMuroTransRef, true));
        sbRef.Append(ReporteResultados.EspesorMinimoMuro(espesorMinimoRef));
        sbRef.Append(ReporteResultados.Flotabilidad(flotabilidadRef, sobreanchoRef));
        sbRef.Append(ReporteResultados.LosaFondoSubpresion(losaSubpresionRef, true));
        sbRef.Append(ReporteResultados.EnvolventePlacaFondo(envolventeFondoRef, camposEnvolventeFondoRef, true));
        var reporteReferencia = sbRef.ToString();

        // Normaliza la unica linea que puede diferir entre las dos llamadas (el reloj avanza entre
        // una y otra) antes de comparar caracter por caracter.
        string QuitarLineaGenerado(string s) =>
            string.Join('\n', s.Replace("\r\n", "\n").Split('\n').Where(l => !l.StartsWith("Generado: ")));

        totalAserciones++;
        var nuevoNorm = QuitarLineaGenerado(reporteNuevo);
        var refNorm = QuitarLineaGenerado(reporteReferencia);
        if (nuevoNorm == refNorm)
        {
            Console.WriteLine($"  [OK  ] GenerarReporte(CalculadorTanque.Calcular(...)) produce EXACTAMENTE el mismo texto ({nuevoNorm.Length} caracteres, excluyendo la linea \"Generado:\") que la orquestacion manual equivalente a la que antes vivia en MainWindow.EjecutarCalculo");
        }
        else
        {
            fallos++;
            var lineasNuevo = nuevoNorm.Split('\n');
            var lineasRef = refNorm.Split('\n');
            var primeraDif = Enumerable.Range(0, Math.Min(lineasNuevo.Length, lineasRef.Length))
                .FirstOrDefault(i => lineasNuevo[i] != lineasRef[i], -1);
            Console.WriteLine($"  [FAIL] El reporte de GenerarReporte difiere del de la orquestacion manual (long. nuevo={nuevoNorm.Length}, ref={refNorm.Length}, primera linea distinta={primeraDif})");
            if (primeraDif >= 0 && primeraDif < lineasNuevo.Length && primeraDif < lineasRef.Length)
            {
                Console.WriteLine($"    nuevo: {lineasNuevo[primeraDif]}");
                Console.WriteLine($"    ref  : {lineasRef[primeraDif]}");
            }
        }
    }

    // Backlog v3, Fase B (2026-08-28, decidida por el usuario: secciones agrupadas Expander).
    // GenerarSeccionesAgrupadas debe reconstruir EXACTAMENTE el mismo texto que GenerarReporte al
    // concatenar sus secciones en orden -- confirma que particionar el reporte por grupos (para
    // mostrarlo en Expander plegables en la interfaz) no pierde ni duplica ningún contenido del
    // mismo reporte ya verificado arriba, sobre el mismo escenario completo (tapa+sismo+nivel
    // freatico+diagramas).
    {
        totalAserciones++;
        var secciones = ReporteResultados.GenerarSeccionesAgrupadas(resultadoF12);
        var reconstruido = string.Concat(secciones.Select(s => s.Texto));
        string QuitarLineaGeneradoFaseB(string s) =>
            string.Join('\n', s.Replace("\r\n", "\n").Split('\n').Where(l => !l.StartsWith("Generado: ")));
        var reconstruidoNorm = QuitarLineaGeneradoFaseB(reconstruido);
        var reporteNuevoNorm = QuitarLineaGeneradoFaseB(reporteNuevo);
        if (reconstruidoNorm == reporteNuevoNorm)
        {
            var nombresGrupos = string.Join(", ", secciones.Select(s => s.Grupo));
            Console.WriteLine($"  [OK  ] GenerarSeccionesAgrupadas reconstruye EXACTAMENTE el mismo texto que GenerarReporte ({secciones.Count} grupos: {nombresGrupos}) -- ningun contenido perdido ni duplicado al particionar por grupos (Fase B)");
        }
        else
        {
            fallos++;
            var lineasA = reconstruidoNorm.Split('\n');
            var lineasB = reporteNuevoNorm.Split('\n');
            var primeraDif = Enumerable.Range(0, Math.Min(lineasA.Length, lineasB.Length))
                .FirstOrDefault(i => lineasA[i] != lineasB[i], -1);
            Console.WriteLine($"  [FAIL] GenerarSeccionesAgrupadas NO reconstruye el mismo texto que GenerarReporte (long. reconstruido={reconstruidoNorm.Length}, flat={reporteNuevoNorm.Length}, primera linea distinta={primeraDif})");
        }

        // Escenario adicional: sismo omitido -- confirma que la seccion "Sismo" lleva el aviso de
        // omision UNA sola vez y que "Dinamico" simplemente no aparece (no se duplica el aviso ni se
        // inventa contenido para "Dinamico" cuando no hay nada que mostrar ahi).
        totalAserciones++;
        var parametrosSinSismoF12 = parametrosF12 with { Sismo = null };
        var resultadoSinSismoF12 = CalculadorTanque.Calcular(proyectoF12, parametrosSinSismoF12);
        var seccionesSinSismo = ReporteResultados.GenerarSeccionesAgrupadas(resultadoSinSismoF12);
        var tieneSismoConAviso = seccionesSinSismo.Any(s => s.Grupo == "Sismo" && s.Texto.Contains("ANÁLISIS SÍSMICO -- OMITIDO"));
        var noHayDinamico = !seccionesSinSismo.Any(s => s.Grupo == "Dinámico");
        var reporteFlatSinSismo = ReporteResultados.GenerarReporte(resultadoSinSismoF12);
        var reconstruidoSinSismo = QuitarLineaGeneradoFaseB(string.Concat(seccionesSinSismo.Select(s => s.Texto)));
        var flatSinSismoNorm = QuitarLineaGeneradoFaseB(reporteFlatSinSismo);
        if (tieneSismoConAviso && noHayDinamico && reconstruidoSinSismo == flatSinSismoNorm)
        {
            Console.WriteLine("  [OK  ] GenerarSeccionesAgrupadas con sismo OMITIDO: el aviso aparece una sola vez bajo \"Sismo\", \"Dinamico\" no se genera, y la reconstruccion sigue coincidiendo exactamente con GenerarReporte");
        }
        else
        {
            fallos++;
            Console.WriteLine($"  [FAIL] GenerarSeccionesAgrupadas con sismo omitido: avisoSismo={tieneSismoConAviso}, sinDinamico={noHayDinamico}, textoCoincide={reconstruidoSinSismo == flatSinSismoNorm}");
        }
    }

    // Fase 1 del frente de interfaz (2026-08-30) -- veredicto global CUMPLE/NO CUMPLE
    // (Tanque.Core/Modulos/Veredicto.cs). Verifica que el veredicto (1) sea la conjunción exacta
    // de sus ítems, (2) se cruce ítem a ítem contra las señales normativas CRUDAS del resultado
    // (sin inventar ninguna), y (3) detecte un fallo determinista (espesor de muro bajo el mínimo).
    {
        Console.WriteLine("  --- Veredicto global (Fase 1 del frente de interfaz) ---");

        var res = resultadoF12;
        var v = Veredicto.Calcular(res);

        // (a) Invariante fundamental: Cumple == conjunción de todos los ítems.
        totalAserciones++;
        var invariante = v.Cumple == v.Items.All(i => i.Cumple);
        if (invariante)
            Console.WriteLine($"  [OK  ] Veredicto.Cumple == Items.All(Cumple) (conjunción) -- {v.Items.Count} ítems");
        else { fallos++; Console.WriteLine("  [FAIL] Veredicto.Cumple no es la conjunción de sus ítems"); }

        // (b) Presencia de los ítems obligatorios (tapa + nivel freático: espesor, cubierta×3,
        //     fondo×2, muro long/trans×2, flotabilidad = 11 fijos).
        bool Tiene(string elem, string concepto) => v.Items.Any(i => i.Elemento == elem && i.Concepto == concepto);
        var obligatorios = new (string, string)[]
        {
            ("Muros", "Espesor mínimo"),
            ("Cubierta", "Detallado Ø/s"), ("Cubierta", "Fisuración (fs ≤ fs,adm)"), ("Cubierta", "Cortante (Vu ≤ Vc)"),
            ("Fondo", "Detallado Ø/s"), ("Fondo", "Cortante (Vu ≤ Vc)"),
            ("Muro longitudinal", "Detallado Ø/s"), ("Muro longitudinal", "Cortante (Vu ≤ Vc)"),
            ("Muro transversal", "Detallado Ø/s"), ("Muro transversal", "Cortante (Vu ≤ Vc)"),
            ("Estructura", "Flotabilidad (FS ≥ 1.25)"),
        };
        var faltan = obligatorios.Where(e => !Tiene(e.Item1, e.Item2)).ToList();
        totalAserciones++;
        if (faltan.Count == 0)
            Console.WriteLine($"  [OK  ] Los 11 ítems obligatorios están presentes ({v.Items.Count} ítems en total)");
        else { fallos++; Console.WriteLine($"  [FAIL] Faltan ítems obligatorios: {string.Join("; ", faltan.Select(e => e.Item1 + " · " + e.Item2))}"); }

        // (c) Cruzado ítem a ítem contra las señales crudas del resultado (independiente del código
        //     de Veredicto). El switch lanza si aparece un ítem no reconocido (cubre exhaustivamente).
        var cubDirs = new[] { res.DisenoCubierta!.MxPositivo, res.DisenoCubierta.MxNegativo, res.DisenoCubierta.MyPositivo, res.DisenoCubierta.MyNegativo };
        var envCaras = new[] { res.EnvolventeFondo!.MxCaraInferior, res.EnvolventeFondo.MxCaraSuperior, res.EnvolventeFondo.MyCaraInferior, res.EnvolventeFondo.MyCaraSuperior };
        var mlDirs = new[] { res.DisenoMuroLongitudinal.VerticalPositivo, res.DisenoMuroLongitudinal.VerticalNegativo, res.DisenoMuroLongitudinal.HorizontalPositivo, res.DisenoMuroLongitudinal.HorizontalNegativo };
        var mtDirs = new[] { res.DisenoMuroTransversal.VerticalPositivo, res.DisenoMuroTransversal.VerticalNegativo, res.DisenoMuroTransversal.HorizontalPositivo, res.DisenoMuroTransversal.HorizontalNegativo };

        bool MuroDet(ResultadoDisenoDireccionMuro[] dirs) => !dirs.Any(d => d.Flexion.DetalladoInsuficiente);
        bool MuroFis(ResultadoDisenoDireccionMuro[] dirs) => dirs.Where(d => d.Fisuracion is not null).All(d => d.Fisuracion!.Cumple);
        bool MuroCort(ResultadoDisenoMuro m) => new[] { m.CortanteFondo, m.CortanteLateralMaximo, m.CortanteLateralMedio }.All(c => c.Cortante.Cumple);

        bool EsperadoDe(string elem, string concepto) => (elem, concepto) switch
        {
            ("Muros", "Espesor mínimo") => res.EspesorMinimoMuro.Cumple,
            ("Cubierta", "Detallado Ø/s") => !cubDirs.Any(d => d.Flexion.DetalladoInsuficiente),
            ("Cubierta", "Fisuración (fs ≤ fs,adm)") => cubDirs.All(d => d.Fisuracion.Cumple),
            ("Cubierta", "Cortante (Vu ≤ Vc)") => new[] { res.DisenoCubierta!.CortanteX, res.DisenoCubierta.CortanteY }.All(c => c.Cortante.Cumple),
            ("Fondo", "Detallado Ø/s") => !envCaras.Any(c => c.DetalladoInsuficiente),
            ("Fondo", "Fisuración (fs ≤ fs,adm)") => envCaras.Where(c => c.FisuracionCumple is not null).All(c => c.FisuracionCumple!.Value),
            ("Fondo", "Cortante (Vu ≤ Vc)") => new[] { res.EnvolventeFondo!.CortanteX, res.EnvolventeFondo.CortanteY }.All(c => c.Cumple),
            ("Muro longitudinal", "Detallado Ø/s") => MuroDet(mlDirs),
            ("Muro longitudinal", "Fisuración (fs ≤ fs,adm)") => MuroFis(mlDirs),
            ("Muro longitudinal", "Cortante (Vu ≤ Vc)") => MuroCort(res.DisenoMuroLongitudinal),
            ("Muro transversal", "Detallado Ø/s") => MuroDet(mtDirs),
            ("Muro transversal", "Fisuración (fs ≤ fs,adm)") => MuroFis(mtDirs),
            ("Muro transversal", "Cortante (Vu ≤ Vc)") => MuroCort(res.DisenoMuroTransversal),
            ("Estructura", "Flotabilidad (FS ≥ 1.25)") => res.Flotabilidad!.Cumple || res.Sobreancho?.EsPosible == true,
            _ => throw new ArgumentException($"Ítem inesperado: {elem} · {concepto}")
        };

        foreach (var it in v.Items)
        {
            totalAserciones++;
            var esperado = EsperadoDe(it.Elemento, it.Concepto);
            if (it.Cumple == esperado)
                Console.WriteLine($"  [OK  ] {it.Elemento} · {it.Concepto}: {it.Cumple}");
            else { fallos++; Console.WriteLine($"  [FAIL] {it.Elemento} · {it.Concepto}: veredicto={it.Cumple}, esperado={esperado}"); }
        }

        // (d) Escenario determinista con espesor de muro insuficiente → veredicto NO CUMPLE.
        {
            var geoF = new Geometria(BAnchoM: 3.0, LLargoM: 4.0, HtAlturaM: 2.5, ConTapa: false,
                EmEspesorMuroM: 0.14, EfEspesorFondoM: 0.20, EtEspesorTapaM: 0.0,
                HLAlturaLiquidoM: 2.0, HmAlturaSueloSobreMuroM: 2.0, WextSobrecargaKNm2: 0.0);
            var matF = new Materiales(FcMPa: 21, FyMPa: 420, GammaConcretoKNm3: 24, GammaLiquidoKNm3: 9.81,
                GammaSueloKNm3: 16, PhiGradosAnguloFriccionSuelo: 30);
            var proyF = new ProyectoTanque(geoF, matF);
            var paramF = new ParametrosCalculoTanque(
                CvCubiertaKNm2: 0.0, CgCubiertaKNm2: 0.0, CvFondoKNm2: 2.0,
                DiametrosBarra: new DiametrosBarraCalculo(
                    CatalogoBarras.DiametroPredeterminadoBarraMm, CatalogoBarras.DiametroPredeterminadoBarraMm,
                    CatalogoBarras.DiametroPredeterminadoBarraMm, CatalogoBarras.DiametroPredeterminadoBarraMm),
                MetodoInterpolacion: MetodoInterpolacion.Interpolar, IncluirDiagramas: false, Sismo: null);
            var resF = CalculadorTanque.Calcular(proyF, paramF);
            var vF = Veredicto.Calcular(resF);
            var itemEsp = vF.Items.First(i => i.Elemento == "Muros" && i.Concepto == "Espesor mínimo");
            totalAserciones++;
            if (!vF.Cumple && !itemEsp.Cumple && itemEsp.Detalle.Contains("déficit"))
                Console.WriteLine($"  [OK  ] Espesor 0.14m NO cumple → veredicto NO CUMPLE (ítem espesor con déficit)");
            else { fallos++; Console.WriteLine($"  [FAIL] El veredicto no captura el fallo de espesor mínimo (Cumple={vF.Cumple})"); }
        }
    }

    // Fase 2 del frente de interfaz (2026-08-30) -- curvas de momento (faja gobernante) y cortante
    // puntual gobernante (DiagramaMomento.cs). Verifica (a) el conjunto exacto de curvas del
    // escenario completo, (b) que el PICO de cada curva coincide con el momento gobernante del campo
    // fuente (max(pos, neg)) -- sin fórmula nueva, solo re-muestreo --, (c) la estructura de
    // posiciones/luz de cada faja, y (d) el cruce de cortantes contra el diseño.
    {
        Console.WriteLine(" --- Diagramas de momento y cortante (Fase 2 del frente de interfaz) ---");

        var r = resultadoF12;
        var d = DiagramaMomento.Calcular(r);

        double Pico(double[,] campo) { double m = 0; for (int f = 0; f < campo.GetLength(0); f++) for (int c = 0; c < campo.GetLength(1); c++) m = Math.Max(m, Math.Abs(campo[f, c])); return m; }
        double PicoCurva(CurvaMomento cu) => cu.Puntos.Max(p => Math.Abs(p.MomentoKNmM));
        CurvaMomento Curva(string elem, string dir, string cond) => d.Curvas.First(c => c.Elemento == elem && c.Direccion == dir && c.Condicion == cond);

        // (a) Conjunto exacto para el escenario F12 (tapa + nivel freático + Hm>0): cubierta(2) +
        //     fondo/envolvente(2) + muro long interior/exterior(4) + muro trans interior/exterior(4) = 12.
        totalAserciones++;
        var esperadas = new (string, string, string)[] {
            ("Cubierta","Mx",""), ("Cubierta","My",""),
            ("Fondo","Mx","Envolvente final"), ("Fondo","My","Envolvente final"),
            ("Muro longitudinal","Mx","Interior"), ("Muro longitudinal","My","Interior"),
            ("Muro longitudinal","Mx","Exterior"), ("Muro longitudinal","My","Exterior"),
            ("Muro transversal","Mx","Interior"), ("Muro transversal","My","Interior"),
            ("Muro transversal","Mx","Exterior"), ("Muro transversal","My","Exterior"),
        };
        var conjCurvas = d.Curvas.Count == esperadas.Length
            && esperadas.All(e => d.Curvas.Any(c => c.Elemento == e.Item1 && c.Direccion == e.Item2 && c.Condicion == e.Item3));
        if (conjCurvas) Console.WriteLine($"  [OK  ] {d.Curvas.Count} curvas con el conjunto exacto elemento×dirección×condición");
        else { fallos++; Console.WriteLine($"  [FAIL] Curvas: esperadas {esperadas.Length}, obtenidas {d.Curvas.Count}"); }

        // (b) Pico de cada curva == gobernante del campo fuente (max(pos, neg)).
        var cub = r.PlacaCubierta!;
        var env = r.CamposEnvolventeFondo!;
        var mlInt = r.MuroLongitudinalEstatico.Interior;
        var mlExt = r.MuroLongitudinalEstatico.Exterior!;
        var mtInt = r.MuroTransversalEstatico.Interior;
        var mtExt = r.MuroTransversalEstatico.Exterior!;

        var picos = new (CurvaMomento Curva, double Esperado)[]
        {
            (Curva("Cubierta","Mx",""), Math.Max(cub.MxPosGobernanteKNmM, cub.MxNegGobernanteKNmM)),
            (Curva("Cubierta","My",""), Math.Max(cub.MyPosGobernanteKNmM, cub.MyNegGobernanteKNmM)),
            (Curva("Fondo","Mx","Envolvente final"), Math.Max(Pico(env.CampoMxCaraInferior), Pico(env.CampoMxCaraSuperior))),
            (Curva("Fondo","My","Envolvente final"), Math.Max(Pico(env.CampoMyCaraInferior), Pico(env.CampoMyCaraSuperior))),
            (Curva("Muro longitudinal","Mx","Interior"), Math.Max(mlInt.MxPosGobernanteKNmM, mlInt.MxNegGobernanteKNmM)),
            (Curva("Muro longitudinal","My","Interior"), Math.Max(mlInt.MyPosGobernanteKNmM, mlInt.MyNegGobernanteKNmM)),
            (Curva("Muro longitudinal","Mx","Exterior"), Math.Max(mlExt.MxPosGobernanteKNmM, mlExt.MxNegGobernanteKNmM)),
            (Curva("Muro longitudinal","My","Exterior"), Math.Max(mlExt.MyPosGobernanteKNmM, mlExt.MyNegGobernanteKNmM)),
            (Curva("Muro transversal","Mx","Interior"), Math.Max(mtInt.MxPosGobernanteKNmM, mtInt.MxNegGobernanteKNmM)),
            (Curva("Muro transversal","My","Interior"), Math.Max(mtInt.MyPosGobernanteKNmM, mtInt.MyNegGobernanteKNmM)),
            (Curva("Muro transversal","Mx","Exterior"), Math.Max(mtExt.MxPosGobernanteKNmM, mtExt.MxNegGobernanteKNmM)),
            (Curva("Muro transversal","My","Exterior"), Math.Max(mtExt.MyPosGobernanteKNmM, mtExt.MyNegGobernanteKNmM)),
        };

        foreach (var (curva, esperado) in picos)
        {
            totalAserciones++;
            var pico = PicoCurva(curva);
            if (Math.Abs(pico - esperado) < 1e-6)
                Console.WriteLine($"  [OK  ] {curva.Elemento} {curva.Direccion} [{curva.Condicion}]: pico={pico:0.###} == gobernante");
            else { fallos++; Console.WriteLine($"  [FAIL] {curva.Elemento} {curva.Direccion} [{curva.Condicion}]: pico={pico:0.###} ≠ esperado={esperado:0.###}"); }
        }

        // (c) Estructura de posiciones/luz: muro Mx = 11 puntos con Luz = altura (HL); muro My =
        //     6 puntos con Luz = 0.5·b (b = L-em, eje a eje); placa = 6 puntos con Luz = 0.5·a
        //     (a = L-em). Posiciones 0..Luz.
        totalAserciones++;
        var mlMxInt = Curva("Muro longitudinal","Mx","Interior");
        var mlMyInt = Curva("Muro longitudinal","My","Interior");
        var cubMx = Curva("Cubierta","Mx","");
        var estructuraOk =
            mlMxInt.Puntos.Count == 11 && Math.Abs(mlMxInt.LuzM - r.Proyecto.Geometria.HLAlturaLiquidoM) < 1e-9
            && mlMyInt.Puntos.Count == 6 && Math.Abs(mlMyInt.LuzM - 0.5 * (r.Proyecto.Geometria.LLargoM - r.Proyecto.Geometria.EmEspesorMuroM)) < 1e-9
            && cubMx.Puntos.Count == 6 && Math.Abs(cubMx.LuzM - 0.5 * (r.Proyecto.Geometria.LLargoM - r.Proyecto.Geometria.EmEspesorMuroM)) < 1e-9
            && Math.Abs(mlMxInt.Puntos[0].PosicionM) < 1e-12
            && Math.Abs(mlMxInt.Puntos[^1].PosicionM - mlMxInt.LuzM) < 1e-9;
        if (estructuraOk) Console.WriteLine("  [OK  ] Estructura: muro Mx=11 pts/Luz=HL, muro My=6 pts/Luz=0.5·(L-em), placa=6 pts/Luz=0.5·(L-em); posiciones 0..Luz");
        else { fallos++; Console.WriteLine("  [FAIL] Estructura de curvas (nº de puntos, luz o posiciones) incorrecta"); }

        // (d) Cortantes gobernantes: 10 en total y cruce contra el diseño.
        totalAserciones++;
        var cortOk = d.Cortantes.Count == 10
            && d.Cortantes.First(c => c.Elemento == "Cubierta" && c.Ubicacion == "Borde 'a'").VuKNm == r.DisenoCubierta!.CortanteX.VuKN
            && d.Cortantes.First(c => c.Elemento == "Muro longitudinal" && c.Ubicacion == "Fondo").VuKNm == r.DisenoMuroLongitudinal.CortanteFondo.VuKN
            && d.Cortantes.First(c => c.Elemento == "Muro longitudinal" && c.Ubicacion == "Fondo").Cumple == r.DisenoMuroLongitudinal.CortanteFondo.Cortante.Cumple
            && d.Cortantes.First(c => c.Elemento == "Fondo" && c.Ubicacion == "Borde 'a'").VuKNm == r.EnvolventeFondo!.CortanteX.VuKN;
        if (cortOk) Console.WriteLine($"  [OK  ] {d.Cortantes.Count} cortantes gobernantes con cruce contra el diseño");
        else { fallos++; Console.WriteLine("  [FAIL] Cortantes gobernantes: conteo o cruce incorrecto"); }

        // (e) Campos completos (mapas de calor): 24 para F12 (cubierta4 + fondo/envolvente4 + muro long8 + muro trans8).
        totalAserciones++;
        var esperadasCampos = new (string, string, string, string)[] {
            ("Cubierta","Mx","Cara inferior",""), ("Cubierta","Mx","Cara superior",""),
            ("Cubierta","My","Cara inferior",""), ("Cubierta","My","Cara superior",""),
            ("Fondo","Mx","Cara inferior","Envolvente final"), ("Fondo","Mx","Cara superior","Envolvente final"),
            ("Fondo","My","Cara inferior","Envolvente final"), ("Fondo","My","Cara superior","Envolvente final"),
            ("Muro longitudinal","Mx","Cara interior","Interior"), ("Muro longitudinal","Mx","Cara exterior","Interior"),
            ("Muro longitudinal","My","Cara interior","Interior"), ("Muro longitudinal","My","Cara exterior","Interior"),
            ("Muro longitudinal","Mx","Cara interior","Exterior"), ("Muro longitudinal","Mx","Cara exterior","Exterior"),
            ("Muro longitudinal","My","Cara interior","Exterior"), ("Muro longitudinal","My","Cara exterior","Exterior"),
            ("Muro transversal","Mx","Cara interior","Interior"), ("Muro transversal","Mx","Cara exterior","Interior"),
            ("Muro transversal","My","Cara interior","Interior"), ("Muro transversal","My","Cara exterior","Interior"),
            ("Muro transversal","Mx","Cara interior","Exterior"), ("Muro transversal","Mx","Cara exterior","Exterior"),
            ("Muro transversal","My","Cara interior","Exterior"), ("Muro transversal","My","Cara exterior","Exterior"),
        };
        var conjCampos = d.Campos.Count == esperadasCampos.Length
            && esperadasCampos.All(e => d.Campos.Any(c => c.Elemento == e.Item1 && c.Direccion == e.Item2 && c.Cara == e.Item3 && c.Condicion == e.Item4));
        if (conjCampos) Console.WriteLine($"  [OK  ] {d.Campos.Count} campos con el conjunto exacto elemento×dirección×cara×condición");
        else { fallos++; Console.WriteLine($"  [FAIL] Campos: esperados {esperadasCampos.Length}, obtenidos {d.Campos.Count}"); }

        // (f) Cada campo es la MISMA grilla que su fuente (copia fiel, sin manipular) y su pico == gobernante.
        CampoMomento Campo(string elem, string dir, string cara, string cond) => d.Campos.First(c => c.Elemento == elem && c.Direccion == dir && c.Cara == cara && c.Condicion == cond);
        bool Igual(double[,] a, double[,] b)
        {
            if (a.GetLength(0) != b.GetLength(0) || a.GetLength(1) != b.GetLength(1)) return false;
            for (int i = 0; i < a.GetLength(0); i++) for (int j = 0; j < a.GetLength(1); j++) if (Math.Abs(a[i, j] - b[i, j]) > 1e-9) return false;
            return true;
        }
        double[,] Neg(double[,] a)
        {
            var n = new double[a.GetLength(0), a.GetLength(1)];
            for (int i = 0; i < a.GetLength(0); i++) for (int j = 0; j < a.GetLength(1); j++) n[i, j] = -a[i, j];
            return n;
        }
        var crucesCampos = new (CampoMomento Campo, double[,] Fuente, double Gobernante, string Nombre)[]
        {
            (Campo("Cubierta","Mx","Cara inferior",""), cub.CampoMxPos, cub.MxPosGobernanteKNmM, "Cubierta Mx+"),
            (Campo("Cubierta","Mx","Cara superior",""), cub.CampoMxNeg, cub.MxNegGobernanteKNmM, "Cubierta Mx-"),
            (Campo("Cubierta","My","Cara inferior",""), cub.CampoMyPos, cub.MyPosGobernanteKNmM, "Cubierta My+"),
            (Campo("Fondo","Mx","Cara inferior","Envolvente final"), env.CampoMxCaraInferior, Pico(env.CampoMxCaraInferior), "Fondo Mx inferior"),
            (Campo("Fondo","Mx","Cara superior","Envolvente final"), Neg(env.CampoMxCaraSuperior), Pico(env.CampoMxCaraSuperior), "Fondo Mx superior"),
            (Campo("Muro longitudinal","Mx","Cara interior","Interior"), mlInt.CampoMxPos, mlInt.MxPosGobernanteKNmM, "MuroL Mx+ interior"),
            (Campo("Muro longitudinal","My","Cara exterior","Exterior"), mlExt.CampoMyNeg, mlExt.MyNegGobernanteKNmM, "MuroL My- exterior"),
            (Campo("Muro transversal","Mx","Cara interior","Exterior"), mtExt.CampoMxPos, mtExt.MxPosGobernanteKNmM, "MuroT Mx+ exterior"),
        };
        foreach (var (campo, fuente, gobernante, nombre) in crucesCampos)
        {
            totalAserciones++;
            var mismaGrilla = Igual(campo.Valores, fuente);
            var picoCampo = Pico(campo.Valores);
            var picoOk = Math.Abs(picoCampo - gobernante) < 1e-6;
            if (mismaGrilla && picoOk)
                Console.WriteLine($"  [OK  ] {nombre}: grilla == fuente y pico {picoCampo:0.###} == gobernante");
            else { fallos++; Console.WriteLine($"  [FAIL] {nombre}: grilla==fuente:{mismaGrilla}, pico {picoCampo:0.###} vs gobernante {gobernante:0.###}"); }
        }

        // (g) Estructura de grilla: muro 11×6 (LuzFilas=HL, LuzColumnas=0.5·b, b=L-em); placa 6×6
        //     (0.5·a, 0.5·b, a=L-em) -- luces EJE A EJE (PCA pág. 173).
        totalAserciones++;
        var campMuroMx = Campo("Muro longitudinal","Mx","Cara interior","Interior");
        var campPlacaMx = Campo("Cubierta","Mx","Cara inferior","");
        var estructuraCampos =
            campMuroMx.Valores.GetLength(0) == 11 && campMuroMx.Valores.GetLength(1) == 6
            && Math.Abs(campMuroMx.LuzFilasM - r.Proyecto.Geometria.HLAlturaLiquidoM) < 1e-9
            && Math.Abs(campMuroMx.LuzColumnasM - 0.5 * (r.Proyecto.Geometria.LLargoM - r.Proyecto.Geometria.EmEspesorMuroM)) < 1e-9
            && campPlacaMx.Valores.GetLength(0) == 6 && campPlacaMx.Valores.GetLength(1) == 6
            && Math.Abs(campPlacaMx.LuzFilasM - 0.5 * (r.Proyecto.Geometria.LLargoM - r.Proyecto.Geometria.EmEspesorMuroM)) < 1e-9;
        if (estructuraCampos) Console.WriteLine("  [OK  ] Estructura de campos: muro 11×6 (LuzFilas=HL, LuzCols=0.5·(L-em)), placa 6×6 (0.5·(L-em), 0.5·b)");
        else { fallos++; Console.WriteLine("  [FAIL] Estructura de campos incorrecta"); }
    }

    // Fase3 del frente de interfaz (2026-08-30) -- reporte profesional HTML (ReporteHtml.cs).
    // Verifica la estructura del HTML autocontenido: encabezado + banner de veredicto + las siete
    // secciones agrupadas (sin grillas ASCII) + mapas de calor SVG + pie normativo, con etiquetas
    // balanceadas (smoke-test de render sin dependencias de navegador).
    {
        Console.WriteLine(" --- Reporte profesional HTML (Fase3 del frente de interfaz) ---");
        var html = ReporteHtml.Generar(resultadoF12);

        // (a) Documento y encabezado.
        totalAserciones++;
        var documento = html.Contains("<!DOCTYPE html>") && html.Contains("<html") && html.Contains("Memoria de cálculo")
            && html.Contains("NSR-10 Título C (C.23)");
        if (documento) Console.WriteLine("  [OK  ] Documento HTML con encabezado y referencia normativa");
        else { fallos++; Console.WriteLine("  [FAIL] Documento HTML: falta DOCTYPE/encabezado/referencia"); }

        // (b) Banner de veredicto (Fase1) con la tabla de verificaciones.
        totalAserciones++;
        var banner = html.Contains("class=\"veredicto") && html.Contains("CUMPLE") && html.Contains("Elemento")
            && html.Contains("Concepto");
        if (banner) Console.WriteLine("  [OK  ] Banner de veredicto con tabla de verificaciones");
        else { fallos++; Console.WriteLine("  [FAIL] Banner de veredicto ausente/incompleto"); }

        // (c) Las siete secciones agrupadas, sin grillas ASCII (los diagramas van como SVG).
        totalAserciones++;
        var grupos = new[] { "Datos generales", "Hidrostático / Tierras", "Sismo", "Dinámico",
            "Diseño de losas", "Diseño de muros", "Envolventes" };
        var seccionesOk = grupos.All(g => html.Contains("<h2>" + g + "</h2>")) && !html.Contains(", kN·m/m (filas:");
        if (seccionesOk) Console.WriteLine("  [OK  ] Siete secciones agrupadas, sin grillas ASCII");
        else { fallos++; Console.WriteLine("  [FAIL] Secciones agrupadas: faltan grupos o quedaron grillas ASCII"); }

        // (d) Mapas de calor SVG (campo completo) presentes y balanceados.
        totalAserciones++;
        var svgOk = html.Contains("<svg") && html.Contains("</svg>")
            && html.Split("<svg").Length - 1 == html.Split("</svg>").Length - 1;
        if (svgOk) Console.WriteLine($"  [OK  ] SVG de mapas de calor balanceados ({html.Split("<svg").Length - 1})");
        else { fallos++; Console.WriteLine("  [FAIL] SVG de mapas de calor ausentes o desbalanceados"); }

        // (e) Pie normativo.
        totalAserciones++;
        var pie = html.Contains("<footer") && html.Contains("ACI350.3-06") && html.Contains("Mononobe-Okabe");
        if (pie) Console.WriteLine("  [OK  ] Pie con citas normativas");
        else { fallos++; Console.WriteLine("  [FAIL] Pie normativo ausente/incompleto"); }

        // (f) Balance de etiquetas principales (smoke-test de estructura bien formada).
        totalAserciones++;
        int Cuenta(string tag) => html.Split("<" + tag).Length - 1;
        var balance = Cuenta("section") == Cuenta("/section") && Cuenta("table") == Cuenta("/table")
            && Cuenta("figure") == Cuenta("/figure") && Cuenta("div") == Cuenta("/div");
        if (balance) Console.WriteLine("  [OK  ] Etiquetas section/table/figure/div balanceadas");
        else { fallos++; Console.WriteLine("  [FAIL] Etiquetas HTML desbalanceadas"); }

        // (g) B/L son dimensiones EXTERIORES: el encabezado HTML lo declara con los claros interiores.
        totalAserciones++;
        var ext = html.Contains("(dimensiones EXTERIORES") && html.Contains("claros interiores B-2·em") && html.Contains("altura interior del muro") && html.Contains("cara externa de la losa de fondo");
        if (ext) Console.WriteLine("  [OK  ] Encabezado HTML declara B/L exteriores y claros interiores");
        else { fallos++; Console.WriteLine("  [FAIL] Encabezado HTML no declara B/L como exteriores"); }

        // (h) Validación simétrica: L-2·em ≤0 debe lanzar (B/L son exteriores; antes solo se validaba B).
        totalAserciones++;
        try
        {
            var geoLInvalida = new Geometria(BAnchoM: 6.0, LLargoM: 0.20, HtAlturaM: 3.5, ConTapa: true,
                EmEspesorMuroM: 0.15, EfEspesorFondoM: 0.20, EtEspesorTapaM: 0.15,
                HLAlturaLiquidoM: 3.0, HmAlturaSueloSobreMuroM: 2.0, WextSobrecargaKNm2: 0.0);
            geoLInvalida.Validar();
            fallos++;
            Console.WriteLine("  [FAIL] L-2·em ≤0 no lanzó excepción (L es exterior)");
        }
        catch (ArgumentException)
        {
            Console.WriteLine("  [OK  ] L-2·em ≤0 lanza (claro interior inválido en L)");
        }

        // (i) Orientación de los mapas de calor (2026-08-31): nota "cómo leer", leyenda semántica
        // (hogging/sagging), etiquetas de borde físico en los ejes y celda gobernante resaltada.
        totalAserciones++;
        var orientado = html.Contains("Cómo leer los mapas")
            && html.Contains("(hogging)") && html.Contains("(sagging)")
            && html.Contains("tope ") && html.Contains("base ") && html.Contains("borde ") && html.Contains("centro ")
            && html.Contains("stroke=\"#111827\"") && html.Contains("font-weight=\"bold\"");
        if (orientado) Console.WriteLine("  [OK  ] Mapas orientados: nota 'cómo leer', leyenda hogging/sagging, ejes tope/base/borde/centro y celda gobernante resaltada");
        else { fallos++; Console.WriteLine("  [FAIL] Orientación de mapas ausente/incompleta"); }

        // (j) Anclaje en cero: el helper compartido RangoSimetrico devuelve ±máx|M|, de modo que el
        // cero sea SIEMPRE el centro de la escala (blanco) y nunca un extremo saturado.
        totalAserciones++;
        var rango = MapaDeColor.RangoSimetrico(new double[,] { { 0.0, 11.6 }, { 5.0, 2.0 } });
        var anclada = Math.Abs(rango.Minimo - (-11.6)) < 1e-12 && Math.Abs(rango.Maximo - 11.6) < 1e-12;
        if (anclada) Console.WriteLine("  [OK  ] Escala anclada en cero: RangoSimetrico = ±máx|M| (el cero nunca cae en un extremo saturado)");
        else { fallos++; Console.WriteLine("  [FAIL] RangoSimetrico no es simétrico en cero"); }
    }

    // Fase4 (2026-08-31) -- persistencia JSON (PersistenciaTanque): round-trip del escenario F12
    // y JSON legible (enums como cadenas, sin la propiedad derivada RelacionLadosR).
    {
        Console.WriteLine(" --- Persistencia JSON (Fase4 del frente de interfaz) ---");

        var entrada = new EntradaCalculoTanque("Escenario F12 (verificación)", resultadoF12.Proyecto, resultadoF12.Parametros);
        var json = PersistenciaTanque.Serializar(entrada);
        var vuelta = PersistenciaTanque.Deserializar(json);

        totalAserciones++;
        var roundTrip = vuelta.Proyecto.Geometria == entrada.Proyecto.Geometria
            && vuelta.Proyecto.Materiales == entrada.Proyecto.Materiales
            && vuelta.Parametros == entrada.Parametros;
        if (roundTrip) Console.WriteLine("  [OK  ] Round-trip JSON: Geometria + Materiales + Parametros idénticos (F12)");
        else { fallos++; Console.WriteLine("  [FAIL] Round-trip JSON no reproduce la entrada F12"); }

        totalAserciones++;
        var legible = json.Contains("\"EnterradoConNivelFreatico\"") && json.Contains("\"Interpolar\"") && !json.Contains("RelacionLadosR");
        if (legible) Console.WriteLine("  [OK  ] JSON legible: enums como cadenas y sin RelacionLadosR");
        else { fallos++; Console.WriteLine("  [FAIL] JSON no legible (enums o RelacionLadosR)"); }
    }

    // Fase4 (2026-08-31) -- exportación CSV (ExportadorCsv, ítem 2): grillas/resultados en formato
    // largo (una sola tabla de 12 columnas). Verifica que no lanza sobre el escenario completo F12,
    // que es determinista, que toda línea tiene exactamente 12 columnas (sin comas sin escapar), que
    // están los 8 bloques, que "Momento" vuelca TODAS las celdas de los campos de DiagramaMomento,
    // que "Diseño" emite las 16 direcciones/caras × 13 conceptos fijos, y que el escalar exportado
    // coincide con el registro ya verificado (round-trip, sin fórmulas nuevas).
    {
        Console.WriteLine(" --- Exportación CSV (Fase4 del frente de interfaz, ítem 2) ---");

        var csv = ExportadorCsv.Generar(resultadoF12);
        var lineas = csv.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToArray();

        // (a) Cabecera fija de 12 columnas.
        totalAserciones++;
        var cabecera = lineas.Length > 0 &&
            lineas[0] == "Bloque,Elemento,Concepto,Detalle,Subdetalle,Fila,Columna,PosFila_m,PosCol_m,Valor,Unidad,Texto";
        if (cabecera) Console.WriteLine($"  [OK  ] CSV con cabecera de 12 columnas ({lineas.Length - 1} filas de datos)");
        else { fallos++; Console.WriteLine("  [FAIL] Cabecera CSV incorrecta o ausente"); }

        // (b) Determinismo: mismo ResultadoCalculoTanque → mismo CSV byte a byte.
        totalAserciones++;
        var determinista = csv == ExportadorCsv.Generar(resultadoF12);
        if (determinista) Console.WriteLine("  [OK  ] CSV determinista (dos generaciones idénticas)");
        else { fallos++; Console.WriteLine("  [FAIL] CSV no determinista"); }

        // (c) Toda línea tiene exactamente 12 columnas (respetando comillas).
        static int ContarColumnas(string linea)
        {
            var n = 1; var enComillas = false;
            foreach (var ch in linea)
            {
                if (enComillas) { if (ch == '"') enComillas = false; }
                else if (ch == '"') enComillas = true;
                else if (ch == ',') n++;
            }
            return n;
        }
        totalAserciones++;
        var columnasOk = lineas.All(l => ContarColumnas(l) == 12);
        if (columnasOk) Console.WriteLine("  [OK  ] Todas las líneas con exactamente 12 columnas");
        else { fallos++; Console.WriteLine("  [FAIL] Alguna línea del CSV no tiene 12 columnas"); }

        // (d) Los ocho bloques están presentes.
        totalAserciones++;
        var bloques = new[] { "Veredicto", "Momento", "Diseño", "Cortante", "Cargas", "Presiones", "Flotabilidad", "EspesorMínimo" };
        var bloquesOk = bloques.All(b => lineas.Skip(1).Any(l => l.StartsWith(b + ",")));
        if (bloquesOk) Console.WriteLine("  [OK  ] Los 8 bloques (Veredicto/Momento/Diseño/Cortante/Cargas/Presiones/Flotabilidad/EspesorMínimo) presentes");
        else { fallos++; Console.WriteLine("  [FAIL] Falta algún bloque del CSV"); }

        // (e) Momento: vuelca TODAS las celdas de todos los campos de DiagramaMomento.
        var campos = DiagramaMomento.Calcular(resultadoF12).Campos;
        var celdasEsperadas = campos.Sum(cmp => cmp.Valores.GetLength(0) * cmp.Valores.GetLength(1));
        var filasMomento = lineas.Skip(1).Count(l => l.StartsWith("Momento,"));
        totalAserciones++;
        if (filasMomento == celdasEsperadas)
            Console.WriteLine($"  [OK  ] Momento vuelca las {celdasEsperadas} celdas de los {campos.Count} campos (muros 11×6, placas 6×6)");
        else { fallos++; Console.WriteLine($"  [FAIL] Momento: {filasMomento} filas ≠ {celdasEsperadas} celdas esperadas"); }

        // (f) Diseño: 16 direcciones/caras × 13 conceptos fijos = 208 filas.
        var filasDiseno = lineas.Skip(1).Count(l => l.StartsWith("Diseño,"));
        totalAserciones++;
        if (filasDiseno == 208) Console.WriteLine("  [OK  ] Diseño: 16 direcciones/caras × 13 conceptos = 208 filas");
        else { fallos++; Console.WriteLine($"  [FAIL] Diseño: {filasDiseno} filas ≠ 208 esperadas"); }

        // (g) Round-trip: el Mu exportado de Cubierta Mx+ coincide con el registro ya verificado.
        string[] Columnas(string linea)
        {
            var res = new List<string>(); var actual = new System.Text.StringBuilder(); var enC = false;
            foreach (var ch in linea)
            {
                if (enC) { if (ch == '"') enC = false; else actual.Append(ch); }
                else if (ch == '"') enC = true;
                else if (ch == ',') { res.Add(actual.ToString()); actual.Clear(); }
                else actual.Append(ch);
            }
            res.Add(actual.ToString());
            return res.ToArray();
        }
        var filaMu = lineas.Skip(1).FirstOrDefault(l => l.StartsWith("Diseño,Cubierta,Mu,Mx+,"));
        var muCsv = 0.0;
        totalAserciones++;
        var muOk = filaMu is not null
            && double.TryParse(Columnas(filaMu)[9], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out muCsv)
            && Math.Abs(muCsv - resultadoF12.DisenoCubierta!.MxPositivo.MuKNm) < 1e-6;
        if (muOk) Console.WriteLine($"  [OK  ] Round-trip Mu cubierta Mx+ = {resultadoF12.DisenoCubierta!.MxPositivo.MuKNm:0.###} kN·m/m (coincide con el registro)");
        else { fallos++; Console.WriteLine("  [FAIL] Round-trip Mu cubierta Mx+ no coincide con el registro"); }
    }

    FinModulo12: ;
}

// ============================================================================
// Pendiente "C" (2026-08-29) -- catálogo real de barras (diámetros/separaciones
// comerciales) y detallado C.23-C.14.3 (doble capa >250mm, espaciamiento máx. 300mm).
// ============================================================================
{
    Console.WriteLine("=== Catalogo de barras y detallado C.23-C.14.3 (pendiente C, backlog) ===");

    // 1) El catálogo enumera combinaciones reales (diámetro, separación) ordenadas por As ascendente,
    //    todas con separación <= 300mm (tope C.23-C.14.3 / ACI 350 §14.3.5).
    var combinaciones = CatalogoBarras.GenerarCombinacionesOrdenadas(CatalogoBarras.EspaciamientoMaximoMuroM);
    totalAserciones++;
    var catalogoOrdenadoYAscendente = combinaciones.Count > 0
        && combinaciones.All(c => c.SeparacionM <= CatalogoBarras.EspaciamientoMaximoMuroM + 1e-9)
        && Enumerable.Range(0, combinaciones.Count - 1).All(i => combinaciones[i].AsSuministradoMm2 <= combinaciones[i + 1].AsSuministradoMm2 + 1e-9);
    if (catalogoOrdenadoYAscendente)
        Console.WriteLine($"  [OK  ] Catalogo: {combinaciones.Count} combinaciones reales, separación<=300mm y As ascendente");
    else { fallos++; Console.WriteLine("  [FAIL] Catalogo no ordenado/ascendente o con separación>300mm"); }

    // 2) El As suministrado de cada combinación es exactamente área(diámetro)/separación.
    totalAserciones++;
    var asCoherente = combinaciones.All(c =>
        Math.Abs(c.AsSuministradoMm2 - CatalogoBarras.AreaBarraMm2(c.DiametroMm) / c.SeparacionM) < 1e-6);
    if (asCoherente) Console.WriteLine("  [OK  ] As de cada combinación == área(Ø)/s (coherencia interna del catálogo)");
    else { fallos++; Console.WriteLine("  [FAIL] As del catálogo no coincide con área(Ø)/s"); }

    // 3) Regla de doble capa C.23-C.14.3 (ACI 350 §14.3.4): espesor > 250mm.
    totalAserciones++;
    var dobleCapa = !CatalogoBarras.RequiereDobleCapa(0.250)
        && CatalogoBarras.RequiereDobleCapa(0.250 + 1e-6)
        && !CatalogoBarras.RequiereDobleCapa(0.200);
    if (dobleCapa) Console.WriteLine("  [OK  ] RequiereDobleCapa: false en 250mm, true justo por encima (C.23-C.14.3 / ACI 350 §14.3.4)");
    else { fallos++; Console.WriteLine("  [FAIL] RequiereDobleCapa no respeta el umbral de 250mm"); }

    // 4) El camino del catálogo en el control de fisuración expone un detallado REAL (diámetro +
    //    separación comerciales), con separación <=300mm, y el As reportado coincide con el As
    //    suministrado por esa combinación del catálogo.
    {
        var geoC = new Geometria(BAnchoM: 4.5, LLargoM: 6.0, HtAlturaM: 3.8, ConTapa: true, EmEspesorMuroM: 0.25, EfEspesorFondoM: 0.20, EtEspesorTapaM: 0.20, HLAlturaLiquidoM: 3.0, HmAlturaSueloSobreMuroM: 3.0, WextSobrecargaKNm2: 0.0);
        var matC = new Materiales(FcMPa: 21, FyMPa: 420, GammaConcretoKNm3: 24, GammaLiquidoKNm3: 9.81, GammaSueloKNm3: 16, PhiGradosAnguloFriccionSuelo: 30);
        var proyectoC = new ProyectoTanque(geoC, matC);
        var cargasC = CargasGravitacionales.Calcular(proyectoC, cargaVivaCubiertaKNm2: 1.5, cargaAdicionalCubiertaKNm2: 0.5);
        var disenoCubierta = DisenoPlacas.DisenarPlacaCubierta(proyectoC, cargasC, cvKNm2: 1.5, cgKNm2: 0.5);
        var f = disenoCubierta.MxPositivo.Flexion;
        totalAserciones++;
        var detalladoReal = f.DiametroBarraMm is double db && f.SeparacionM is double s
            && s <= CatalogoBarras.EspaciamientoMaximoMuroM + 1e-9
            && Math.Abs(f.AsRequeridoMm2 - CatalogoBarras.AreaBarraMm2(db) / s) < 1e-6;
        if (detalladoReal)
            Console.WriteLine($"  [OK  ] Detallado de cubierta Mx+: Ø{f.DiametroBarraMm:0.#}mm @ {f.SeparacionM * 1000:0}mm, As={f.AsRequeridoMm2:0.#}mm²/m == área(Ø)/s, separación<=300mm");
        else { fallos++; Console.WriteLine($"  [FAIL] Detallado de cubierta no comercial: db={f.DiametroBarraMm}, s={f.SeparacionM}"); }
    }

    // 5) El detallado del MURO (sin sismo → las cuatro direcciones son estáticas y llevan control
    //    de fisuración) respeta el tope de 300mm en todas las direcciones.
    {
        var geom = new Geometria(BAnchoM: 4.5, LLargoM: 6.0, HtAlturaM: 3.5, ConTapa: false, EmEspesorMuroM: 0.25, EfEspesorFondoM: 0.20, EtEspesorTapaM: 0.0, HLAlturaLiquidoM: 3.0, HmAlturaSueloSobreMuroM: 3.0, WextSobrecargaKNm2: 0.0);
        var matm = new Materiales(FcMPa: 21, FyMPa: 420, GammaConcretoKNm3: 24, GammaLiquidoKNm3: 9.81, GammaSueloKNm3: 16, PhiGradosAnguloFriccionSuelo: 30);
        var proyectom = new ProyectoTanque(geom, matm);
        var presiones = PresionesLaterales.Calcular(proyectom);
        var disenoL = DisenoMuros.DisenarMuroLongitudinal(proyectom, presiones, sismoHidrodinamico: null, sismoSuelo: null);
        var dirsM = new[] { disenoL.VerticalPositivo, disenoL.VerticalNegativo, disenoL.HorizontalPositivo, disenoL.HorizontalNegativo };
        totalAserciones++;
        var detalladoMuro = dirsM.All(dd => dd.Fisuracion is not null
            && dd.Flexion.DiametroBarraMm is double dbM
            && dd.Flexion.SeparacionM is double sM
            && sM <= CatalogoBarras.EspaciamientoMaximoMuroM + 1e-9);
        if (detalladoMuro)
            Console.WriteLine($"  [OK  ] Detallado de muro: 4 direcciones estáticas con detallado real y separación<=300mm");
        else { fallos++; Console.WriteLine("  [FAIL] Detallado de muro no resuelto o separación>300mm en alguna dirección estática"); }
    }
}

// ============================================================================
// Cuantía mínima de retracción/temperatura (2026-08-29) -- cruce normativo
// C.23-C.7.12.2.1 (Tabla C.23-C.7.12.2.1, folio C-440): la cuantía mínima de
// retracción de fraguado y variación de temperatura es FUNCIÓN de la distancia
// entre juntas y del grado del acero (fy), sustituyendo la genérica 0.0018 en
// losas y el 0.0030 fijo en el refuerzo horizontal de muros.
// ============================================================================
{
    Console.WriteLine("=== Cuantia minima de retraccion/temperatura C.23-C.7.12.2.1 (Tabla C.23-C.7.12.2.1) ===");

    // 1) Columna fy=420 MPa, exacta por rango de distancia entre juntas.
    AssertTol("fy=420, <6 m -> 0.0030", CatalogoBarras.CuantiaMinimaRetracionTemperatura(3.0, 420), 0.0030, atol: 1e-9);
    AssertTol("fy=420, 6-9 m -> 0.0030", CatalogoBarras.CuantiaMinimaRetracionTemperatura(7.0, 420), 0.0030, atol: 1e-9);
    AssertTol("fy=420, 9-12 m -> 0.0040", CatalogoBarras.CuantiaMinimaRetracionTemperatura(10.0, 420), 0.0040, atol: 1e-9);
    AssertTol("fy=420, >=12 m -> 0.0050", CatalogoBarras.CuantiaMinimaRetracionTemperatura(20.0, 420), 0.0050, atol: 1e-9);

    // 2) Columna fy=240 MPa, exacta por rango.
    AssertTol("fy=240, <6 m -> 0.0030", CatalogoBarras.CuantiaMinimaRetracionTemperatura(3.0, 240), 0.0030, atol: 1e-9);
    AssertTol("fy=240, 6-9 m -> 0.0040", CatalogoBarras.CuantiaMinimaRetracionTemperatura(7.0, 240), 0.0040, atol: 1e-9);
    AssertTol("fy=240, 9-12 m -> 0.0050", CatalogoBarras.CuantiaMinimaRetracionTemperatura(10.0, 240), 0.0050, atol: 1e-9);
    AssertTol("fy=240, >=12 m -> 0.0060", CatalogoBarras.CuantiaMinimaRetracionTemperatura(20.0, 240), 0.0060, atol: 1e-9);

    // 3) Interpolación lineal en fy (punto medio 330) y sujeción fuera de [240,420].
    AssertTol("fy=330 (punto medio), 6-9 m -> 0.0035", CatalogoBarras.CuantiaMinimaRetracionTemperatura(7.0, 330), 0.0035, atol: 1e-9);
    AssertTol("fy=200 (<=240), 6-9 m -> 0.0040 (sujeto a la columna de 240)", CatalogoBarras.CuantiaMinimaRetracionTemperatura(7.0, 200), 0.0040, atol: 1e-9);
    AssertTol("fy=500 (>=420), 9-12 m -> 0.0040 (sujeto a la columna de 420)", CatalogoBarras.CuantiaMinimaRetracionTemperatura(10.0, 500), 0.0040, atol: 1e-9);

    totalAserciones++;
    try { CatalogoBarras.CuantiaMinimaRetracionTemperatura(-1.0, 420); fallos++; Console.WriteLine("  [FAIL] distancia entre juntas negativa no lanzo"); }
    catch (ArgumentOutOfRangeException) { Console.WriteLine("  [OK  ] distancia entre juntas negativa lanza ArgumentOutOfRangeException"); }
    totalAserciones++;
    try { CatalogoBarras.CuantiaMinimaRetracionTemperatura(6.0, 0.0); fallos++; Console.WriteLine("  [FAIL] fy<=0 no lanzo"); }
    catch (ArgumentOutOfRangeException) { Console.WriteLine("  [OK  ] fy<=0 lanza ArgumentOutOfRangeException"); }

    // 4) Integración en placas (cubierta y fondo): cada dirección respeta AL MENOS la cuantía
    //    mínima de la tabla en la dirección de su refuerzo -- ya no la genérica 0.0018.
    {
        var geoR = new Geometria(BAnchoM: 4.5, LLargoM: 6.0, HtAlturaM: 3.8, ConTapa: true, EmEspesorMuroM: 0.25, EfEspesorFondoM: 0.20, EtEspesorTapaM: 0.20, HLAlturaLiquidoM: 3.0, HmAlturaSueloSobreMuroM: 3.0, WextSobrecargaKNm2: 0.0);
        var matR = new Materiales(FcMPa: 21, FyMPa: 420, GammaConcretoKNm3: 24, GammaLiquidoKNm3: 9.81, GammaSueloKNm3: 16, PhiGradosAnguloFriccionSuelo: 30);
        var proyectoR = new ProyectoTanque(geoR, matR);
        var cargasR = CargasGravitacionales.Calcular(proyectoR, cargaVivaCubiertaKNm2: 1.5, cargaAdicionalCubiertaKNm2: 0.5);
        var cubR = DisenoPlacas.DisenarPlacaCubierta(proyectoR, cargasR, cvKNm2: 1.5, cgKNm2: 0.5);
        var fdoR = DisenoPlacas.DisenarPlacaFondo(proyectoR, cargasR, cvKNm2: 0.0);

        var minCubMx = CatalogoBarras.CuantiaMinimaRetracionTemperatura(geoR.LLargoM, matR.FyMPa);
        var minCubMy = CatalogoBarras.CuantiaMinimaRetracionTemperatura(geoR.BAnchoM, matR.FyMPa);
        var minFdoMx = CatalogoBarras.CuantiaMinimaRetracionTemperatura(geoR.BAnchoM, matR.FyMPa);
        var minFdoMy = CatalogoBarras.CuantiaMinimaRetracionTemperatura(geoR.LLargoM, matR.FyMPa);
        // Notas opcionales C.23-C.7.12.2.1 (2026-08-30): 50 % en la cara inferior (contra el suelo).
        var minFdoMxSuperior = CatalogoBarras.AjustarCuantiaMinimaRetracionTemperatura(minFdoMx, geoR.EfEspesorFondoM, caraInferiorContraSuelo: false);
        var minFdoMxInferior = CatalogoBarras.AjustarCuantiaMinimaRetracionTemperatura(minFdoMx, geoR.EfEspesorFondoM, caraInferiorContraSuelo: true);
        var minFdoMySuperior = CatalogoBarras.AjustarCuantiaMinimaRetracionTemperatura(minFdoMy, geoR.EfEspesorFondoM, caraInferiorContraSuelo: false);
        var minFdoMyInferior = CatalogoBarras.AjustarCuantiaMinimaRetracionTemperatura(minFdoMy, geoR.EfEspesorFondoM, caraInferiorContraSuelo: true);

        totalAserciones++;
        var placasOk = cubR.MxPositivo.Flexion.Rho >= minCubMx - 1e-9 && cubR.MxNegativo.Flexion.Rho >= minCubMx - 1e-9
            && cubR.MyPositivo.Flexion.Rho >= minCubMy - 1e-9 && cubR.MyNegativo.Flexion.Rho >= minCubMy - 1e-9
            && fdoR.MxPositivo.Flexion.Rho >= minFdoMxInferior - 1e-9 && fdoR.MxNegativo.Flexion.Rho >= minFdoMxSuperior - 1e-9
            && fdoR.MyPositivo.Flexion.Rho >= minFdoMyInferior - 1e-9 && fdoR.MyNegativo.Flexion.Rho >= minFdoMySuperior - 1e-9;
        if (placasOk)
            Console.WriteLine($"  [OK  ] Placas: cubierta (Mx@L={geoR.LLargoM}->{minCubMx:0.0000}, My@B={geoR.BAnchoM}->{minCubMy:0.0000}) y fondo (Mx@B->{minFdoMx:0.0000}, My@L->{minFdoMy:0.0000}) respetan la cuantia minima de la tabla en cada direccion");
        else { fallos++; Console.WriteLine("  [FAIL] Alguna direccion de placa cae por debajo de la cuantia minima de retraccion/temperatura de la tabla"); }
    }

    // 5) Integración en muro: el refuerzo HORIZONTAL respeta la tabla (función de L/B), el VERTICAL el 0.0030 fijo.
    {
        var geoM = new Geometria(BAnchoM: 4.5, LLargoM: 6.0, HtAlturaM: 3.5, ConTapa: false, EmEspesorMuroM: 0.25, EfEspesorFondoM: 0.20, EtEspesorTapaM: 0.0, HLAlturaLiquidoM: 3.0, HmAlturaSueloSobreMuroM: 3.0, WextSobrecargaKNm2: 0.0);
        var matM = new Materiales(FcMPa: 21, FyMPa: 420, GammaConcretoKNm3: 24, GammaLiquidoKNm3: 9.81, GammaSueloKNm3: 16, PhiGradosAnguloFriccionSuelo: 30);
        var proyectoM = new ProyectoTanque(geoM, matM);
        var presionesM = PresionesLaterales.Calcular(proyectoM);
        var muroL = DisenoMuros.DisenarMuroLongitudinal(proyectoM, presionesM, sismoHidrodinamico: null, sismoSuelo: null);
        var muroT = DisenoMuros.DisenarMuroTransversal(proyectoM, presionesM, sismoHidrodinamico: null, sismoSuelo: null);

        var minHL = CatalogoBarras.CuantiaMinimaRetracionTemperatura(geoM.LLargoM, matM.FyMPa);
        var minHT = CatalogoBarras.CuantiaMinimaRetracionTemperatura(geoM.BAnchoM, matM.FyMPa);

        totalAserciones++;
        var murosOk = muroL.HorizontalPositivo.Flexion.Rho >= minHL - 1e-9 && muroL.HorizontalNegativo.Flexion.Rho >= minHL - 1e-9
            && muroT.HorizontalPositivo.Flexion.Rho >= minHT - 1e-9 && muroT.HorizontalNegativo.Flexion.Rho >= minHT - 1e-9
            && muroL.VerticalPositivo.Flexion.Rho >= DisenoFlexionCortanteFisuracion.CuantiaMinimaMuroTanque - 1e-9;
        if (murosOk)
            Console.WriteLine($"  [OK  ] Muros: horizontal respeta la tabla (long@L={geoM.LLargoM}->{minHL:0.0000}, trans@B={geoM.BAnchoM}->{minHT:0.0000}), vertical mantiene 0.0030");
        else { fallos++; Console.WriteLine("  [FAIL] Muro: horizontal por debajo de la tabla o vertical por debajo de 0.0030"); }
    }
}

// ============================================================================
// Diámetro de barra por elemento + blindaje normativo (2026-08-29) -- cierre de
// C.23-C.7.12.2.2 (tamaño mínimo de barra No.4) y del detallado por diámetro
// FIJO elegido por el usuario (en vez de auto-seleccionar la barra más delgada).
// ============================================================================
{
    Console.WriteLine("=== Diámetro de barra por elemento (blindaje C.23-C.7.12.2.2, detallado por diámetro fijo) ===");

    // 1) La No.3 (9.5 mm) queda EXCLUIDA del catálogo; el mínimo es No.4 (12.7 mm).
    totalAserciones++;
    var catalogoBlindado = CatalogoBarras.DiametrosComercialesMm.Length > 0
        && Math.Abs(CatalogoBarras.DiametrosComercialesMm[0] - CatalogoBarras.DiametroMinimoBarraMuroLosaMm) < 1e-9
        && CatalogoBarras.DiametrosComercialesMm.All(db => db >= CatalogoBarras.DiametroMinimoBarraMuroLosaMm - 1e-9)
        && !CatalogoBarras.EsDiametroValido(9.5);
    if (catalogoBlindado) Console.WriteLine("  [OK  ] Catalogo sin No.3 (9.5mm): minimo No.4 (12.7mm) -- C.23-C.7.12.2.2");
    else { fallos++; Console.WriteLine("  [FAIL] El catalogo admite No.3 o un diámetro por debajo de No.4"); }

    totalAserciones++;
    try { CatalogoBarras.ValidarDiametroBarra(9.5); fallos++; Console.WriteLine("  [FAIL] ValidarDiametroBarra(9.5) no lanzo"); }
    catch (ArgumentOutOfRangeException) { Console.WriteLine("  [OK  ] ValidarDiametroBarra(9.5) lanza ArgumentOutOfRangeException (No.3 excluida)"); }

    // 2) El detallado usa el diámetro FIJO elegido (No.5 =15.9mm por defecto), no la barra más
    //    delgada; y un diámetro explícito se respeta.
    {
        var geoC = new Geometria(BAnchoM: 4.5, LLargoM: 6.0, HtAlturaM: 3.8, ConTapa: true, EmEspesorMuroM: 0.25, EfEspesorFondoM: 0.20, EtEspesorTapaM: 0.20, HLAlturaLiquidoM: 3.0, HmAlturaSueloSobreMuroM: 3.0, WextSobrecargaKNm2: 0.0);
        var matC = new Materiales(FcMPa: 21, FyMPa: 420, GammaConcretoKNm3: 24, GammaLiquidoKNm3: 9.81, GammaSueloKNm3: 16, PhiGradosAnguloFriccionSuelo: 30);
        var proyectoC = new ProyectoTanque(geoC, matC);
        var cargasC = CargasGravitacionales.Calcular(proyectoC, cargaVivaCubiertaKNm2: 1.5, cargaAdicionalCubiertaKNm2: 0.5);

        var cub = DisenoPlacas.DisenarPlacaCubierta(proyectoC, cargasC, cvKNm2: 1.5, cgKNm2: 0.5);
        totalAserciones++;
        var detalladoFijo = cub.MxPositivo.Flexion.DiametroBarraMm is double db
            && Math.Abs(db - CatalogoBarras.DiametroPredeterminadoBarraMm) < 1e-9
            && cub.MxPositivo.Flexion.SeparacionM is double s
            && s <= CatalogoBarras.EspaciamientoMaximoMuroM + 1e-9
            && Math.Abs(cub.MxPositivo.Flexion.AsRequeridoMm2 - CatalogoBarras.AreaBarraMm2(db) / s) < 1e-6;
        if (detalladoFijo)
            Console.WriteLine($"  [OK  ] Detallado por diámetro fijo: cubierta Mx+ Ø{cub.MxPositivo.Flexion.DiametroBarraMm:0.#}mm @ {cub.MxPositivo.Flexion.SeparacionM * 1000:0}mm (As == área(Ø)/s)");
        else { fallos++; Console.WriteLine($"  [FAIL] Detallado no usa el diámetro fijo elegido: db={cub.MxPositivo.Flexion.DiametroBarraMm}, s={cub.MxPositivo.Flexion.SeparacionM}"); }

        // Con un diámetro explícito No.4 (12.7mm), el detallado debe respetarlo.
        var cubN4 = DisenoPlacas.DisenarPlacaCubierta(proyectoC, cargasC, cvKNm2: 1.5, cgKNm2: 0.5, diametroBarraMm: CatalogoBarras.DiametroMinimoBarraMuroLosaMm);
        totalAserciones++;
        var respetaN4 = cubN4.MxPositivo.Flexion.DiametroBarraMm is double db4
            && Math.Abs(db4 - CatalogoBarras.DiametroMinimoBarraMuroLosaMm) < 1e-9;
        if (respetaN4) Console.WriteLine("  [OK  ] Diámetro explícito No.4 respetado en el detallado");
        else { fallos++; Console.WriteLine($"  [FAIL] Diámetro explícito No.4 no respetado: db={cubN4.MxPositivo.Flexion.DiametroBarraMm}"); }

        // Blindaje: un diámetro fuera del catálogo se rechaza en el módulo de diseño.
        totalAserciones++;
        try { DisenoPlacas.DisenarPlacaCubierta(proyectoC, cargasC, 1.5, 0.5, diametroBarraMm: 16.0); fallos++; Console.WriteLine("  [FAIL] diametroBarraMm=16.0 (no comercial) no lanzo"); }
        catch (ArgumentOutOfRangeException) { Console.WriteLine("  [OK  ] diametroBarraMm=16.0 (no comercial) lanza ArgumentOutOfRangeException en el módulo de diseño"); }
    }

    // 3) Diámetro insuficiente (No.4 con Mu grande): NO CUMPLE + sugerencia del diámetro superior.
    {
        var matX = new Materiales(FcMPa: 21, FyMPa: 420, GammaConcretoKNm3: 24, GammaLiquidoKNm3: 9.81, GammaSueloKNm3: 16, PhiGradosAnguloFriccionSuelo: 30);
        var ecX = 4700.0 * Math.Sqrt(matX.FcMPa);
        var (flexionInsuf, _) = DisenoFlexionCortanteFisuracion.DisenarFlexionConControlFisuracion(
            100.0, 60.0, 0.15, 1.0, matX.FyMPa, matX.FcMPa, 200000.0, ecX, 0.20,
            diametroBarraMm: CatalogoBarras.DiametroMinimoBarraMuroLosaMm,
            cuantiaMinima: DisenoFlexionCortanteFisuracion.CuantiaMinimaMuroTanque,
            espaciamientoMaximoM: CatalogoBarras.EspaciamientoMaximoMuroM);
        totalAserciones++;
        var noCumpleBienSeñalado = flexionInsuf.DetalladoInsuficiente
            && flexionInsuf.DiametroSugeridoMm is double sug
            && Math.Abs(sug - CatalogoBarras.DiametroSiguienteMayor(CatalogoBarras.DiametroMinimoBarraMuroLosaMm)!.Value) < 1e-9;
        if (noCumpleBienSeñalado)
            Console.WriteLine($"  [OK  ] Diámetro No.4 insuficiente: DetalladoInsuficiente=true y sugiere Ø{flexionInsuf.DiametroSugeridoMm:0.#} mm (NO CUMPLE, nunca ocultar)");
        else { fallos++; Console.WriteLine($"  [FAIL] No se señaló el diámetro insuficiente: DetalladoInsuficiente={flexionInsuf.DetalladoInsuficiente}, sugerido={flexionInsuf.DiametroSugeridoMm}"); }
    }

    // 4) Cierre del hueco: el muro gobernado por sismo ahora TAMBIÉN resuelve su detallado Ø/s.
    //     B/L desplazadas +em para que, con luz EJE A EJE, los ratios del Capítulo 3 Caso 7 sigan
    //     tabulados: (L-em)/HL=(6.25-0.25)/3.0=2.0, (B-em)/HL=(4.75-0.25)/3.0=1.5.
    {
        var geoM = new Geometria(BAnchoM: 4.75, LLargoM: 6.25, HtAlturaM: 3.5, ConTapa: false, EmEspesorMuroM: 0.25, EfEspesorFondoM: 0.20, EtEspesorTapaM: 0.0, HLAlturaLiquidoM: 3.0, HmAlturaSueloSobreMuroM: 3.0, WextSobrecargaKNm2: 0.0);
        var matM = new Materiales(FcMPa: 21, FyMPa: 420, GammaConcretoKNm3: 24, GammaLiquidoKNm3: 9.81, GammaSueloKNm3: 16, PhiGradosAnguloFriccionSuelo: 30);
        var proyectoM = new ProyectoTanque(geoM, matM);
        var presionesM = PresionesLaterales.Calcular(proyectoM);
        var espectroM = new ParametrosEspectroDiseno(Aa: 0.2, Av: 0.2, Fa: 1.3, Fv: 2.0, I: 1.0, CondicionBase: CondicionBaseMuro.Rigida, CondicionAnclaje: CondicionAnclajeBase.ArticuladaEmpotrada);
        var sueloM = new ParametrosSueloDinamico(KhCoeficienteSismicoHorizontal: 0.2, KvCoeficienteSismicoVertical: 0.0, DeltaGradosFriccionSueloMuro: 0.0, IGradosInclinacionRelleno: 0.0, BetaGradosInclinacionMuro: 90.0);
        var sismoHidroM = FuerzaSismicaHidrodinamica.Calcular(proyectoM, espectroM);
        var sismoSueloM = FuerzaDinamicaSuelo.Calcular(proyectoM, sueloM);
        var disenoM = DisenoMuros.DisenarMuroLongitudinal(proyectoM, presionesM, sismoHidroM, sismoSueloM);

        var dirsM = new[] { disenoM.VerticalPositivo, disenoM.VerticalNegativo, disenoM.HorizontalPositivo, disenoM.HorizontalNegativo };
        var sismicas = dirsM.Count(dd => dd.Fisuracion is null);
        totalAserciones++;
        var todasConDetallado = dirsM.All(dd => dd.Flexion.DiametroBarraMm is not null && dd.Flexion.SeparacionM is not null);
        if (todasConDetallado)
            Console.WriteLine($"  [OK  ] Muro con sismo: las 4 direcciones tienen detallado Ø/s resuelto ({sismicas} gobernadas por sismo, antes sin detallado -- hueco cerrado)");
        else { fallos++; Console.WriteLine("  [FAIL] Muro con sismo: alguna direccion quedo sin detallado Ø/s"); }
    }
}

// ============================================================================
// Acotado de fs,adm (NSR-10 C.23-C.10.6.4.1) -- tope 250 MPa + piso 140/170 MPa. El valor crudo
// de la ecuación (C.23-2) se limita: por ARRIBA a 250 MPa (antes se permitía fs hasta ~305 MPa a
// separaciones densas, del lado INSEGURO) y por ABAJO al piso 140/170 (antes se rechazaban diseños
// con separaciones amplias que la norma admite). Cruce normativo 2026-08-30.
// ============================================================================
{
    Console.WriteLine("=== Acotado de fs,adm (C.23-C.10.6.4.1): tope 250 MPa + piso 140/170 MPa ===");
    AssertTol("fs,adm acotado ARRIBA a 250 MPa (s=75mm, Ø15.9mm)", DisenoFlexionCortanteFisuracion.CalcularFsAdmisible(0.075, 0.25, 15.9), 250.0, atol: 1e-6);
    AssertTol("fs,adm PISADO a 140 MPa (una dirección, s=1.0m)", DisenoFlexionCortanteFisuracion.CalcularFsAdmisible(1.0, 0.25, 25.0), 140.0, atol: 1e-6);
    AssertTol("fs,adm PISADO a 170 MPa (dos direcciones, s=1.0m)", DisenoFlexionCortanteFisuracion.CalcularFsAdmisible(1.0, 0.25, 25.0, DisenoFlexionCortanteFisuracion.FsAdmisibleMinimoDosDireccionesMPa), 170.0, atol: 1e-6);
    AssertTol("fs,adm SIN alterar dentro de [140,250] (s=0.15m)", DisenoFlexionCortanteFisuracion.CalcularFsAdmisible(0.15, 0.25, 25.0), 216.24, atol: 0.01);
}

// ============================================================================
// A.2 (MetodoInterpolacion en Muros Sísmico) + A.3 (notas opcionales C.23-C.7.12.2.1).
// ============================================================================
{
    Console.WriteLine("=== A.2 RedondearSuperior en Muros Sísmico + A.3 notas opcionales de retracción/temperatura ===");
    // A.2: en b/a=1.25 (no tabulado en la grilla del Cap.3), RedondearSuperior difiere de Interpolar.
    var sismoRedondeado = MurosRectangularesSismico.Calcular(1.25, 0.5, 1.0, 1.0, MetodoInterpolacion.RedondearSuperior);
    var sismoInterpolado = MurosRectangularesSismico.Calcular(1.25, 0.5, 1.0, 1.0, MetodoInterpolacion.Interpolar);
    totalAserciones++;
    if (Math.Abs(sismoRedondeado.LadoLargo.MxPosGobernanteKNmM - sismoInterpolado.LadoLargo.MxPosGobernanteKNmM) > 1e-9)
        Console.WriteLine("  [OK  ] A.2: Muros Sísmico RedondearSuperior difiere de Interpolar en b/a=1.25 (no tabulado)");
    else { fallos++; Console.WriteLine("  [FAIL] A.2: RedondearSuperior coincide con Interpolar en un punto no tabulado de la grilla Cap.3"); }

    var sismoExactoR = MurosRectangularesSismico.Calcular(2.0, 0.5, 1.0, 1.0, MetodoInterpolacion.RedondearSuperior);
    var sismoExactoI = MurosRectangularesSismico.Calcular(2.0, 0.5, 1.0, 1.0, MetodoInterpolacion.Interpolar);
    AssertTol("A.2: RedondearSuperior coincide con Interpolar en b/a=2.0 exacto", sismoExactoR.LadoLargo.MxPosGobernanteKNmM, sismoExactoI.LadoLargo.MxPosGobernanteKNmM, atol: 1e-9);

    // A.3: la cuantía mínima se reduce 50% en cara inferior sobre suelo, y con capa 300mm si ≥ 600mm.
    AssertTol("A.3: 50% en cara inferior sobre suelo", CatalogoBarras.AjustarCuantiaMinimaRetracionTemperatura(0.0030, 0.35, true), 0.0015, atol: 1e-9);
    AssertTol("A.3: capa 300mm para espesor ≥ 600mm", CatalogoBarras.AjustarCuantiaMinimaRetracionTemperatura(0.0030, 0.60, false), 0.0015, atol: 1e-9);
}

// ============================================================================
// C.23-C.10.5/C.10.6 contra placas (cruce normativo 2026-09-02, cierre del punto 13(b) de
// "Pendientes técnicos concretos"). Cadena normativa: C.10.5.4 remite el mínimo de LOSAS de
// espesor uniforme a C.7.12 (no C.10.5.1); en estructuras ambientales C.23-C.7.12.2.1 exige la
// cuantía FUNCIÓN de la distancia entre juntas (>=0.0030 para fy=420; implementado en
// DisenoPlacas+CatalogoBarras, cruce 2026-08-29); C.23-C.10.5.3 (excepción As >= 1.33 x As,análisis
// sin incluir Sd) no aplica porque C.7.12 gobierna; y C.23-C.10.6 (distribución del refuerzo,
// fs,máx) ya está implementado en la ec. C.23-2/C.23-3 (ver sección de fisuración). Verificación:
// con carga mínima, la cuantía diseñada de cada dirección nunca cae por debajo del mínimo
// C.23-C.7.12.2.1 de la dirección correspondiente.
// ============================================================================
{
    Console.WriteLine("=== C.23-C.10.5/C.10.6 vs placas: mínimo C.23-C.7.12.2.1 (C.10.5.4 -> C.7.12) ===");
    var pMin = new ProyectoTanque(
        new Geometria(BAnchoM: 4.0, LLargoM: 5.0, HtAlturaM: 4.0, ConTapa: true,
            EmEspesorMuroM: 0.3, EfEspesorFondoM: 0.35, EtEspesorTapaM: 0.2,
            HLAlturaLiquidoM: 3.5, HmAlturaSueloSobreMuroM: 3.0, WextSobrecargaKNm2: 0.0,
            Tipo: TipoTanque.EnterradoSinNivelFreatico, AlturaNivelFreaticoM: null),
        new Materiales(FcMPa: 21, FyMPa: 420, GammaConcretoKNm3: 24, GammaLiquidoKNm3: 9.81,
            GammaSueloKNm3: 16, PhiGradosAnguloFriccionSuelo: 30));
    var cargasMin = CargasGravitacionales.Calcular(pMin, 0.5, 0.0);
    var disenoMin = DisenoPlacas.DisenarPlacaCubierta(pMin, cargasMin, 0.5, 0.0);
    var gMin = pMin.Geometria;
    var fyMin = pMin.Materiales.FyMPa;
    var minMxC = CatalogoBarras.CuantiaMinimaRetracionTemperatura(gMin.LLargoM, fyMin);
    var minMyC = CatalogoBarras.CuantiaMinimaRetracionTemperatura(gMin.BAnchoM, fyMin);
    totalAserciones++;
    bool cumpleMin = disenoMin.MxPositivo.Flexion.Rho >= minMxC - 1e-9
                  && disenoMin.MxNegativo.Flexion.Rho >= minMxC - 1e-9
                  && disenoMin.MyPositivo.Flexion.Rho >= minMyC - 1e-9
                  && disenoMin.MyNegativo.Flexion.Rho >= minMyC - 1e-9;
    if (cumpleMin)
        Console.WriteLine($"  [OK  ] Placas: cuantía diseñada ({disenoMin.MxPositivo.Flexion.Rho:0.0000}) >= mínimo C.23-C.7.12.2.1 ({minMxC:0.0000}/{minMyC:0.0000}) en las 4 direcciones");
    else { fallos++; Console.WriteLine("  [FAIL] Placas: cuantía por debajo del mínimo C.23-C.7.12.2.1 de alguna dirección"); }
}

Console.WriteLine();
Console.WriteLine($"=== {totalAserciones - fallos}/{totalAserciones} aserciones OK ===");

if (fallos > 0)
{
    Console.WriteLine($"FALLARON {fallos} aserciones.");
    return 1;
}
Console.WriteLine("Todas las aserciones pasaron.");
return 0;
