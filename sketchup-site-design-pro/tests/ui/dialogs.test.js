// Renders the SketchUp HtmlDialog pages in Chromium with a stubbed window.sketchup bridge and checks
// rendering, interaction callbacks, HTML escaping and the absence of script errors.
// Usage: node tests/ui/dialogs.test.js [screenshotDir]
'use strict';
const path = require('path');
const fs = require('fs');
const { chromium } = require('playwright');

const HTML = path.resolve(__dirname, '../../src/pb_site_design_pro/sketchup/ui/html');
const FIX = path.resolve(__dirname, 'fixtures');
const shots = process.argv[2];
let failures = 0;

function check(cond, msg) {
  if (cond) { console.log('  ok   ' + msg); } else { failures++; console.log('  FAIL ' + msg); }
}

async function open(browser, file, fixture, size) {
  const page = await browser.newPage({ viewport: size || { width: 900, height: 680 } });
  const errors = [];
  page.on('pageerror', (e) => errors.push(e.message));
  page.on('console', (m) => { if (m.type() === 'error') { errors.push(m.text()); } });
  await page.addInitScript((data) => {
    window.__calls = [];
    window.sketchup = new Proxy({}, { get: (_t, name) => (arg) => {
      window.__calls.push([name, arg ? JSON.parse(arg) : null]);
      if (name === 'ready') { setTimeout(() => window.sdp.render(data), 0); }
    } });
  }, JSON.parse(fs.readFileSync(path.join(FIX, fixture), 'utf8')));
  await page.goto('file://' + path.join(HTML, file));
  await page.waitForTimeout(150);
  return { page, errors };
}

(async () => {
  const browser = await chromium.launch();
  try {
    console.log('import.html (preview)');
    let { page, errors } = await open(browser, 'import.html', 'preview.json');
    check((await page.textContent('#title')).includes('Sample Residential Complex'), 'title shows project');
    check((await page.$$('#actions tr')).length === 139, 'all 139 planned creations listed');
    await page.fill('#filter', 'bench');
    check((await page.$$('#actions tr')).length === 4, 'filter narrows to 4 benches');
    await page.click('button[data-for=validation]');
    const issuesHtml = await page.innerHTML('#issues');
    check(issuesHtml.includes('&lt;script&gt;') && !(await page.$('#issues script')), 'messages are HTML-escaped');
    await page.selectOption('#lod', 'presentation');
    let calls = await page.evaluate(() => window.__calls);
    check(calls.some(c => c[0] === 'replan' && c[1].lod === 'presentation'), 'changing LOD requests a re-plan');
    await page.click('#apply');
    await page.waitForTimeout(80);
    calls = await page.evaluate(() => window.__calls);
    check(calls.some(c => c[0] === 'apply' && c[1].createScenes === true), 'apply sends options');
    check(errors.length === 0, 'no script errors' + (errors.length ? ': ' + errors.join('; ') : ''));
    if (shots) { await page.click('button[data-for=changes]'); await page.fill('#filter', ''); await page.screenshot({ path: path.join(shots, 'import-preview.png') }); }
    await page.close();

    console.log('import.html (report)');
    ({ page, errors } = await open(browser, 'import.html', 'report.json'));
    check((await page.textContent('#cards')).includes('139'), 'report shows created count');
    check(!(await page.isVisible('#apply')), 'import button hidden in report mode');
    await page.click('#save');
    calls = await page.evaluate(() => window.__calls);
    check(calls.some(c => c[0] === 'saveReport' && c[1].text.includes('SDP-R001')), 'save report sends text');
    check(errors.length === 0, 'no script errors');
    if (shots) { await page.screenshot({ path: path.join(shots, 'import-report.png') }); }
    await page.close();

    console.log('properties.html');
    ({ page, errors } = await open(browser, 'properties.html', 'properties.json', { width: 520, height: 640 }));
    check((await page.$$('.param')).length >= 8, 'parameter editors rendered');
    check((await page.$$('.param.edited')).length === 1, 'SketchUp edit marked');
    check((await page.inputValue('select[data-key=material]')) === 'timber', 'material parameters use a library picker');
    await page.fill('input[data-key=length]', '2.6');
    await page.uncheck('input[data-key=backrest]');
    await page.click('#apply');
    calls = await page.evaluate(() => window.__calls);
    const apply = calls.find(c => c[0] === 'apply');
    check(apply && apply[1].edits.length === 2.6 && apply[1].edits.backrest === false && Object.keys(apply[1].edits).length === 2, 'only changed values are sent');
    check(errors.length === 0, 'no script errors');
    if (shots) { await page.screenshot({ path: path.join(shots, 'properties.png') }); }
    await page.close();

    console.log('library.html');
    ({ page, errors } = await open(browser, 'library.html', 'library.json', { width: 920, height: 680 }));
    check((await page.$$('#types tr')).length === 254, 'all 254 types listed');
    await page.fill('#search', 'tree');
    const n = (await page.$$('#types tr')).length;
    check(n > 3 && n < 20, 'search filters types (' + n + ')');
    await page.fill('#search', '');
    await page.check('#onlyModel');
    check((await page.$$('#types tr')).length === 2, 'in-model filter');
    await page.click('button[data-map="furniture.bench"]');
    await page.click('button[data-fav="furniture.bench"]');
    calls = await page.evaluate(() => window.__calls);
    check(calls.some(c => c[0] === 'mapComponent' && c[1].type === 'furniture.bench'), 'assign component callback');
    check(calls.some(c => c[0] === 'saveSettings' && c[1].favorites.includes('furniture.bench')), 'favourite saved');
    await page.click('button[data-for=settings]');
    await page.selectOption('#deletedPolicy', 'delete');
    await page.click('#saveSettings');
    calls = await page.evaluate(() => window.__calls);
    check(calls.some(c => c[0] === 'saveSettings' && c[1].deletedPolicy === 'delete'), 'settings saved');
    check(errors.length === 0, 'no script errors');
    if (shots) { await page.uncheck('#onlyModel').catch(() => {}); await page.click('button[data-for=types]'); await page.screenshot({ path: path.join(shots, 'library.png') }); }
    await page.close();
  } finally {
    await browser.close();
  }
  console.log(failures ? failures + ' UI check(s) failed' : 'all UI checks passed');
  process.exit(failures ? 1 : 0);
})();
