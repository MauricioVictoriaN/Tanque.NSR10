# Tanque.NSR10

**Analysis and design tool for rectangular reinforced-concrete tanks under NSR-10, ACI 350.3 and the PCA manual.**

*Tanque.NSR10* is an academic software prototype that automates the analysis and design of rectangular reinforced-concrete water tanks. It solves the interaction between a long wall, a short wall and the base / cover slabs using the tabulated moment coefficients of the PCA *Rectangular Concrete Tanks* manual, the Colombian earthquake-resistant code **NSR-10 (Título C)** and **ACI 350.3** (seismic hydrodynamic effects). It is the software companion to a manuscript in preparation for submission to **engrXiv** (a preprint archive).

> **Academic / research use only.** Non-commercial. See the [LICENSE](LICENSE) and the *Disclaimer / EULA* below.

[![License: CC BY-NC-SA 4.0](https://img.shields.io/badge/License-CC%20BY--NC--SA%204.0-brightgreen)](LICENSE) · [![Manuscript: in preparation](https://img.shields.io/badge/Manuscript-in%20preparation%20(engrXiv)-purple)]() · [![GitHub](https://img.shields.io/badge/Repository-github.com-%2F-MauricioVictoriaN/Tanque.NSR10-blue)](https://github.com/MauricioVictoriaN/Tanque.NSR10)

---

## Status

| Item | Result |
|---|---|
| Verification suite | **822 assertions** (module-level cross-checks) |
| Unit tests | **144** (xUnit) |
| Build | **0 errors / 0 warnings** across 5 .NET projects |

---

## Features

- **Geometry**: axis-to-axis spans, liquid height, wall / cover / base thickness (with automatic minimums).
- **Loads**: hydrostatic, lateral soil (Ka), Housner equivalent seismic masses and dynamic earth pressure.
- **Analysis**: tabulated PCA / Marcus coefficients for asymmetric `b/a`, `c/a` with bilinear interpolation; seismic Case 7 (uniform load) with corrected out-of-domain bounds.
- **Design**: flexure, shear and flexural crack-control per NSR-10 (B.2.4 load factors, C.23 serviceability, minimum reinforcement), bar catalogue No.4–No.10 and durability cover.
- **Outputs**: professional **text report**, print-ready **HTML**, and a tidy **CSV** data table.

---

## Download & usage

The program is distributed **free of charge** for academic and research use. Releases are available at https://github.com/MauricioVictoriaN/Tanque.NSR10/releases. Each release provides a compiled **Windows application**, the packaged **calculation core** and example projects.

> The calculation core (`Tanque.Core`) is distributed as a **compiled, obfuscated binary**. Its source is not public.

Quick start:

1. Download the latest release asset and unzip it.
2. Run the executable and enter the tank geometry on the **"Datos de entrada"** tab.
3. Press **Calcular** and review the governing results, verdict and report (text / HTML / CSV).

---

## Reproducibility & verification

The worked **Example 1** of the PCA manual is reproduced with the program's own outputs (see the `casos_prueba/` folder). The automated verification suite (`tools/Tanque.Core.Verificacion`, 822 assertions) and the unit tests (144) are included and can be re-run against the published binary.

---

## License

This project is released under a **tiered license**:

| Component | License |
|---|---|
| Accessible source (application, documentation, examples, test data, verification suite, tests) | **CC BY-NC-SA 4.0** (attribution, non-commercial, share-alike) |
| Calculation core (`Tanque.Core`) | **Proprietary academic binary** — commercial use, reverse engineering and modification are **prohibited** |
| Preprint (manuscript) | **CC BY 4.0** |

See [LICENSE](LICENSE) and [DISCLAIMER_AND_EULA.md](DISCLAIMER_AND_EULA.md) for the full Disclaimer of warranty and Terms of Use (EULA).

---

## Academic use disclaimer

This is a **research and teaching prototype**. It is **not** a substitute for professional engineering judgement. Results must be independently verified by a licensed structural engineer before any construction, licensing or execution of civil works.

---

## Citation

If you use this software, please cite the manuscript (in preparation, to be submitted to engrXiv):

> Mauricio Javier Victoria Niño. *Tanque.NSR10: automated analysis and design of rectangular reinforced-concrete tanks under NSR-10, ACI 350.3 and the PCA manual.* Software companion to a manuscript in preparation for submission to **engrXiv** (2026). Repository: https://github.com/MauricioVictoriaN/Tanque.NSR10 — DOI to be assigned upon publication.

---

## Contact & source on request

**Mauricio Javier Victoria Niño** — Independent Researcher, Cali, Colombia  
Email: hidratecsa@gmail.com · ORCID: 0009-0003-4328-5691

The source of the calculation core is made available to **academic reviewers** upon justifiable request.
