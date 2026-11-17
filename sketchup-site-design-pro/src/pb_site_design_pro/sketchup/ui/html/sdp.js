/* Zajedničke funkcije dijaloga Vanjsko uređenje PRO. Rade bez mrežnog pristupa. */
(function () {
  'use strict';
  var sdp = window.sdp = window.sdp || {};

  // SketchUp 2017+: HtmlDialog (window.sketchup.<name>); SketchUp 2014-2016: WebDialog (skp: address). The
  // WebDialog calls are queued, because a new skp: address cancels one that is still being handled.
  var queue = [], busy = false;
  window.sdpWebDialog = /[?&]sdpwd=1/.test(window.location.search);
  function next() {
    if (busy || queue.length === 0) { return; }
    busy = true;
    window.location.href = 'skp:' + queue.shift();
  }
  sdp.ack = function () { busy = false; setTimeout(next, 0); };
  sdp.call = function (name, data) {
    var json = data === undefined ? '' : JSON.stringify(data);
    if (window.sketchup && typeof window.sketchup[name] === 'function') {
      window.sketchup[name](json);
    } else if (window.sdpWebDialog) {
      queue.push(name + '@' + encodeURIComponent(json));
      next();
    } else if (window.console) {
      console.log('sketchup.' + name, data);
    }
  };

  sdp.esc = function (s) {
    return String(s === null || s === undefined ? '' : s)
      .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
  };

  sdp.$ = function (sel, root) { return (root || document).querySelector(sel); };
  sdp.$$ = function (sel, root) { return Array.prototype.slice.call((root || document).querySelectorAll(sel)); };

  sdp.tabs = function (root, onChange) {
    sdp.$$('.tabs button', root).forEach(function (b) {
      b.addEventListener('click', function () {
        sdp.$$('.tabs button', root).forEach(function (x) { x.classList.toggle('active', x === b); });
        sdp.$$('[data-tab]', root).forEach(function (p) { p.classList.toggle('hidden', p.getAttribute('data-tab') !== b.getAttribute('data-for')); });
        if (onChange) { onChange(b.getAttribute('data-for')); }
      });
    });
  };

  sdp.badge = function (cls, text) {
    return '<span class="badge b-' + sdp.esc(cls) + '">' + sdp.esc(text) + '</span>';
  };

  // Bosnian number format: decimal comma, no thousands separator, trailing zeros removed (518,56).
  sdp.fmt = function (v, digits) {
    if (typeof v !== 'number' || !isFinite(v)) { return sdp.esc(v); }
    var s = v.toFixed(digits === undefined ? 3 : digits);
    if (s.indexOf('.') >= 0) { s = s.replace(/0+$/, '').replace(/\.$/, ''); }
    if (s === '-0') { s = '0'; }
    return s.replace('.', ',');
  };

  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' || e.key === 'Esc' || e.keyCode === 27) { sdp.call('close'); }
  });
  window.addEventListener('DOMContentLoaded', function () { sdp.call('ready'); });
})();
