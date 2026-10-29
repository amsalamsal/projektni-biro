/* Shared helpers for Site Design Pro dialogs. Works without network access. */
(function () {
  'use strict';
  var sdp = window.sdp = window.sdp || {};

  sdp.call = function (name, data) {
    if (window.sketchup && typeof window.sketchup[name] === 'function') {
      window.sketchup[name](data === undefined ? '' : JSON.stringify(data));
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

  sdp.fmt = function (v, digits) {
    if (typeof v !== 'number') { return sdp.esc(v); }
    return v.toLocaleString(undefined, { maximumFractionDigits: digits === undefined ? 3 : digits });
  };

  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape') { sdp.call('close'); }
  });
  window.addEventListener('DOMContentLoaded', function () { sdp.call('ready'); });
})();
