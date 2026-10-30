// Loads every model.dae of a library with three.js ColladaLoader (an independent COLLADA reader) in headless
// Chromium and compares its bounding box with the dimensions in asset.json. Exit code 1 on any mismatch.
// Usage: node tools/assets/check_dae.js <library-dir> [asset-id ...]
const fs = require('fs');
const path = require('path');
const http = require('http');
const { chromium } = require('playwright');

const lib = path.resolve(process.argv[2]);
const only = process.argv.slice(3);
const three = path.join(__dirname, 'node_modules', 'three');
const ext = path.resolve(__dirname, '..', '..', 'sketchup-site-design-pro', 'src', 'pb_site_design_pro');

const server = http.createServer((req, res) => {
  const u = decodeURIComponent(req.url.split('?')[0]);
  let f;
  if (u === '/') { res.setHeader('content-type', 'text/html'); return res.end(`<!DOCTYPE html><script type="importmap">{"imports":{"three":"/three/build/three.module.js","three/addons/":"/three/examples/jsm/"}}</script><script type="module">import * as THREE from 'three'; import { ColladaLoader } from 'three/addons/loaders/ColladaLoader.js'; window.check = async (url) => { const c = await new ColladaLoader().loadAsync(url); c.scene.updateMatrixWorld(true); const b = new THREE.Box3().setFromObject(c.scene, true); let meshes = 0; c.scene.traverse(o => { if (o.isMesh) meshes++; }); return { min: b.min.toArray(), max: b.max.toArray(), meshes }; }; window.ready = true;</script>`); }
  if (u.startsWith('/three/')) f = path.join(three, u.slice(7));
  else if (u.startsWith('/lib/')) f = path.join(lib, u.slice(5));
  else if (u.startsWith('/textures/')) f = path.join(ext, u.slice(1));
  if (!f || !fs.existsSync(f)) { res.statusCode = 404; return res.end(); }
  res.setHeader('content-type', f.endsWith('.js') ? 'text/javascript' : 'application/octet-stream');
  fs.createReadStream(f).pipe(res);
});

(async () => {
  await new Promise((r) => server.listen(0, r));
  const port = server.address().port;
  const browser = await chromium.launch();
  const page = await browser.newPage();
  await page.goto(`http://127.0.0.1:${port}/`);
  await page.waitForFunction(() => window.ready);
  let bad = 0; let n = 0;
  for (const folder of fs.readdirSync(lib)) {
    const fdir = path.join(lib, folder);
    if (!fs.statSync(fdir).isDirectory()) continue;
    for (const id of fs.readdirSync(fdir)) {
      if (only.length && !only.includes(id)) continue;
      const assetFile = path.join(fdir, id, 'asset.json');
      if (!fs.existsSync(assetFile)) continue;
      const a = JSON.parse(fs.readFileSync(assetFile, 'utf8'));
      const r = await page.evaluate((u) => window.check(u), `/lib/${folder}/${id}/model.dae`);
      // ColladaLoader converts Z-up to Y-up: x stays, y <- z, z <- -y
      const size = [r.max[0] - r.min[0], r.max[2] - r.min[2], r.max[1] - r.min[1]];
      const want = [a.dimensions.x, a.dimensions.y, a.dimensions.z];
      const ok = size.every((v, i) => Math.abs(v - want[i]) < 0.01) && r.meshes > 0;
      n++;
      if (!ok) { bad++; console.log(`NESKLAD ${id}: DAE ${size.map(v => v.toFixed(3))} / asset ${want} (meshes ${r.meshes})`); }
    }
  }
  await browser.close(); server.close();
  console.log(`DAE provjereno: ${n}, neusklađeno: ${bad}`);
  process.exit(bad ? 1 : 0);
})();
