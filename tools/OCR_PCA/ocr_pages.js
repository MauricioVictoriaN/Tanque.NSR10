// OCR_PCA: OCR de páginas del manual PCA (renders PNG/JPG) con tesseract.js (Node).
// Uso:
//   node ocr_pages.js <archivo.png>                 -> .txt junto a la imagen
//   node ocr_pages.js <directorio>                  -> OCR de todas las imágenes del directorio
//   node ocr_pages.js p-173..p-188.png              -> rango (prueba .png y .jpg)
//   node ocr_pages.js <entrada> <dirSalida> [--lang eng] [--psm 3]
'use strict';
const fs = require('fs');
const path = require('path');

const LIB = path.join(__dirname, 'lib');

function usage() {
  console.log('Uso: node ocr_pages.js <archivo.png|directorio|rango> [dirSalida] [--lang eng] [--psm N]');
  process.exit(1);
}

function collectImages(input) {
  const images = [];
  // rango del estilo "p-173..p-188.png" o "dir/p-173..p-188"
  const m = /^(.*?)(\d+)\.\.(\d+)(\.[a-z]+)?$/i.exec(input);
  if (m) {
    const prefix = m[1];
    const start = parseInt(m[2], 10);
    const end = parseInt(m[3], 10);
    const ext = (m[4] || '').toLowerCase();
    for (let n = start; n <= end; n++) {
      const candidates = ext
        ? [prefix + n + ext]
        : [prefix + n + '.png', prefix + n + '.jpg', prefix + n + '.jpeg'];
      const found = candidates.find((c) => fs.existsSync(c));
      if (found) images.push(found);
    }
    return images;
  }
  if (fs.existsSync(input)) {
    const st = fs.statSync(input);
    if (st.isDirectory()) {
      return fs.readdirSync(input)
        .filter((f) => /\.(png|jpe?g)$/i.test(f))
        .sort()
        .map((f) => path.join(input, f));
    }
    return [input];
  }
  console.error('No existe la entrada: ' + input);
  return [];
}

async function main() {
  const argv = process.argv.slice(2);
  const pos = [];
  const opts = {};
  for (let i = 0; i < argv.length; i++) {
    if (argv[i] === '--lang') opts.lang = argv[++i];
    else if (argv[i] === '--psm') opts.psm = parseInt(argv[++i], 10);
    else pos.push(argv[i]);
  }
  if (pos.length === 0) usage();
  const input = pos[0];
  const outDir = pos[1] || (fs.existsSync(input) && fs.statSync(input).isDirectory() ? path.join(input, 'ocr_output') : path.dirname(input));

  const images = collectImages(input);
  if (images.length === 0) { console.error('No se encontraron imágenes.'); process.exit(1); }
  console.log('OCR de ' + images.length + ' página(s)…');

  // cwd = carpeta del script para que las rutas relativas del worker/langPath sean estables
  process.chdir(__dirname);

  const { createWorker } = require(path.join(LIB, 'pkg/src/index.js'));
  const worker = await createWorker(opts.lang || 'eng', 1, {
    workerPath: path.join(__dirname, 'lib/pkg/src/worker-script/node/index.js'),
    langPath: 'lib/tessdata',
  });
  if (opts.psm) await worker.setParameters({ psm: opts.psm });

  fs.mkdirSync(outDir, { recursive: true });
  for (const img of images) {
    try {
      const buf = fs.readFileSync(img); // Buffer: evita que is-url lea "D:/…" como URL
      const { data } = await worker.recognize(buf);
      const out = path.join(outDir, path.basename(img).replace(/\.(png|jpe?g)$/i, '') + '.txt');
      fs.writeFileSync(out, data.text);
      console.log('OK  ' + path.basename(img) + ' -> ' + path.relative(__dirname, out) + ' (' + data.text.length + ' chars)');
    } catch (e) {
      console.error('FAIL ' + path.basename(img) + ': ' + e.message);
    }
  }
  await worker.terminate();
  console.log('Listo. Revisar SIEMPRE el OCR contra la página fuente (ver README).');
}

main().catch((e) => { console.error('Error:', e.message); process.exit(1); });
