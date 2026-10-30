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
    check((await page.textContent('#title')).includes('Primjer: stambeni kompleks'), 'title shows project');
    check((await page.$$('#actions tr')).length === 140, 'all 139 planned elements and the terrain listed');
    check((await page.textContent('#cards')).includes('Teren'), 'terrain change card shown');
    await page.fill('#filter', 'klupa');
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
    check(calls.some(c => c[0] === 'apply' && c[1].createScenes === true && c[1].backup === true), 'apply sends options (scenes, backup)');
    await page.click('button[data-for=package]');
    const pkgInfo = await page.textContent('#pkgInfo');
    check(pkgInfo.includes('520,15 m n.v.') && pkgInfo.includes('Postojeći teren'), 'datum and terrain shown with decimal comma');
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
    check((await page.textContent('#params')).includes('Dužina'), 'parameter labels in Bosnian');
    check((await page.textContent('#props')).includes('Proizvođač'), 'property labels in Bosnian');
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
    const typeCount = JSON.parse(fs.readFileSync(path.join(__dirname, '..', '..', '..', 'shared', 'catalog', 'element-types.json'), 'utf8')).types.length;
    check((await page.$$('#types tr')).length === typeCount, `all ${typeCount} types listed`);
    await page.fill('#search', 'drvo');
    const n = (await page.$$('#types tr')).length;
    check(n > 3 && n < 20, 'search filters types (' + n + ')');
    await page.fill('#search', 'tree');
    const ne = (await page.$$('#types tr')).length;
    check(ne > 3 && ne < 25, 'English names are searchable too (' + ne + ')');
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
    console.log('models.html');
    ({ page, errors } = await open(browser, 'models.html', 'models.json', { width: 1180, height: 760 }));
    check((await page.$$('#cats button')).length === 14, '14 library categories');
    check((await page.textContent('#cats')).includes('Zelenilo i sadnja'), 'Bosnian category names');
    await page.click('#cats button:nth-child(5)');
    const greenery = await page.$$('#grid .asset');
    check(greenery.length > 20, 'greenery category lists its models (' + greenery.length + ')');
    const imgOk = await page.waitForFunction(() => Array.from(document.querySelectorAll('#grid img')).slice(0, 6).every((i) => i.complete && i.naturalWidth > 0), null, { timeout: 5000 }).then(() => true, () => false);
    check(imgOk, 'preview images load from the local library');
    await page.click('#subs button:nth-child(2)');
    check((await page.$$('#grid .asset')).length < greenery.length, 'subcategory chips narrow the grid');
    await page.fill('#search', 'klupa');
    check((await page.$$('#grid .asset')).length >= 3, 'search finds benches across categories');
    await page.click('#grid .asset[data-id="furniture.bench"]');
    let mcalls = await page.evaluate(() => window.__calls);
    check(mcalls.some((c) => c[0] === 'details' && c[1].id === 'furniture.bench'), 'selecting a model requests its details');
    await page.evaluate((d) => window.sdp.render(d), JSON.parse(fs.readFileSync(path.join(FIX, 'models-details.json'), 'utf8')));
    const detail = await page.textContent('#detail');
    check(detail.includes('Veza s AutoCAD-om') && detail.includes('Licenca') && detail.includes('Izvor'), 'details show block link, source and licence');
    check((await page.$$('#detail .param')).length > 3, 'parameter form rendered');
    await page.fill('#p_length', '2.4');
    await page.click('#insert');
    mcalls = await page.evaluate(() => window.__calls);
    check(mcalls.some((c) => c[0] === 'insert' && c[1].id === 'furniture.bench' && c[1].params.length === 2.4), 'insert sends the asset and changed parameters');
    await page.click('#replace');
    mcalls = await page.evaluate(() => window.__calls);
    check(mcalls.some((c) => c[0] === 'replace' && c[1].id === 'furniture.bench'), 'replace sends the asset');
    await page.evaluate(() => window.sdp.render({ mode: 'message', kind: 'warning', text: '<b>x</b> preskočeno' }));
    check((await page.innerHTML('#detail')).includes('&lt;b&gt;x&lt;/b&gt;'), 'messages are HTML-escaped');
    check(errors.length === 0, 'no script errors' + (errors.length ? ': ' + errors.join('; ') : ''));
    if (shots) { await page.screenshot({ path: path.join(shots, 'models.png') }); }
    await page.close();
  } finally {
    await browser.close();
  }
  console.log(failures ? failures + ' UI check(s) failed' : 'all UI checks passed');
  process.exit(failures ? 1 : 0);
})();
