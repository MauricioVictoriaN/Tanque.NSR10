// OCR_PCA setup: descarga tesseract.js (entrada Node) + dependencias + modelo eng en lib/.
// Uso: node setup.js   (una sola vez; lib/ es generado y desechable)
'use strict';
const fs = require('fs');
const path = require('path');
const https = require('https');
const zlib = require('zlib');

const LIB = path.join(__dirname, 'lib');
const PKG = path.join(LIB, 'pkg');                 // árbol src/ de tesseract.js
const NODE_MODULES = path.join(PKG, 'node_modules'); // dependencias
const TESSDATA = path.join(LIB, 'tessdata');       // modelos de lenguaje

const TESSERACT_VERSION = '5.1.1';
const CORE_VERSION = '5.1.0';

// dependencias directas + transitivas de tesseract.js@5.1.1 (CommonJS/Node)
const DEPS = [
  ['regenerator-runtime', '0.13.11'],
  ['node-fetch', '2.7.0'],
  ['whatwg-url', '5.0.0'],
  ['tr46', '0.0.3'],
  ['webidl-conversions', '3.0.1'],
  ['is-url', '1.2.4'],
  ['is-electron', '2.2.2'],
  ['wasm-feature-detect', '1.5.1'],
  ['bmp-js', '0.1.0'],
  ['idb-keyval', '6.2.1'],
  ['tesseract.js-core', CORE_VERSION],
];

function getBin(url) {
  return new Promise((resolve, reject) => {
    const chunks = [];
    https.get(url, { headers: { 'User-Agent': 'Mozilla/5.0' } }, (res) => {
      if (res.statusCode >= 300 && res.statusCode < 400 && res.headers.location) {
        getBin(res.headers.location).then(resolve).catch(reject);
        return;
      }
      if (res.statusCode !== 200) { reject(new Error(`${url} -> HTTP ${res.statusCode}`)); return; }
      res.on('data', (c) => chunks.push(c));
      res.on('end', () => resolve(Buffer.concat(chunks)));
    }).on('error', reject);
  });
}

// Extrae un tarball npm (tgz) quitando el prefijo "package/" de cada entrada.
function extractTgz(tgzBuf, destRoot) {
  const gun = zlib.gunzipSync(tgzBuf);
  let off = 0;
  while (off < gun.length) {
    const hdr = gun.slice(off, off + 512);
    if (hdr.every((b) => b === 0)) break;
    const name = hdr.slice(0, 100).toString('utf8').replace(/\0.*$/, '');
    const size = parseInt(hdr.slice(124, 136).toString('utf8').replace(/\0.*$/, '').trim(), 8) || 0;
    const type = hdr[156];
    if ((type === 48 || type === 0) && size > 0) {
      const content = gun.slice(off + 512, off + 512 + size);
      const dest = path.join(destRoot, name.replace(/^package\//, ''));
      fs.mkdirSync(path.dirname(dest), { recursive: true });
      fs.writeFileSync(dest, content);
    }
    off += 512 + Math.ceil(size / 512) * 512;
  }
}

async function main() {
  fs.mkdirSync(NODE_MODULES, { recursive: true });
  fs.mkdirSync(TESSDATA, { recursive: true });

  // 1) árbol src/ de tesseract.js (entrada Node)
  const tj = await getBin(`https://registry.npmjs.org/tesseract.js/-/tesseract.js-${TESSERACT_VERSION}.tgz`);
  extractTgz(tj, PKG);
  console.log('OK tesseract.js@' + TESSERACT_VERSION + ' (src/)');

  // 2) dependencias
  for (const [name, ver] of DEPS) {
    const tgz = await getBin(`https://registry.npmjs.org/${name}/-/${name}-${ver}.tgz`);
    extractTgz(tgz, path.join(NODE_MODULES, name));
    console.log('OK ' + name + '@' + ver);
  }

  // 3) modelo eng (LSTM, tessdata_fast) + versión gzipeada
  const trained = await getBin('https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/main/eng.traineddata');
  fs.writeFileSync(path.join(TESSDATA, 'eng.traineddata'), trained);
  fs.writeFileSync(path.join(TESSDATA, 'eng.traineddata.gz'), zlib.gzipSync(trained));
  console.log('OK eng.traineddata (' + (trained.length / 1024).toFixed(0) + ' KB)');

  // 4) patch is-electron (el sandbox ejecuta Node dentro de un host Electron; ver README)
  fs.writeFileSync(
    path.join(NODE_MODULES, 'is-electron', 'index.js'),
    'module.exports = function isElectron(){ return false; };\n'
  );
  console.log('OK patch is-electron');

  console.log('\nSetup completo. lib/ generado en: ' + LIB);
  console.log('Uso: node ocr_pages.js <archivo.png|directorio|rango> [dirSalida]');
}

main().catch((e) => { console.error('Error:', e.message); process.exit(1); });
