// Renders preview.png for every asset folder that has a mesh.json (WebGL in headless Chromium via Playwright).
// Usage: node tools/assets/render_previews.js <library-dir> [size] [asset-id ...]
// Needs three.js (npm install in tools/assets) – a development tool only, not shipped with the extension.
const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');

const lib = process.argv[2];
const size = parseInt(process.argv[3] || '384', 10);
const only = process.argv.slice(4);
const threePath = [__dirname, process.cwd()].map((d) => path.join(d, 'node_modules', 'three', 'build', 'three.min.js')).find((f) => fs.existsSync(f));
if (!threePath) { console.error('three.js nije instaliran: cd tools/assets && npm install'); process.exit(2); }

const page_html = `<!DOCTYPE html><html><head><meta charset="utf-8"><style>html,body{margin:0;background:#f4f5f2}</style>
<script>${fs.readFileSync(threePath, 'utf8')}</script></head><body><canvas id="c" width="${size}" height="${size}"></canvas><script>
const renderer = new THREE.WebGLRenderer({ canvas: document.getElementById('c'), antialias: true, preserveDrawingBuffer: true });
renderer.setSize(${size}, ${size}); renderer.shadowMap.enabled = true; renderer.shadowMap.type = THREE.PCFSoftShadowMap;
renderer.outputColorSpace = THREE.SRGBColorSpace;
window.renderAsset = (data) => {
  const scene = new THREE.Scene(); scene.background = new THREE.Color(0xf4f5f2);
  const root = new THREE.Group(); root.rotation.x = -Math.PI / 2; scene.add(root); // Z up -> Y up
  const box = new THREE.Box3();
  for (const key of Object.keys(data)) {
    const m = data[key]; const pos = [];
    for (const [outer, holes] of m.p) {
      // triangulate in the polygon plane (handles concave outlines and holes)
      const all = [outer, ...holes]; const a = outer;
      let nx = 0, ny = 0, nz = 0;
      for (let i = 0; i < a.length; i++) { const p = a[i], q = a[(i + 1) % a.length];
        nx += (p[1] - q[1]) * (p[2] + q[2]); ny += (p[2] - q[2]) * (p[0] + q[0]); nz += (p[0] - q[0]) * (p[1] + q[1]); }
      const n = new THREE.Vector3(nx, ny, nz).normalize();
      const u = new THREE.Vector3(Math.abs(n.z) < 0.9 ? 0 : 1, 0, Math.abs(n.z) < 0.9 ? 1 : 0).cross(n).normalize();
      const v = n.clone().cross(u);
      const to2 = (p) => new THREE.Vector2(u.x * p[0] + u.y * p[1] + u.z * p[2], v.x * p[0] + v.y * p[1] + v.z * p[2]);
      let tris;
      if (a.length === 3 && holes.length === 0) tris = [[0, 1, 2]];
      else tris = THREE.ShapeUtils.triangulateShape(outer.map(to2), holes.map(h => h.map(to2)));
      const flat = all.flat();
      for (const t of tris) for (const i of t) pos.push(...flat[i]);
    }
    if (!pos.length) continue;
    const g = new THREE.BufferGeometry(); g.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3)); g.computeVertexNormals();
    const c = m.c; const mat = new THREE.MeshStandardMaterial({ color: new THREE.Color(c[0], c[1], c[2]).convertSRGBToLinear(), roughness: 0.85, metalness: 0.05,
      transparent: c[3] < 1, opacity: c[3], side: THREE.DoubleSide, flatShading: false });
    const mesh = new THREE.Mesh(g, mat); mesh.castShadow = true; mesh.receiveShadow = true; root.add(mesh);
  }
  root.updateMatrixWorld(true); box.setFromObject(root);
  const size = box.getSize(new THREE.Vector3()); const centre = box.getCenter(new THREE.Vector3());
  const r = Math.max(size.length() / 2, 0.05);
  const ground = new THREE.Mesh(new THREE.PlaneGeometry(r * 12, r * 12), new THREE.ShadowMaterial({ opacity: 0.18 }));
  ground.rotation.x = -Math.PI / 2; ground.position.y = box.min.y - 0.002; ground.receiveShadow = true; scene.add(ground);
  scene.add(new THREE.HemisphereLight(0xffffff, 0xb8b0a0, 1.6));
  const sun = new THREE.DirectionalLight(0xffffff, 2.2); sun.position.set(centre.x - r * 2, centre.y + r * 4, centre.z + r * 3);
  sun.target.position.copy(centre); sun.castShadow = true; sun.shadow.mapSize.set(2048, 2048);
  const sc = sun.shadow.camera; sc.left = sc.bottom = -r * 3; sc.right = sc.top = r * 3; sc.near = 0.01; sc.far = r * 20;
  scene.add(sun); scene.add(sun.target);
  const cam = new THREE.PerspectiveCamera(30, 1, r / 100, r * 100);
  const dir = new THREE.Vector3(-1.0, 0.75, 1.25).normalize();
  cam.position.copy(centre).addScaledVector(dir, r / Math.sin(THREE.MathUtils.degToRad(15)) * 1.08);
  cam.lookAt(centre);
  renderer.render(scene, cam);
  scene.traverse(o => { if (o.geometry) o.geometry.dispose(); if (o.material) o.material.dispose(); });
  return true;
};
</script></body></html>`;

(async () => {
  const browser = await chromium.launch({ args: ['--use-gl=swiftshader', '--enable-webgl', '--ignore-gpu-blocklist'] });
  const page = await browser.newPage({ viewport: { width: size, height: size } });
  await page.setContent(page_html);
  let n = 0;
  for (const folder of fs.readdirSync(lib)) {
    const fdir = path.join(lib, folder);
    if (!fs.statSync(fdir).isDirectory()) continue;
    for (const id of fs.readdirSync(fdir)) {
      if (only.length && !only.includes(id)) continue;
      const meshFile = path.join(fdir, id, 'mesh.json');
      if (!fs.existsSync(meshFile)) continue;
      const data = JSON.parse(fs.readFileSync(meshFile, 'utf8'));
      await page.evaluate((d) => window.renderAsset(d), data);
      await page.locator('#c').screenshot({ path: path.join(fdir, id, 'preview.png') });
      n++;
    }
  }
  await browser.close();
  console.log(`Pregleda: ${n}`);
})();
