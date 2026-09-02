PLAN DE PUBLICACIÓN EN GITHUB — TANQUE.NSR10
=======================================
Repositorio: https://github.com/MauricioVictoriaN/Tanque.NSR10
=========================================

MODELO (confirmado)
- Repositorio PÚBLICO: descarga y consulta libres.
- Código ACCESIBLE (interfaz, docs, ejemplos, datos de prueba, suite de verificación,
  tests): CC BY-NC-SA 4.0.
- MOTOR DE CÁLCULO (Tanque.Core): se publica como BINARIO OFUSCADO, NO como fuente.
  Su fuente se entrega a revisores académicos previa solicitud.
- Manuscrito (engrXiv): CC BY 4.0.

PUBLICAR EN EL REPO PÚBLICO (sí)
  [x] src/Tanque.App  (interfaz, XAML — CC BY-NC-SA)
  [x] src/Tanque.Reportes  (generadores de reportes — CC BY-NC-SA)
  [x] La DLL del núcleo: Tanque.Core.dll  (ofuscada, NO el fuente)
  [x] tests/Tanque.Core.Tests  (144 tests — CC BY-NC-SA)
  [x] tools/Tanque.Core.Verificacion  (suite 822 aserciones — CC BY-NC-SA)
  [x] casos_prueba/  (salida de ejemplo, reportes)
  [x] Manuscrito/  (PDF + figuras; manuscrito CC BY 4.0)
  [x] LICENSE, CONTRIBUTING.md, README.md, .gitignore
  [x] Descargo_Responsabilidad_y_EULA.md

NO PUBLICAR (no)
  [ ] El FUENTE de Tanque.Core  (se sustituye por la DLL ofuscada)
  [ ] bin/ obj/ *.dll intermedios, *.pdb, secretos, claves
  [ ] tools/OCR_PCA/lib/ (≈52 MB regenerable), pca_render/, .history/

PASOS
1. Repo PÚBLICO creado: https://github.com/MauricioVictoriaN/Tanque.NSR10 (el fuente del núcleo queda fuera).
2. Empaquetar Tanque.Core como NuGet/DLL ofuscada (herramienta de ofuscación +
   sign) — tarea de implementación pendiente.
3. Ajustar csproj de la App/Reportes para consumir la DLL ofuscada del núcleo.
4. Publicar Release con la app compilada + ejemplos + manuscrito.
5. Configurar LICENSE/README/CONTRIBUTING (ya listos).
6. En el README: sección "Licencia" + aclaración de binario ofuscado + cómo pedir
   el fuente del núcleo para revisión académica.
