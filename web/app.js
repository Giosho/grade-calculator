(function () {
  'use strict';

  var $ = function (s, r) { return (r || document).querySelector(s); };
  var $$ = function (s, r) { return Array.prototype.slice.call((r || document).querySelectorAll(s)); };
  var NS = 'http://www.w3.org/2000/svg';
  var KEY = 'gradecalc.v1';

  // ---------- състояние ----------
  var state = { theme: 'auto', goal: 5.5, subjects: [] };
  try {
    var raw = localStorage.getItem(KEY);
    if (raw) {
      var d = JSON.parse(raw);
      if (d && typeof d === 'object') {
        state.theme = (d.theme === 'light' || d.theme === 'dark') ? d.theme : 'auto';
        state.goal = (d.goal >= 2 && d.goal <= 6) ? d.goal : 5.5;
        state.subjects = Array.isArray(d.subjects) ? d.subjects.map(function (s) { return { name: String(s.name || ''), grades: String(s.grades || '') }; }) : [];
      }
    }
  } catch (e) { }
  function save() { try { localStorage.setItem(KEY, JSON.stringify(state)); } catch (e) { } }

  // ---------- помощни ----------
  function esc(s) { return String(s).replace(/[&<>"']/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]; }); }
  function fmt(v) { return v.toFixed(2); }
  function fmtGrade(v) { return String(Math.round(v * 100) / 100); }
  function num(s) { var v = Number(String(s).trim().replace(',', '.')); return (String(s).trim() === '' || !isFinite(v)) ? NaN : v; }

  function parseGrades(text) {
    var out = [];
    var toks = String(text || '').split(/[\s;]+/).filter(Boolean);
    for (var i = 0; i < toks.length; i++) {
      var t = toks[i];
      if (/^[2-6](,[2-6])+$/.test(t)) { t.split(',').forEach(function (x) { out.push(Number(x)); }); continue; }
      var v = num(t);
      if (isNaN(v) || v < 2 || v > 6) return { ok: false, list: [] };
      out.push(v);
    }
    return { ok: true, list: out };
  }
  function sum(l) { return l.reduce(function (a, b) { return a + b; }, 0); }

  function rng(seed) { var s = (seed >>> 0) || 1; return function () { s = (s * 1664525 + 1013904223) >>> 0; return s / 4294967296; }; }

  // нарисувано на ръка кръгче (SVG, разтегливо)
  function circleSvg(seed, w, h) {
    var r = rng(seed * 7919 + 13), n = 44, pts = [];
    for (var i = 0; i <= n; i++) {
      var a = -0.6 + i * (2 * Math.PI + 1.0) / n;
      var k = 1 + 0.03 * Math.sin(i * 0.8 + seed) + 0.07 * i / n;
      var x = w / 2 + Math.cos(a) * (w * 0.46) * k + (r() - 0.5) * 0.015 * w;
      var y = h / 2 + Math.sin(a) * (h * 0.46) * k + (r() - 0.5) * 0.015 * h;
      pts.push(x.toFixed(1) + ' ' + y.toFixed(1));
    }
    return '<svg viewBox="0 0 ' + w + ' ' + h + '" preserveAspectRatio="none" aria-hidden="true"><path class="pen" transform="rotate(-5 ' + w / 2 + ' ' + h / 2 + ')" d="M' + pts.join(' L') + '"/></svg>';
  }
  function underlineSvg(seed, cls) {
    var r = rng(seed * 31 + 5), pts = [];
    for (var x = 0; x <= 200; x += 10) pts.push(x + ' ' + (4 + (r() - 0.5) * 2.4).toFixed(1));
    return '<svg class="uline" viewBox="0 0 200 8" preserveAspectRatio="none" aria-hidden="true"><path class="' + cls + '" d="M' + pts.join(' L') + '"/></svg>';
  }
  function decorateTitles() {
    $$('.title h1').forEach(function (h, i) {
      if (!h.querySelector('svg')) h.insertAdjacentHTML('beforeend', underlineSvg(i + 1, 'inkline') + '');
    });
  }

  // ---------- тема ----------
  var mq = window.matchMedia ? window.matchMedia('(prefers-color-scheme: dark)') : null;
  function applyTheme() {
    var dark = state.theme === 'dark' || (state.theme === 'auto' && mq && mq.matches);
    document.documentElement.setAttribute('data-theme', dark ? 'dark' : 'light');
    var m = document.querySelector('meta[name="theme-color"]');
    if (m) m.setAttribute('content', dark ? '#171b27' : '#fafcff');
  }
  if (mq && mq.addEventListener) mq.addEventListener('change', applyTheme);

  // ---------- табове ----------
  var TABS = ['calc', 'subj', 'stats', 'set'];
  function showTab(id, focus) {
    TABS.forEach(function (t) {
      var on = t === id;
      var b = $('#t-' + t), p = $('#tab-' + t);
      b.setAttribute('aria-selected', on ? 'true' : 'false');
      b.tabIndex = on ? 0 : -1;
      p.hidden = !on;
      if (on && focus) b.focus();
    });
    if (id === 'stats') renderStats();
    if (id === 'subj') renderOverall();
    try { history.replaceState(null, '', '#' + id); } catch (e) { }
    window.scrollTo(0, 0);
  }
  TABS.forEach(function (t, i) {
    $('#t-' + t).addEventListener('click', function () { showTab(t); });
    $('#t-' + t).addEventListener('keydown', function (e) {
      var j = null;
      if (e.key === 'ArrowRight') j = (i + 1) % TABS.length;
      else if (e.key === 'ArrowLeft') j = (i + TABS.length - 1) % TABS.length;
      if (j !== null) { e.preventDefault(); showTab(TABS[j], true); }
    });
  });

  // ================= КАЛКУЛАТОР =================
  var PRESETS = [3.0, 3.5, 4.5, 5.5, 6.0];
  var PRESET_TIPS = ['Среден 3', 'Добър 4', 'Много добър 5', 'Отличен 6 (от 5.50)', 'Шест'];
  var inGrades = $('#in-grades'), inAvg = $('#in-avg'), inCount = $('#in-count'), inTarget = $('#in-target');
  var summaryEl = $('#summary'), resultsEl = $('#results'), presetsEl = $('#presets');

  PRESETS.forEach(function (p, i) {
    var b = document.createElement('button');
    b.type = 'button'; b.className = 'chip'; b.textContent = fmt(p); b.title = PRESET_TIPS[i];
    b.addEventListener('click', function () { inTarget.value = fmt(p); renderCalc(); });
    presetsEl.appendChild(b);
  });

  function word(g) { return g === 6 ? 'шестици' : g === 5 ? 'петици' : g === 4 ? 'четворки' : 'тройки'; }

  function rulerHtml(cur, target) {
    function pos(v) { return (Math.max(2, Math.min(6, v)) - 2) / 4 * 100; }
    var ticks = '';
    for (var i = 0; i <= 40; i++) {
      var x = i * 10, len = i % 10 === 0 ? 14 : (i % 5 === 0 ? 10 : 6);
      ticks += '<line x1="' + x + '" y1="0" x2="' + x + '" y2="' + len + '"/>';
    }
    var nums = '';
    for (var v = 2; v <= 6; v++) nums += '<span class="num" style="left:' + pos(v) + '%">' + v + '</span>';
    return '<div class="ruler" aria-label="Линийка от 2 до 6: сега ' + fmt(cur) + ', цел ' + fmt(target) + '">' +
      '<svg viewBox="0 0 400 34" preserveAspectRatio="none" aria-hidden="true">' + ticks + '</svg>' + nums +
      '<i class="cur" style="left:' + pos(cur) + '%"></i><i class="goal" style="left:' + pos(target) + '%"><b>цел</b></i></div>';
  }

  function emptySummary(msg, cls) {
    summaryEl.innerHTML = '<p class="empty"' + (cls ? ' style="color:var(--red)"' : '') + '>' + esc(msg) + '</p>';
    resultsEl.innerHTML = '';
  }

  function renderCalc() {
    var t = num(inTarget.value);
    $$('#presets .chip').forEach(function (b, i) { b.setAttribute('aria-pressed', (!isNaN(t) && Math.abs(t - PRESETS[i]) < 1e-9) ? 'true' : 'false'); });

    var s = 0, n = 0, g = inGrades.value.trim();
    if (g) {
      var p = parseGrades(g);
      if (!p.ok) return emptySummary('В списъка има нещо, което не е оценка. Оценките са между 2 и 6.', true);
      s = sum(p.list); n = p.list.length;
    } else {
      var avg = num(inAvg.value), cnt = num(inCount.value);
      if (isNaN(avg) || isNaN(cnt)) {
        return emptySummary(inAvg.value.trim() ? 'Успехът сам не стига: напиши и колко оценки имаш. От броя им зависи колко шестици трябват.' : 'Напиши оценките си горе или сегашния успех и броя оценки.');
      }
      if (avg < 2 || avg > 6 || cnt < 1 || cnt !== Math.floor(cnt)) return emptySummary('Успехът е между 2 и 6, а броят оценки е цяло число.', true);
      n = cnt; s = avg * n;
    }
    if (n === 0) return emptySummary('Напиши оценките си горе.');
    if (isNaN(t) || t < 2 || t > 6) return emptySummary('Целта е число между 2 и 6.', true);

    var cur = s / n, big = '<div class="big circled"><span>' + fmt(cur) + '</span>' + circleSvg(11, 100, 60) + '</div>';
    var noun = n === 1 ? ' оценка' : ' оценки';
    if (cur >= t - 1e-9) {
      summaryEl.innerHTML = '<div class="sum-top">' + big + '<div class="teacher"><p class="note ok">Браво! Целта е постигната ✔</p><p class="meta">' + n + noun + ' в момента<br>Цел: ' + fmt(t) + '</p></div></div>' + rulerHtml(cur, t);
      resultsEl.innerHTML = '';
      return;
    }
    var any = false, rows = '';
    for (var gr = 6; gr >= 3; gr--) {
      if (gr <= t) {
        rows += rowHtml(String(gr), 'Само ' + word(gr), 'не помагат: не са над целта ' + fmt(t), true, 20 + gr);
      } else {
        var k = Math.max(1, Math.ceil((t * n - s) / (gr - t) - 1e-9));
        rows += rowHtml(String(gr), 'Още ' + k + ' ' + word(gr), 'успехът става ' + fmt((s + k * gr) / (n + k)), false, 20 + gr);
        any = true;
      }
    }
    if (t >= 5 && t < 6) {
      var combos = [];
      for (var six = 1; six <= 12 && combos.length < 3; six++) {
        for (var b = 1; b <= 80; b++) {
          if ((s + 6 * six + 5 * b) / (n + six + b) >= t - 1e-9) { combos.push(six + '×6 + ' + b + '×5'); break; }
        }
      }
      if (combos.length) rows += rowHtml('+', 'Или комбинация', combos.join(',  '), false, 40);
    }
    var msg = any ? 'Още ' + fmt(t - cur) + ' до целта' : 'С тези оценки целта не се достига';
    summaryEl.innerHTML = '<div class="sum-top">' + big + '<div class="teacher"><p class="note">' + msg + '</p><p class="meta">' + n + noun + ' в момента<br>Цел: ' + fmt(t) + '</p></div></div>' + rulerHtml(cur, t);
    resultsEl.innerHTML = rows;
  }

  function rowHtml(big, title, sub, dis, seed) {
    return '<li class="row' + (dis ? ' dis' : '') + '"><div class="g">' + esc(big) + (dis ? '' : circleSvg(seed, 60, 60)) + '</div>' +
      '<div><p class="t">' + esc(title) + '</p><p class="s">' + esc(sub) + '</p></div></li>';
  }

  [inGrades, inAvg, inCount, inTarget].forEach(function (el) { el.addEventListener('input', renderCalc); });
  $('#clear').addEventListener('click', function () { inGrades.value = ''; inAvg.value = ''; inCount.value = ''; inGrades.focus(); renderCalc(); });

  // ================= ПРЕДМЕТИ =================
  var listEl = $('#subj-list'), overallEl = $('#overall');

  function stats() {
    var out = [];
    state.subjects.forEach(function (s) {
      var p = parseGrades(s.grades);
      if (p.ok && p.list.length) out.push({ name: s.name, grades: p.list, sum: sum(p.list), n: p.list.length, avg: sum(p.list) / p.list.length });
    });
    return out;
  }

  function renderOverall() {
    var st = stats();
    if (!st.length) { overallEl.innerHTML = '<p class="empty">Добави предмет и напиши оценките му. Общият успех се смята сам.</p>'; return; }
    var a = st.reduce(function (x, y) { return x + y.avg; }, 0) / st.length;
    var rounded = Math.min(6, Math.floor(a + 0.5));
    overallEl.innerHTML = '<div class="big circled"><span>' + fmt(a) + '</span>' + circleSvg(31, 100, 60) + '</div>' +
      '<div><p class="lab">Общ успех</p><p class="meta">по ' + st.length + (st.length === 1 ? ' предмет' : ' предмета') + ', закръглен ' + rounded + '</p></div>';
  }

  function avgOf(s, el) {
    var p = parseGrades(s.grades);
    el.className = 'avg';
    if (!p.ok) { el.textContent = '?'; el.classList.add('bad'); }
    else if (!p.list.length) { el.textContent = '–'; el.classList.add('none'); }
    else el.textContent = fmt(sum(p.list) / p.list.length);
  }

  function renderSubjects() {
    listEl.innerHTML = '';
    state.subjects.forEach(function (s, idx) {
      var row = document.createElement('div');
      row.className = 'subj';
      row.innerHTML = '<input class="ink" type="text" placeholder="предмет" aria-label="Име на предмет" autocomplete="off">' +
        '<input class="ink" type="text" placeholder="оценки, напр. 6 5 5" aria-label="Оценки по предмета" autocomplete="off">' +
        '<div class="acts"><span class="avg" aria-label="Среден успех"></span>' +
        '<button type="button" class="chip" title="Прехвърли оценките в калкулатора">Смятай</button>' +
        '<button type="button" class="link x" title="Премахни предмета" aria-label="Премахни предмета">×</button></div>';
      var inputs = $$('input', row), avgEl = $('.avg', row);
      inputs[0].value = s.name; inputs[1].value = s.grades;
      avgOf(s, avgEl);
      inputs[0].addEventListener('input', function () { s.name = inputs[0].value; save(); });
      inputs[1].addEventListener('input', function () { s.grades = inputs[1].value; avgOf(s, avgEl); save(); renderOverall(); });
      $('.chip', row).addEventListener('click', function () {
        var p = parseGrades(s.grades);
        if (!p.ok || !p.list.length) { alert('Първо напиши валидни оценки (между 2 и 6).'); return; }
        inGrades.value = p.list.map(fmtGrade).join(' '); inAvg.value = ''; inCount.value = '';
        renderCalc(); showTab('calc');
      });
      $('.x', row).addEventListener('click', function () {
        if (s.grades && !confirm('Да премахна ли „' + (s.name || 'предмета') + '“ и оценките му?')) return;
        state.subjects.splice(state.subjects.indexOf(s), 1);
        save(); renderSubjects(); renderOverall();
      });
      listEl.appendChild(row);
    });
    renderOverall();
  }

  $('#add-subj').addEventListener('click', function () {
    state.subjects.push({ name: '', grades: '' });
    save(); renderSubjects();
    var rows = $$('.subj input:first-child', listEl); if (rows.length) rows[rows.length - 1].focus();
  });
  $('#all-to-calc').addEventListener('click', function () {
    var all = []; stats().forEach(function (s) { all = all.concat(s.grades); });
    if (!all.length) { alert('Още няма оценки в предметите.'); return; }
    inGrades.value = all.map(fmtGrade).join(' '); inAvg.value = ''; inCount.value = '';
    renderCalc(); showTab('calc');
  });

  // ---------- поставяне от Школо ----------
  var dlg = $('#dlg-import'), impText = $('#import-text'), impMsg = $('#import-msg'), impOk = $('#import-ok');
  var imported = [];

  function parseShkolo(text) {
    var lines = String(text || '').split(/\r?\n/), subs = [], official = '';
    lines.forEach(function (line) {
      if (/средноаритметично/i.test(line)) { var m = /(\d[.,]\d+)/.exec(line); if (m) official = m[1].replace(',', '.'); return; }
      var f = line.split('\t');
      if (f.length >= 3 && /^\s*\d+\s*$/.test(f[0])) {
        var name = f[1].trim(); if (!name) return;
        var cols = [2]; if (f.length >= 7) cols.push(4);
        var gr = [];
        cols.forEach(function (c) { (f[c] || '').split(/[\s;]+/).forEach(function (t) { t = t.replace(/[,*]+$/, ''); if (/^[2-6]$/.test(t)) gr.push(t); }); });
        if (gr.length) subs.push({ name: name, grades: gr.join(' ') });
        return;
      }
      // без табове (някои телефони): "1 Математика 5 6 4"
      var m2 = /^\s*\d+\s+(.+?)\s+((?:[2-6]\s*)+)$/.exec(line);
      if (m2 && !/общ|средно/i.test(m2[1])) subs.push({ name: m2[1].trim(), grades: m2[2].trim().replace(/\s+/g, ' ') });
    });
    return { subs: subs, official: official };
  }

  function updateImportPreview() {
    var r = parseShkolo(impText.value);
    imported = r.subs;
    if (!impText.value.trim()) { impMsg.textContent = ''; impOk.disabled = true; return; }
    if (!r.subs.length) { impMsg.textContent = 'Не намерих оценки в този текст. Копирай цялата страница с оценките (Ctrl+A, Ctrl+C).'; impOk.disabled = true; return; }
    var all = []; r.subs.forEach(function (s) { all = all.concat(parseGrades(s.grades).list); });
    var a = sum(all) / all.length, txt = 'Намерих ' + all.length + ' оценки в ' + r.subs.length + ' предмета, среден ' + fmt(a) + '.';
    if (r.official) txt += Math.abs(Number(r.official) - a) < 0.006 ? ' Школо показва ' + r.official + ' ✔ съвпада.' : ' Школо показва ' + r.official + ' – не съвпада, провери.';
    impMsg.textContent = txt; impOk.disabled = false;
  }
  $('#open-import').addEventListener('click', function () { impText.value = ''; updateImportPreview(); if (dlg.showModal) dlg.showModal(); else alert('Този браузър не поддържа диалози.'); impText.focus(); });
  impText.addEventListener('input', updateImportPreview);
  impOk.addEventListener('click', function () {
    imported.forEach(function (s) {
      var f = state.subjects.filter(function (x) { return x.name.trim().toLowerCase() === s.name.trim().toLowerCase(); })[0];
      if (f) f.grades = s.grades; else state.subjects.push({ name: s.name, grades: s.grades });
    });
    save(); renderSubjects(); dlg.close();
  });

  // ================= СТАТИСТИКА =================
  function tallySvg(n, seed) {
    var r = rng(seed * 17 + 3), groups = Math.ceil(n / 5), gw = 38, h = 34, x = 4, left = n, parts = '';
    for (var gi = 0; gi < groups; gi++) {
      var k = Math.min(5, left); left -= k;
      for (var b = 0; b < Math.min(4, k); b++) {
        var jx = (r() - 0.5) * 1.6;
        parts += '<line x1="' + (x + b * 8 + jx).toFixed(1) + '" y1="5" x2="' + (x + b * 8 - jx).toFixed(1) + '" y2="30"/>';
      }
      if (k === 5) parts += '<line x1="' + (x - 5) + '" y1="26" x2="' + (x + 28) + '" y2="9"/>';
      x += gw;
    }
    return '<svg width="' + x + '" height="' + h + '" viewBox="0 0 ' + x + ' ' + h + '" aria-hidden="true"><g stroke="var(--ink)" stroke-width="2.3" stroke-linecap="round">' + parts + '</g></svg>';
  }

  function nextSteps(st) {
    var r = [];
    st.forEach(function (s) {
      var a = s.avg, t = Math.floor(a - 0.5 + 1e-9) + 1.5;
      if (t > 5.5 + 1e-9) return;
      var k = Math.max(1, Math.ceil((t * s.n - s.sum) / (6 - t) - 1e-9));
      r.push({ name: s.name, avg: a, target: t, k: k });
    });
    r.sort(function (x, y) { return x.k - y.k || y.avg - x.avg; });
    return r.slice(0, 6);
  }

  function renderStats() {
    var st = stats(), el = $('#stats');
    if (!st.length) { el.innerHTML = '<p class="hint" style="font:400 24px/1.2 var(--hand)">Още няма предмети с оценки. Добави ги в раздел „Предмети“ или постави от Школо.</p>'; return; }
    var best = st[0], worst = st[0], total = 0;
    st.forEach(function (s) { if (s.avg > best.avg) best = s; if (s.avg < worst.avg) worst = s; total += s.n; });
    var nm = function (s) { return esc(s.name || '(без име)'); };
    var facts = st.length === 1 ? 'Имаш ' + total + ' оценки по 1 предмет.' :
      'Най-силен: <b>' + nm(best) + '</b> (' + fmt(best.avg) + '). Най-слаб: <b>' + nm(worst) + '</b> (' + fmt(worst.avg) + '). Общо ' + total + ' оценки.';

    var sorted = st.slice().sort(function (a, b) { return b.avg - a.avg; });
    var pos = function (v) { return (Math.max(2, Math.min(6, v)) - 2) / 4 * 100; };
    var bars = '<div class="bars">';
    // линията за целта живее в колоната на стълбовете
    bars += '<div style="position:relative">';
    sorted.forEach(function (s) {
      bars += '<div class="bar"><div class="nm" title="' + esc(s.name) + '">' + nm(s) + '</div><div class="tr"><div class="fill' + (s.avg >= state.goal - 1e-9 ? ' ok' : '') + '" style="width:' + Math.max(1, pos(s.avg) * 0.86) + '%"><span>' + fmt(s.avg) + '</span></div></div></div>';
    });
    bars += '<div class="goalline" style="left:calc(34% + 10px + (100% - 34% - 10px) * ' + (pos(state.goal) * 0.86 / 100).toFixed(4) + ')"><b>цел ' + fmt(state.goal) + '</b></div>';
    bars += '</div></div>';

    var counts = [0, 0, 0, 0, 0];
    st.forEach(function (s) { s.grades.forEach(function (v) { var r = Math.max(2, Math.min(6, Math.floor(v + 0.5))); counts[6 - r]++; }); });
    var tally = '<ul class="tally">';
    for (var i = 0; i < 5; i++) {
      var g = 6 - i, c = counts[i];
      tally += '<li><div class="g">' + g + circleSvg(50 + g, 60, 60).replace('class="pen"', 'class="pen thin"') + '</div><div class="marks">' + (c ? (c <= 60 ? tallySvg(c, g) : '') : '<span class="none">няма</span>') + '</div><div class="n">' + (c || '') + '</div></li>';
    }
    tally += '</ul>';

    var ns = nextSteps(st), nx;
    if (!ns.length) nx = '<p class="allsix">Всичко е на шест. Браво! ✔</p>';
    else nx = '<ul class="next">' + ns.map(function (x) {
      return '<li><div><div class="nm">' + esc(x.name || '(без име)') + '</div><div class="sub">' + fmt(x.avg) + ' → ' + fmt(x.target) + ', става ' + Math.floor(x.target + 0.5) + '</div></div><div class="need">още ' + x.k + (x.k === 1 ? ' шестица' : ' шестици') + '</div></li>';
    }).join('') + '</ul>';

    function h2(t, seed) { return '<h2>' + esc(t) + underlineSvg(seed, 'pen').replace('class="uline"', '') + '</h2>'; }
    el.innerHTML = '<p class="facts">' + facts + '</p>' +
      '<div class="sec">' + h2('Среден успех по предмет', 4) + bars + '</div>' +
      '<div class="sec">' + h2('Колко оценки от всеки вид', 5) + tally + '</div>' +
      '<div class="sec">' + h2('Какво да поправиш първо', 6) + nx + '</div>';
  }

  // ================= НАСТРОЙКИ =================
  var themeChips = $('#theme-chips');
  [['auto', 'Като устройството'], ['light', 'Светла'], ['dark', 'Тъмна']].forEach(function (t) {
    var b = document.createElement('button');
    b.type = 'button'; b.className = 'chip'; b.textContent = t[1]; b.dataset.v = t[0];
    b.addEventListener('click', function () { state.theme = t[0]; save(); applyTheme(); refreshSettings(); });
    themeChips.appendChild(b);
  });
  var inGoal = $('#in-goal');
  function refreshSettings() {
    $$('.chip', themeChips).forEach(function (b) { b.setAttribute('aria-pressed', b.dataset.v === state.theme ? 'true' : 'false'); });
    if (document.activeElement !== inGoal) inGoal.value = fmt(state.goal);
  }
  inGoal.addEventListener('input', function () {
    var v = num(inGoal.value);
    if (!isNaN(v) && v >= 2 && v <= 6) { state.goal = v; save(); inTarget.value = fmt(v); renderCalc(); }
  });
  inGoal.addEventListener('blur', refreshSettings);
  $('#wipe').addEventListener('click', function () {
    if (!confirm('Да изтрия ли всички предмети и оценки? Това не може да се върне.')) return;
    state.subjects = []; save(); renderSubjects();
  });

  // ---------- инсталиране (PWA) ----------
  var deferred = null, installBtn = $('#install'), installHint = $('#install-hint');
  var standalone = (window.matchMedia && window.matchMedia('(display-mode: standalone)').matches) || window.navigator.standalone;
  var ios = /iphone|ipad|ipod/i.test(navigator.userAgent);
  function installText() {
    if (standalone) installHint.textContent = 'Приложението вече е инсталирано и работи и без интернет.';
    else if (deferred) installHint.textContent = 'Инсталирай го, за да го отваряш като обикновено приложение, дори без интернет.';
    else if (ios) installHint.textContent = 'На iPhone и iPad: натисни „Сподели“ и избери „Добави към началния екран“.';
    else installHint.textContent = 'Ако браузърът позволява, в менюто му има „Инсталирай приложението“ (в Chrome и Edge – иконата в адресната лента).';
  }
  window.addEventListener('beforeinstallprompt', function (e) { e.preventDefault(); deferred = e; installBtn.hidden = false; installText(); });
  window.addEventListener('appinstalled', function () { deferred = null; installBtn.hidden = true; standalone = true; installText(); });
  installBtn.addEventListener('click', function () {
    if (!deferred) return;
    deferred.prompt();
    deferred.userChoice.then(function () { deferred = null; installBtn.hidden = true; installText(); });
  });
  installText();

  if ('serviceWorker' in navigator) {
    window.addEventListener('load', function () { navigator.serviceWorker.register('sw.js').catch(function () { }); });
  }

  // ---------- старт ----------
  applyTheme();
  decorateTitles();
  inTarget.value = fmt(state.goal);
  renderSubjects();
  refreshSettings();
  renderCalc();
  var start = (location.hash || '').replace('#', '');
  showTab(TABS.indexOf(start) >= 0 ? start : 'calc');
})();
