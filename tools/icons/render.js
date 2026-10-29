// Rasterises shared/icons/*.svg with Chromium (Playwright):
//   AutoCAD:  autocad-site-design-pro/src/SiteDesignPro.AutoCAD/Resources/Icons/<name>_<16|32>[_dark].png
//   SketchUp: sketchup-site-design-pro/src/pb_site_design_pro/icons/<name>_<16|24>.png and <name>.svg
// Also writes an overview sheet (both themes) to the path given as the first argument, if any.
// Usage: NODE_PATH=$(npm root -g) node tools/icons/render.js [overview.png]
'use strict';
const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');

const root = path.resolve(__dirname, '../..');
const src = path.join(root, 'shared/icons');
const acad = path.join(root, 'autocad-site-design-pro/src/SiteDesignPro.AutoCAD/Resources/Icons');
const su = path.join(root, 'sketchup-site-design-pro/src/pb_site_design_pro/icons');
const themes = { light: '#3C4652', dark: '#E6EAF0' };

(async () => {
  fs.mkdirSync(acad, { recursive: true });
  fs.mkdirSync(su, { recursive: true });
  const names = fs.readdirSync(src).filter(f => f.endsWith('.svg')).map(f => f.slice(0, -4)).sort();
  const browser = await chromium.launch();
  const page = await browser.newPage();
  async function render(name, size, color, file) {
    const svg = fs.readFileSync(path.join(src, name + '.svg'), 'utf8').replace(/color="#3C4652"/, 'color="' + color + '"');
    await page.setViewportSize({ width: size, height: size });
    await page.setContent('<html><body style="margin:0;background:transparent"><div id="i" style="width:' + size + 'px;height:' + size + 'px">' +
      svg.replace(/width="32" height="32"/, 'width="' + size + '" height="' + size + '"') + '</div></body></html>');
    await page.locator('#i').screenshot({ path: file, omitBackground: true });
  }
  let count = 0;
  for (const name of names) {
    const isSu = name.startsWith('su_');
    if (isSu) {
      const base = name.slice(3);
      for (const size of [16, 24]) { await render(name, size, themes.light, path.join(su, base + '_' + size + '.png')); count++; }
      fs.copyFileSync(path.join(src, name + '.svg'), path.join(su, base + '.svg'));
    } else {
      for (const size of [16, 32]) {
        await render(name, size, themes.light, path.join(acad, name + '_' + size + '.png'));
        await render(name, size, themes.dark, path.join(acad, name + '_' + size + '_dark.png'));
        count += 2;
      }
    }
  }
  if (process.argv[2]) {
    const cell = (name, bg, color) => '<div style="display:inline-flex;flex-direction:column;align-items:center;width:84px;margin:4px;font:10px Arial;color:' + color + '">' +
      '<div>' + fs.readFileSync(path.join(src, name + '.svg'), 'utf8').replace(/color="#3C4652"/, 'color="' + color + '"') + '</div><div style="margin-top:3px">' + name + '</div></div>';
    const sheet = ['light', 'dark'].map(t => '<div style="background:' + (t === 'light' ? '#F0F0F0' : '#3B4453') + ';padding:8px">' +
      names.map(n => cell(n, t, themes[t])).join('') + '</div>').join('');
    await page.setViewportSize({ width: 1100, height: 400 });
    await page.setContent('<html><body style="margin:0;width:1100px">' + sheet + '</body></html>');
    await page.screenshot({ path: process.argv[2], fullPage: true });
  }
  await browser.close();
  console.log(count + ' PNG icons rendered (' + names.length + ' SVG sources).');
})();
