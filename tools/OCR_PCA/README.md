# OCR_PCA — OCR reproducible de las páginas del manual PCA

Herramienta Node.js (sin dependencias nativas) para OCR de las páginas escaneadas del manual
**PCA "Rectangular Concrete Tanks"** (y de cualquier PDF renderizado a imágenes). Creada en la
**sesión 2026-09-02** para extraer los valores publicados del **"Example 1 — Rectangular Tank
Design"** (Capítulo 5, páginas físicas 173-198) y compararlos con la salida de `Tanque.Core`.

## Origen y uso real

- Los renders de las páginas se generan desde el PDF escaneado
  `PCA-rectangular-concrete-tanks.pdf` (sin capa de texto, 198 páginas). El2026-09-02 se limpió
  `pca_render/` (renders + artefactos intermedios) y `lib/` — ambos regenerables.
- Con esta herramienta se extrajo el texto de las páginas del Ejemplo 1 (págs. físicas 173-198) y
  se verificó la comparación registrada en `RUTA_TRABAJO_PROXIMAS_SESIONES.md` (entrada
  2026-09-02). Los textos OCR quedaron conservados como evidencia en `pca_ocr/`.

### Regenerar los renders (si se necesita re-OCR)

Requiere poppler (`pdftoppm`, opcional — no lo instala `setup.js`):

```bat
:: Página individual (200 dpi es suficiente para OCR):
pdftoppm -f 173 -l 198 -r 200 -png PCA-rectangular-concrete-tanks.pdf pca_render/p
:: Directorio de un capítulo:
pdftoppm -f 108 -l 132 -r 200 -png PCA-rectangular-concrete-tanks.pdf pca_render/ch3/p
```

> Los datos CONFIRMADOS de las tablas viven en `referencia_normativa/pca_manual/` (JSONs +
> `paginas_fuente/`); los renders solo se necesitan para re-leer páginas del manual.

## Requisitos

- Node.js **18+** (probado con Node 22).
- Red disponible la **primera vez** (`node setup.js` descarga ~20 MB a `lib/`).

## Instalación (una sola vez)

```bat
cd /d D:\R\Tanques_DS\tools\OCR_PCA
node setup.js
```

`setup.js` descarga en `lib/`:
- el árbol `src/` de `tesseract.js@5.1.1` (entrada Node, CommonJS);
- sus dependencias (`tesseract.js-core@5.1.0`, `node-fetch@2`, `regenerator-runtime`, etc.);
- el modelo de lenguaje **eng** (LSTM, `tessdata_fast`) + su versión `.gz`.

`lib/` es **generado y desechable**: se puede borrar y recrear con `node setup.js`.
Si el repositorio usa git, conviene ignorarlo (`tools/OCR_PCA/lib/`).

## Uso

> Los ejemplos suponen que los renders existen (regenerarlos con `pdftoppm`, ver arriba).

```bat
:: Una sola página
node ocr_pages.js D:\R\Tanques_DS\pca_render\p-173.png

:: Un rango de páginas (los archivos deben existir; probará .png y .jpg)
node ocr_pages.js D:\R\Tanques_DS\pca_render\p-173..p-188

:: Un directorio completo (todas las imágenes *.png/*.jpg, ordenadas)
node ocr_pages.js D:\R\Tanques_DS\pca_render\ch3

:: Salida a otro directorio + opciones
node ocr_pages.js p-173.png .\ocr --lang eng --psm 3
```

- Salida: un `.txt` por página, en el directorio de salida (por defecto, el mismo de la entrada).
- `--psm`: modo de segmentación de tesseract (`3` = AUTO, `6` = bloque único, `11` = texto
  disperso). Por defecto `3` (mejor para páginas mixtas de texto/tablas).
- `--lang`: idioma (por defecto `eng`; se puede ampliar `tessdata/` con otros `.traineddata`).
- La primera ejecución crea un caché `eng.traineddata` junto al script (4 MB, desechable).

## Detalles técnicos (por qué está así)

- **Entrada Node de tesseract.js** (`lib/pkg/src/index.js`), no el bundle de navegador: el bundle
  usa `importScripts`/`self` y no corre en Node.
- **Patch a `is-electron`** (lo aplica `setup.js`): el sandbox de sesión ejecuta Node dentro de un
  host Electron, y `is-electron()` devuelve `true`, lo que rompe la detección de entorno de
  tesseract.js (lo trata como 'electron' y llama a `node-fetch` con rutas locales). El patch fuerza
  `false` y es inocuo en Node plano.
- **Imágenes como `Buffer`**: se leen con `fs.readFileSync` y se pasan como buffer, porque
  `is-url` interpreta rutas Windows (`D:/…`, `C:/…`) como URLs y `node-fetch` falla con "Only
  absolute URLs are supported".
- **`langPath` relativo** (`lib/tessdata`): evita la misma trampa de `is-url` con la letra de
  unidad; el script hace `process.chdir(__dirname)` para que las rutas relativas del worker sean
  estables.
- El modelo se espera gzipeado (`eng.traineddata.gz`) porque tesseract.js lo pide con `.gz` por
  defecto.

## Limitaciones y principio rector del proyecto

- El OCR de **tablas numéricas densas es imperfecto** (dígitos/negativos se confunden). NUNCA se
  transcribe un valor OCR a código sin verificación: releer la página fuente y cruzar por doble
  vía (simetrías, anclas, restricciones físicas), como se hizo con las tablas del Capítulo 3.
- Para coeficientes de diseño ya hay datos **CONFIRMADOS** en
  `referencia_normativa/pca_manual/` (`coeficientes_caso3_muro_y_caso10_placa.json`,
  `pca_case3_chapter3.json`, `pca_case7_chapter3.json`) — usar esos JSON como fuente de código,
  no un OCR nuevo.
- Esta herramienta es auxiliar de verificación normativa (leer el manual), no un oráculo:
  el patrón de trabajo sigue siendo "la norma es la única fuente de verdad".
