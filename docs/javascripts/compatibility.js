/* 5DO compatibility browser. No dependencies. */
(function () {
  'use strict';

  var app = document.getElementById('compat-app');
  if (!app) return;

  function findScript() {
    if (document.currentScript) return document.currentScript;
    var scripts = document.getElementsByTagName('script');
    for (var i = scripts.length - 1; i >= 0; i--) {
      if (scripts[i].src && scripts[i].src.indexOf('compatibility.js') !== -1) {
        return scripts[i];
      }
    }
    return null;
  }

  var script = findScript();
  if (!script) return;

  var siteRoot = new URL('../', script.src);
  var dataUrl = new URL('../data/compatibility.json', script.src);

  var STATUS_ORDER = ['rendered', 'no-video', 'not-loadable'];
  var STATUS_RANK = { 'unknown': 0, 'rendered': 1, 'no-video': 2, 'not-loadable': 3 };
  var STATUS_VALUES = ['all', 'rendered', 'no-video', 'not-loadable', 'manual'];
  var SORT_VALUES = ['title-asc', 'title-desc', 'status', 'depth', 'region'];
  var REGION_ORDER = [
    'USA', 'Europe', 'Japan', 'Korea', 'World', 'Germany', 'France',
    'Canada', 'Brazil', 'Australia', 'Spain', 'Italy', 'Taiwan', 'China',
    'Asia', 'United Kingdom', 'Netherlands', 'Sweden', 'Unknown'
  ];
  var CATEGORY_ORDER = [
    'Games', 'Action', 'Adventure', 'Racing', 'Sports', 'Fighting',
    'Shooter', 'Strategy', 'Puzzle', 'Simulation', 'Multimedia',
    'Educational', 'Demo', 'Adult', 'Arcade', 'Miscellaneous', 'Other'
  ];

  var state = {
    q: '',
    qKey: '',
    status: 'all',
    region: 'all',
    category: 'all',
    letter: 'all',
    sort: 'title-asc',
    biosOnly: false,
    game: null
  };

  var data = null;
  var games = [];
  var els = {};
  var lastFocus = null;
  var searchTimer = null;
  var collator = new Intl.Collator(undefined, { sensitivity: 'base', numeric: true });

  function el(tag, className, text) {
    var node = document.createElement(tag);
    if (className) node.className = className;
    if (text !== undefined && text !== null) node.textContent = text;
    return node;
  }

  function append(parent) {
    for (var i = 1; i < arguments.length; i++) {
      if (arguments[i]) parent.appendChild(arguments[i]);
    }
    return parent;
  }

  function formatNumber(value) {
    var number = Number(value);
    if (!isFinite(number)) return String(value);
    return number.toLocaleString();
  }

  function formatFrames(frames) {
    var number = Number(frames) || 0;
    if (number >= 1000) {
      var k = number / 1000;
      return (k >= 10 ? Math.round(k) : Math.round(k * 10) / 10) + 'k';
    }
    return String(number);
  }

  function assetUrl(path) {
    if (!path) return '';
    return new URL(path, siteRoot).href;
  }

  function cssKey(value) {
    var text = String(value || '').toLowerCase();
    var out = '';
    var lastDash = false;
    for (var i = 0; i < text.length; i++) {
      var code = text.charCodeAt(i);
      var keep = (code >= 97 && code <= 122) || (code >= 48 && code <= 57);
      if (keep) {
        out += text.charAt(i);
        lastDash = false;
      } else if (!lastDash && out) {
        out += '-';
        lastDash = true;
      }
    }
    if (out.charAt(out.length - 1) === '-') out = out.slice(0, -1);
    return out || 'unknown';
  }

  function manualRank(value) {
    if (!value) return -1;
    var key = String(value).toLowerCase();
    if (key === 'good') return 4;
    if (key === 'playable') return 3;
    if (key === 'unplayable') return 2;
    if (key === 'not loadable') return 1;
    return 0;
  }

  function normalize(value) {
    var text = String(value || '');
    if (text.normalize) text = text.normalize('NFD');
    var out = '';
    var lastSpace = true;
    for (var i = 0; i < text.length; i++) {
      var code = text.charCodeAt(i);
      if (code >= 0x0300 && code <= 0x036f) continue;
      var ch = text.charAt(i).toLowerCase();
      if (ch === ' ' || ch === '\t' || ch === '\n' || ch === '\r') {
        if (!lastSpace) {
          out += ' ';
          lastSpace = true;
        }
      } else {
        out += ch;
        lastSpace = false;
      }
    }
    return out.trim();
  }

  function searchKey(value) {
    var text = normalize(value);
    var out = '';
    var lastSpace = true;
    for (var i = 0; i < text.length; i++) {
      var code = text.charCodeAt(i);
      var keep = (code >= 48 && code <= 57) || (code >= 97 && code <= 122) || code > 127;
      if (keep) {
        out += text.charAt(i);
        lastSpace = false;
      } else if (!lastSpace) {
        out += ' ';
        lastSpace = true;
      }
    }
    return out.trim();
  }

  function validLetter(value) {
    if (value === 'all' || value === '#') return true;
    if (value.length !== 1) return false;
    var code = value.charCodeAt(0);
    return code >= 65 && code <= 90;
  }

  function regionRank(region) {
    var index = REGION_ORDER.indexOf(region);
    return index === -1 ? REGION_ORDER.length : index;
  }

  function categoryRank(category) {
    var index = CATEGORY_ORDER.indexOf(category);
    return index === -1 ? CATEGORY_ORDER.length : index;
  }

  function readUrl() {
    var params = new URLSearchParams(window.location.search);
    state.q = params.get('search') || '';
    state.qKey = searchKey(state.q);
    var status = params.get('status') || 'all';
    state.status = STATUS_VALUES.indexOf(status) !== -1 ? status : 'all';
    state.region = params.get('region') || 'all';
    state.category = params.get('category') || 'all';
    var letter = params.get('letter') || 'all';
    state.letter = validLetter(letter) ? letter : 'all';
    var sort = params.get('sort') || 'title-asc';
    state.sort = SORT_VALUES.indexOf(sort) !== -1 ? sort : 'title-asc';
    state.biosOnly = params.get('bios') === '1';
    state.game = params.get('game') || null;
  }

  function writeUrl() {
    var params = new URLSearchParams();
    if (state.q) params.set('search', state.q);
    if (state.status !== 'all') params.set('status', state.status);
    if (state.region !== 'all') params.set('region', state.region);
    if (state.category !== 'all') params.set('category', state.category);
    if (state.letter !== 'all') params.set('letter', state.letter);
    if (state.sort !== 'title-asc') params.set('sort', state.sort);
    if (state.biosOnly) params.set('bios', '1');
    if (state.game) params.set('game', state.game);
    var query = params.toString();
    var url = window.location.pathname + (query ? '?' + query : '');
    try {
      window.history.replaceState(null, '', url);
    } catch (error) {
      /* file:// and some embedded views do not allow history updates. */
    }
  }

  function addUnique(list, value) {
    if (value && list.indexOf(value) === -1) list.push(value);
  }

  function collectBios(game) {
    var values = [];
    var hasDefault = false;
    for (var i = 0; i < game.variants.length; i++) {
      var variant = game.variants[i];
      if (!variant.bios && !variant.bios2) hasDefault = true;
      addUnique(values, variant.bios);
      addUnique(values, variant.bios2);
    }
    return { values: values, hasDefault: hasDefault };
  }

  function prepareGames() {
    for (var i = 0; i < games.length; i++) {
      var game = games[i];
      var parts = [game.title, game.sortTitle, game.category, (game.regions || []).join(' ')];
      for (var j = 0; j < game.variants.length; j++) {
        var variant = game.variants[j];
        parts.push(variant.disc);
        if (variant.manualNotes) parts.push(variant.manualNotes);
        if (variant.reference && variant.reference.notes) parts.push(variant.reference.notes);
      }
      if (game.manualNotes) parts.push(game.manualNotes);
      if (game.reference && game.reference.notes) parts.push(game.reference.notes);
      game._haystack = searchKey(parts.join(' '));
      game._titleKey = game.sortTitle || normalize(game.title);
      game._statusRank = STATUS_RANK[game.status] || 0;
      game._manualRank = manualRank(game.manualGrade);
      game._variantCount = game.variants.length;
      game._regionKey = (game.regions && game.regions[0]) || 'Unknown';
      game._bios = collectBios(game);
    }
  }

  function showError(error) {
    app.innerHTML = '';
    app.className = 'compat-app is-ready';
    var box = el('div', 'compat-error');
    box.appendChild(el('h2', '', 'Compatibility data could not be loaded'));
    box.appendChild(el('p', '', 'The filterable view needs JavaScript and the compatibility JSON file. The full table is still available.'));
    var link = el('a', 'compat-action is-primary', 'Open the raw compatibility results');
    link.href = 'GameCompatibilityResults.html';
    box.appendChild(link);
    app.appendChild(box);
    if (window.console && console.error) console.error(error);
  }

  function loadData() {
    var loading = document.getElementById('compat-loading');
    if (loading) loading.hidden = false;
    fetch(dataUrl.href, { cache: 'no-cache' })
      .then(function (response) {
        if (!response.ok) throw new Error('HTTP ' + response.status);
        return response.json();
      })
      .then(function (json) {
        data = json;
        games = json.games || [];
        prepareGames();
        buildUi();
        validateFilters();
        syncControls();
        render();
        if (state.game) openGame(state.game, true);
      })
      .catch(function (error) {
        showError(error);
      });
  }

  function buildUi() {
    app.innerHTML = '';
    app.className = 'compat-app is-ready';
    var hero = buildHero();
    var stats = buildStats();
    var toolbar = buildToolbar();
    var letters = buildLetters();
    var meta = buildMeta();
    var table = buildTable();
    var empty = buildEmpty();
    var dialog = buildDialog();
    append(app, hero, stats, toolbar, letters, meta, table, empty, dialog);
    bindEvents();
  }

  function buildHero() {
    var hero = el('div', 'compat-hero');
    var copy = el('div', 'compat-hero-copy');
    copy.appendChild(el('p', 'compat-kicker', 'Automated compatibility sweep'));
    var lede = el('p', 'compat-lede');
    lede.textContent = formatNumber(data.totals.discs) + ' discs across ' +
      formatNumber(data.totals.games) + ' games. Filter by status, region, category, or title.';
    copy.appendChild(lede);
    copy.appendChild(el('p', 'compat-note',
      'Rendered means the automated boot test passed. It is not a playability grade. ' +
      'Manual grades come from the curated 5DO list.'));
    var source = el('p', 'compat-source');
    source.appendChild(document.createTextNode('Results updated '));
    var time = el('time', '', data.sourceUpdated || 'unknown');
    if (data.sourceUpdated) time.setAttribute('datetime', data.sourceUpdated);
    source.appendChild(time);
    source.appendChild(document.createTextNode(' · '));
    var raw = el('a', '', 'Raw evidence');
    raw.href = 'GameCompatibilityResults.html';
    source.appendChild(raw);
    source.appendChild(document.createTextNode(' · '));
    var how = el('a', '', 'How to read this page');
    how.href = '#how-to-read-this-page';
    source.appendChild(how);
    copy.appendChild(source);
    hero.appendChild(copy);
    return hero;
  }

  function percent(count) {
    var total = Number(data.totals.discs) || 0;
    if (!total) return '0.0% of discs';
    return ((Number(count) / total) * 100).toFixed(1) + '% of discs';
  }

  function buildStats() {
    var wrap = el('div', 'compat-stats');
    wrap.setAttribute('role', 'group');
    wrap.setAttribute('aria-label', 'Filter by compatibility status');
    els.statButtons = {};
    var totals = data.totals;

    function addStat(key, label, count, detail, extraClass) {
      var button = el('button', 'compat-stat is-' + extraClass);
      button.type = 'button';
      button.setAttribute('data-status', key);
      button.setAttribute('aria-pressed', 'false');
      append(button,
        el('span', 'compat-stat-label', label),
        el('span', 'compat-stat-count', formatNumber(count)),
        el('span', 'compat-stat-detail', detail)
      );
      els.statButtons[key] = button;
      wrap.appendChild(button);
    }

    addStat('all', 'All discs', totals.discs, formatNumber(totals.games) + ' games', 'all');
    addStat('rendered', 'Rendered', totals.rendered, percent(totals.rendered), 'rendered');
    addStat('no-video', 'No video', totals.noVideo, percent(totals.noVideo), 'no-video');
    addStat('not-loadable', 'Not loadable', totals.notLoadable, percent(totals.notLoadable), 'not-loadable');
    addStat('manual', 'Manually graded', totals.manualGrades, 'games with a grade', 'manual');
    return wrap;
  }

  function addOption(select, value, label) {
    var option = document.createElement('option');
    option.value = value;
    option.textContent = label;
    select.appendChild(option);
  }

  function buildToolbar() {
    var wrap = el('div', 'compat-toolbar');
    var searchWrap = el('div', 'compat-search');
    var label = el('label', 'visually-hidden', 'Search games and discs');
    label.setAttribute('for', 'compat-search');
    var input = el('input', 'compat-search-input');
    input.id = 'compat-search';
    input.type = 'search';
    input.placeholder = 'Search ' + formatNumber(data.totals.games) + ' games or ' +
      formatNumber(data.totals.discs) + ' discs...';
    input.autocomplete = 'off';
    input.spellcheck = false;
    var clearSearch = el('button', 'compat-search-clear', '×');
    clearSearch.type = 'button';
    clearSearch.id = 'compat-search-clear';
    clearSearch.setAttribute('aria-label', 'Clear search');
    clearSearch.hidden = true;
    append(searchWrap, label, input, clearSearch);
    els.search = input;
    els.searchClear = clearSearch;

    var filters = el('div', 'compat-filters');

    var regionField = el('label', 'compat-field');
    regionField.appendChild(el('span', '', 'Region'));
    var regionSelect = document.createElement('select');
    regionSelect.id = 'compat-region';
    regionField.appendChild(regionSelect);
    els.region = regionSelect;

    var categoryField = el('label', 'compat-field');
    categoryField.appendChild(el('span', '', 'Category'));
    var categorySelect = document.createElement('select');
    categorySelect.id = 'compat-category';
    categoryField.appendChild(categorySelect);
    els.category = categorySelect;

    var sortField = el('label', 'compat-field');
    sortField.appendChild(el('span', '', 'Sort'));
    var sortSelect = document.createElement('select');
    sortSelect.id = 'compat-sort';
    sortField.appendChild(sortSelect);
    els.sort = sortSelect;

    var biosLabel = el('label', 'compat-check');
    var biosInput = document.createElement('input');
    biosInput.type = 'checkbox';
    biosInput.id = 'compat-bios';
    biosLabel.appendChild(biosInput);
    biosLabel.appendChild(el('span', '', 'BIOS overrides'));
    els.bios = biosInput;

    var clear = el('button', 'compat-clear', 'Clear filters');
    clear.type = 'button';
    clear.id = 'compat-clear';
    els.clear = clear;

    append(filters, regionField, categoryField, sortField, biosLabel, clear);
    append(wrap, searchWrap, filters);

    var regionCounts = {};
    for (var i = 0; i < games.length; i++) {
      var regions = games[i].regions || [];
      for (var j = 0; j < regions.length; j++) {
        regionCounts[regions[j]] = (regionCounts[regions[j]] || 0) + 1;
      }
    }
    var regionNames = Object.keys(regionCounts).sort(function (a, b) {
      return regionRank(a) - regionRank(b) || a.localeCompare(b);
    });
    addOption(regionSelect, 'all', 'All regions');
    for (var r = 0; r < regionNames.length; r++) {
      addOption(regionSelect, regionNames[r],
        regionNames[r] + ' (' + formatNumber(regionCounts[regionNames[r]]) + ')');
    }

    var categoryCounts = {};
    for (var c = 0; c < games.length; c++) {
      categoryCounts[games[c].category] = (categoryCounts[games[c].category] || 0) + 1;
    }
    var categoryNames = Object.keys(categoryCounts).sort(function (a, b) {
      return categoryRank(a) - categoryRank(b) || a.localeCompare(b);
    });
    addOption(categorySelect, 'all', 'All categories');
    for (var k = 0; k < categoryNames.length; k++) {
      addOption(categorySelect, categoryNames[k],
        categoryNames[k] + ' (' + formatNumber(categoryCounts[categoryNames[k]]) + ')');
    }

    addOption(sortSelect, 'title-asc', 'Title A–Z');
    addOption(sortSelect, 'title-desc', 'Title Z–A');
    addOption(sortSelect, 'status', 'Status (worst first)');
    addOption(sortSelect, 'depth', 'Most tested first');
    addOption(sortSelect, 'region', 'Region');

    return wrap;
  }

  function buildLetters() {
    var wrap = el('div', 'compat-letters');
    wrap.setAttribute('role', 'group');
    wrap.setAttribute('aria-label', 'Filter by first letter');
    var counts = {};
    for (var i = 0; i < games.length; i++) {
      counts[games[i].letter] = (counts[games[i].letter] || 0) + 1;
    }
    els.letterButtons = {};
    var letters = ['all', '#'];
    for (var code = 65; code <= 90; code++) letters.push(String.fromCharCode(code));
    for (var j = 0; j < letters.length; j++) {
      var letter = letters[j];
      var count = letter === 'all' ? games.length : (counts[letter] || 0);
      var button = el('button', 'compat-letter', letter === 'all' ? 'All' : letter);
      button.type = 'button';
      button.setAttribute('data-letter', letter);
      button.setAttribute('aria-pressed', 'false');
      button.title = count ? formatNumber(count) + ' games' : 'No games';
      if (!count && letter !== 'all') button.disabled = true;
      els.letterButtons[letter] = button;
      wrap.appendChild(button);
    }
    return wrap;
  }

  function buildMeta() {
    var wrap = el('div', 'compat-results-meta');
    var count = el('p', 'compat-count');
    count.id = 'compat-count';
    count.setAttribute('aria-live', 'polite');
    els.count = count;
    var legend = el('p', 'compat-legend');
    var items = [
      ['rendered', 'Rendered'],
      ['no-video', 'No video'],
      ['not-loadable', 'Not loadable']
    ];
    for (var i = 0; i < items.length; i++) {
      var item = el('span', 'compat-legend-item');
      item.appendChild(el('span', 'compat-dot is-' + items[i][0]));
      item.appendChild(document.createTextNode(items[i][1]));
      legend.appendChild(item);
    }
    append(wrap, count, legend);
    return wrap;
  }

  function buildTable() {
    var wrap = el('div', 'compat-table-wrap');
    var table = el('table', 'compat-table');
    table.appendChild(el('caption', 'visually-hidden', 'Compatibility results by game'));
    var thead = el('thead');
    var headRow = el('tr');
    var headers = [
      ['', 'Screenshot'],
      ['Game', ''],
      ['Region', ''],
      ['Category', ''],
      ['Status', ''],
      ['BIOS', ''],
      ['Tested', ''],
      ['', 'Details']
    ];
    for (var i = 0; i < headers.length; i++) {
      var th = document.createElement('th');
      th.scope = 'col';
      if (headers[i][0]) {
        th.textContent = headers[i][0];
      } else {
        th.appendChild(el('span', 'visually-hidden', headers[i][1]));
      }
      headRow.appendChild(th);
    }
    thead.appendChild(headRow);
    var tbody = el('tbody');
    tbody.id = 'compat-tbody';
    els.tbody = tbody;
    append(table, thead, tbody);
    wrap.appendChild(table);
    els.tableWrap = wrap;
    return wrap;
  }

  function buildEmpty() {
    var box = el('div', 'compat-empty');
    box.hidden = true;
    box.appendChild(el('p', '', 'No games match these filters.'));
    var clear = el('button', 'compat-action is-primary', 'Clear filters');
    clear.type = 'button';
    clear.addEventListener('click', clearFilters);
    box.appendChild(clear);
    els.empty = box;
    return box;
  }

  function buildDialog() {
    var dialog = el('dialog', 'compat-dialog');
    dialog.id = 'compat-dialog';
    dialog.setAttribute('aria-labelledby', 'compat-dialog-title');
    var inner = el('div', 'compat-dialog-inner');
    var head = el('header', 'compat-dialog-head');
    var headText = el('div', 'compat-dialog-headtext');
    var title = el('h2');
    title.id = 'compat-dialog-title';
    var sub = el('p', 'compat-dialog-sub');
    sub.id = 'compat-dialog-sub';
    append(headText, title, sub);
    var close = el('button', 'compat-dialog-close', '×');
    close.type = 'button';
    close.setAttribute('aria-label', 'Close details');
    close.addEventListener('click', closeDialog);
    append(head, headText, close);
    var body = el('div', 'compat-dialog-body');
    body.id = 'compat-dialog-body';
    append(inner, head, body);
    dialog.appendChild(inner);
    dialog.addEventListener('click', function (event) {
      if (event.target === dialog) closeDialog();
    });
    dialog.addEventListener('close', onDialogClose);
    els.dialog = dialog;
    els.dialogTitle = title;
    els.dialogSub = sub;
    els.dialogBody = body;
    els.dialogClose = close;
    return dialog;
  }

  function bindEvents() {
    els.search.addEventListener('input', function () {
      state.q = els.search.value;
      state.qKey = searchKey(state.q);
      if (searchTimer) clearTimeout(searchTimer);
      searchTimer = setTimeout(function () {
        render();
      }, 120);
    });
    els.search.addEventListener('keydown', function (event) {
      if (event.key === 'Escape' && els.search.value) {
        event.preventDefault();
        els.search.value = '';
        state.q = '';
        state.qKey = '';
        render();
      }
    });
    els.searchClear.addEventListener('click', function () {
      els.search.value = '';
      state.q = '';
      state.qKey = '';
      render();
      els.search.focus();
    });
    els.region.addEventListener('change', function () {
      state.region = els.region.value;
      render();
    });
    els.category.addEventListener('change', function () {
      state.category = els.category.value;
      render();
    });
    els.sort.addEventListener('change', function () {
      state.sort = els.sort.value;
      render();
    });
    els.bios.addEventListener('change', function () {
      state.biosOnly = els.bios.checked;
      render();
    });
    els.clear.addEventListener('click', clearFilters);

    Object.keys(els.statButtons).forEach(function (key) {
      els.statButtons[key].addEventListener('click', function () {
        state.status = state.status === key && key !== 'all' ? 'all' : key;
        render();
      });
    });
    Object.keys(els.letterButtons).forEach(function (key) {
      els.letterButtons[key].addEventListener('click', function () {
        state.letter = state.letter === key && key !== 'all' ? 'all' : key;
        render();
      });
    });

    els.tbody.addEventListener('click', function (event) {
      var target = event.target;
      var button = target && target.closest ? target.closest('button[data-game-id]') : null;
      if (button) openGame(button.getAttribute('data-game-id'), false);
    });

    window.addEventListener('popstate', function () {
      readUrl();
      syncControls();
      render();
      if (state.game) openGame(state.game, true);
      else closeDialog();
    });

    document.addEventListener('keydown', function (event) {
      if (event.key === '/' && !isTyping(event.target)) {
        event.preventDefault();
        els.search.focus();
      }
    });
  }

  function isTyping(target) {
    if (!target) return false;
    var tag = target.tagName ? target.tagName.toLowerCase() : '';
    return tag === 'input' || tag === 'textarea' || tag === 'select' || target.isContentEditable;
  }

  function validateFilters() {
    if (!optionExists(els.region, state.region)) state.region = 'all';
    if (!optionExists(els.category, state.category)) state.category = 'all';
    if (state.letter !== 'all' && els.letterButtons[state.letter] && els.letterButtons[state.letter].disabled) {
      state.letter = 'all';
    }
  }

  function optionExists(select, value) {
    for (var i = 0; i < select.options.length; i++) {
      if (select.options[i].value === value) return true;
    }
    return false;
  }

  function syncControls() {
    Object.keys(els.statButtons).forEach(function (key) {
      var active = state.status === key;
      var button = els.statButtons[key];
      button.classList.toggle('is-active', active);
      button.setAttribute('aria-pressed', active ? 'true' : 'false');
    });
    Object.keys(els.letterButtons).forEach(function (key) {
      var active = state.letter === key;
      var button = els.letterButtons[key];
      button.classList.toggle('is-active', active);
      button.setAttribute('aria-pressed', active ? 'true' : 'false');
    });
    if (els.region.value !== state.region) els.region.value = state.region;
    if (els.category.value !== state.category) els.category.value = state.category;
    if (els.sort.value !== state.sort) els.sort.value = state.sort;
    els.bios.checked = state.biosOnly;
    if (document.activeElement !== els.search) els.search.value = state.q;
    els.searchClear.hidden = !state.q;
    els.clear.disabled = !hasActiveFilters();
  }

  function hasActiveFilters() {
    return !!(state.q || state.status !== 'all' || state.region !== 'all' ||
      state.category !== 'all' || state.letter !== 'all' || state.biosOnly);
  }

  function clearFilters() {
    state.q = '';
    state.qKey = '';
    state.status = 'all';
    state.region = 'all';
    state.category = 'all';
    state.letter = 'all';
    state.biosOnly = false;
    state.sort = 'title-asc';
    els.search.value = '';
    render();
  }

  function render() {
    var filtered = games.filter(matchesGame);
    var sorted = sortGames(filtered);
    var discCount = countMatchingDiscs(sorted);
    var totalGames = games.length;
    var totalDiscs = data.totals.discs;
    var text = 'Showing ' + formatNumber(sorted.length) + ' of ' + formatNumber(totalGames) +
      ' games · ' + formatNumber(discCount) + ' of ' + formatNumber(totalDiscs) + ' discs';
    if (!hasActiveFilters()) {
      text = 'Showing ' + formatNumber(totalGames) + ' games · ' + formatNumber(totalDiscs) + ' discs';
    }
    els.count.textContent = text;
    els.tbody.innerHTML = '';
    if (!sorted.length) {
      els.tableWrap.hidden = true;
      els.empty.hidden = false;
    } else {
      els.tableWrap.hidden = false;
      els.empty.hidden = true;
      var fragment = document.createDocumentFragment();
      for (var i = 0; i < sorted.length; i++) {
        fragment.appendChild(buildRow(sorted[i]));
      }
      els.tbody.appendChild(fragment);
    }
    syncControls();
    writeUrl();
  }

  function countMatchingDiscs(list) {
    var total = 0;
    for (var i = 0; i < list.length; i++) {
      var game = list[i];
      if (state.status === 'all' || state.status === 'manual') {
        total += game.variants.length;
      } else {
        total += game.statusCounts[state.status] || 0;
      }
    }
    return total;
  }

  function matchesGame(game) {
    if (state.status === 'manual') {
      if (!game.manualGrade) return false;
    } else if (state.status !== 'all') {
      if (!game.statusCounts[state.status]) return false;
    }
    if (state.region !== 'all' && game.regions.indexOf(state.region) === -1) return false;
    if (state.category !== 'all' && game.category !== state.category) return false;
    if (state.letter !== 'all' && game.letter !== state.letter) return false;
    if (state.biosOnly && !game.hasBiosOverride) return false;
    if (state.qKey) {
      var terms = state.qKey.split(' ');
      for (var i = 0; i < terms.length; i++) {
        if (terms[i] && game._haystack.indexOf(terms[i]) === -1) return false;
      }
    }
    return true;
  }

  function sortGames(list) {
    var sorted = list.slice();
    sorted.sort(function (a, b) {
      var cmp;
      if (state.sort === 'title-desc') {
        cmp = collator.compare(b._titleKey, a._titleKey);
      } else if (state.sort === 'status') {
        cmp = (b._statusRank - a._statusRank) || collator.compare(a._titleKey, b._titleKey);
      } else if (state.sort === 'depth') {
        cmp = (b.testDepth - a.testDepth) || collator.compare(a._titleKey, b._titleKey);
      } else if (state.sort === 'region') {
        cmp = (gameRegionRank(a) - gameRegionRank(b)) || collator.compare(a._titleKey, b._titleKey);
      } else {
        cmp = collator.compare(a._titleKey, b._titleKey);
      }
      return cmp;
    });
    return sorted;
  }

  function gameRegionRank(game) {
    var best = REGION_ORDER.length + 1;
    for (var i = 0; i < game.regions.length; i++) {
      var rank = regionRank(game.regions[i]);
      if (rank < best) best = rank;
    }
    return best;
  }

  function buildRow(game) {
    var row = el('tr', 'compat-row');
    var thumbCell = el('td', 'compat-cell-thumb');
    if (game.screenshot) {
      var img = el('img', 'compat-thumb');
      img.src = assetUrl(game.screenshot);
      img.alt = '';
      img.loading = 'lazy';
      img.decoding = 'async';
      img.width = 64;
      img.height = 48;
      thumbCell.appendChild(img);
    } else {
      thumbCell.appendChild(el('span', 'compat-thumb-placeholder', '3DO'));
    }

    var titleCell = el('td', 'compat-cell-title');
    titleCell.setAttribute('data-label', 'Game');
    var titleButton = el('button', 'compat-game-link', game.title);
    titleButton.type = 'button';
    titleButton.setAttribute('data-game-id', game.id);
    titleCell.appendChild(titleButton);
    var meta = el('div', 'compat-title-meta');
    if (game.variants.length > 1) {
      meta.appendChild(el('span', 'compat-variant-count', game.variants.length + ' discs'));
    }
    if (game.manualGrade) meta.appendChild(buildGradeBadge(game.manualGrade));
    titleCell.appendChild(meta);

    var regionCell = el('td', 'compat-cell-region');
    regionCell.setAttribute('data-label', 'Region');
    var regionValue = el('span', 'compat-cell-value');
    for (var r = 0; r < game.regions.length; r++) {
      regionValue.appendChild(el('span', 'compat-region', game.regions[r]));
    }
    regionCell.appendChild(regionValue);

    var categoryCell = el('td', 'compat-cell-category');
    categoryCell.setAttribute('data-label', 'Category');
    categoryCell.appendChild(el('span', 'compat-cell-value', game.category));

    var statusCell = el('td', 'compat-cell-status');
    statusCell.setAttribute('data-label', 'Status');
    var statusValue = el('span', 'compat-cell-value');
    for (var s = 0; s < STATUS_ORDER.length; s++) {
      var status = STATUS_ORDER[s];
      var count = game.statusCounts[status] || 0;
      if (count) statusValue.appendChild(buildStatusPill(status, count));
    }
    if (!statusValue.firstChild) statusValue.appendChild(buildStatusPill('unknown', 1));
    statusCell.appendChild(statusValue);

    var biosCell = el('td', 'compat-cell-bios');
    biosCell.setAttribute('data-label', 'BIOS');
    var biosValue = el('span', 'compat-cell-value');
    biosValue.appendChild(buildBiosBadge(game));
    biosCell.appendChild(biosValue);

    var depthCell = el('td', 'compat-cell-depth');
    depthCell.setAttribute('data-label', 'Tested');
    var depthValue = el('span', 'compat-cell-value');
    var depth = el('span', 'compat-depth', formatFrames(game.testDepth));
    depth.title = formatNumber(game.testDepth) + ' frames';
    depthValue.appendChild(depth);
    if (game.extended) depthValue.appendChild(el('span', 'compat-badge is-extended', 'extended'));
    depthCell.appendChild(depthValue);

    var actionCell = el('td', 'compat-cell-action');
    var details = el('button', 'compat-details-button', 'Details');
    details.type = 'button';
    details.setAttribute('data-game-id', game.id);
    details.setAttribute('aria-label', 'Open details for ' + game.title);
    actionCell.appendChild(details);

    append(row, thumbCell, titleCell, regionCell, categoryCell, statusCell, biosCell, depthCell, actionCell);
    return row;
  }

  function buildStatusPill(status, count) {
    var info = data.statuses && data.statuses[status] ? data.statuses[status] : null;
    var label = info ? info.label : status;
    var pill = el('span', 'compat-pill is-' + status);
    if (info && info.description) pill.title = info.description;
    pill.appendChild(el('span', 'compat-dot'));
    pill.appendChild(document.createTextNode(label));
    if (count > 1) pill.appendChild(el('span', 'compat-pill-count', String(count)));
    return pill;
  }

  function buildGradeBadge(grade) {
    var key = cssKey(grade);
    var badge = el('span', 'compat-grade is-' + key);
    badge.appendChild(el('span', 'compat-grade-star', '★'));
    badge.appendChild(document.createTextNode(grade));
    badge.appendChild(el('span', 'compat-grade-kind', 'manual'));
    return badge;
  }

  function buildBiosBadge(game) {
    var info = game._bios;
    var label = 'Default';
    var className = 'compat-bios is-default';
    if (info.values.length && !info.hasDefault) {
      label = 'Override';
      className = 'compat-bios is-override';
    } else if (info.values.length && info.hasDefault) {
      label = 'Mixed';
      className = 'compat-bios is-mixed';
    }
    var badge = el('span', className, label);
    badge.title = info.values.length ? info.values.join(' + ') : 'panafz1.bin';
    return badge;
  }

  function findGame(id) {
    for (var i = 0; i < games.length; i++) {
      if (games[i].id === id) return games[i];
    }
    return null;
  }

  function openGame(id, fromUrl) {
    var game = findGame(id);
    if (!game) return;
    if (!els.dialog.open) lastFocus = document.activeElement;
    state.game = id;
    renderDialog(game);
    document.body.classList.add('compat-dialog-open');
    if (typeof els.dialog.showModal === 'function') {
      if (!els.dialog.open) els.dialog.showModal();
    } else {
      els.dialog.setAttribute('open', '');
    }
    if (els.dialogClose) els.dialogClose.focus();
    if (!fromUrl) writeUrl();
  }

  function closeDialog() {
    if (!els.dialog) return;
    if (typeof els.dialog.close === 'function' && els.dialog.open) {
      els.dialog.close();
    } else if (els.dialog.hasAttribute('open')) {
      els.dialog.removeAttribute('open');
      onDialogClose();
    }
  }

  function onDialogClose() {
    document.body.classList.remove('compat-dialog-open');
    state.game = null;
    writeUrl();
    if (lastFocus && typeof lastFocus.focus === 'function') {
      lastFocus.focus();
      lastFocus = null;
    }
  }

  function renderDialog(game) {
    els.dialogTitle.textContent = game.title;
    var subParts = [game.category];
    if (game.regions && game.regions.length) subParts.push(game.regions.join(', '));
    subParts.push(game.manualGrade ? game.manualGrade + ' (manual)' : 'Not manually graded');
    els.dialogSub.textContent = subParts.join(' · ');

    var body = els.dialogBody;
    body.innerHTML = '';

    var summary = el('div', 'compat-summary');
    addSummary(summary, 'Discs', String(game.variants.length));
    addSummary(summary, 'Automated', statusSummary(game));
    addSummary(summary, 'Test depth', formatNumber(game.testDepth) + ' frames');
    addSummary(summary, 'BIOS', game._bios.values.length ? game._bios.values.join(' + ') : 'Default (panafz1.bin)');
    body.appendChild(summary);

    var variants = el('div', 'compat-variants');
    for (var i = 0; i < game.variants.length; i++) {
      variants.appendChild(buildVariantCard(game.variants[i]));
    }
    body.appendChild(variants);

    body.appendChild(buildReferenceSection(game));

    var actions = el('div', 'compat-dialog-actions');
    var report = el('a', 'compat-action is-primary', 'Report a problem');
    report.href = 'https://github.com/purelogiccode/5DO/issues/new?template=compatibility_report.yml&title=' +
      encodeURIComponent('[Compatibility] ' + game.title);
    report.target = '_blank';
    report.rel = 'noopener';
    var raw = el('a', 'compat-action', 'View raw results');
    raw.href = 'GameCompatibilityResults.html';
    var copy = el('button', 'compat-action', 'Copy link');
    copy.type = 'button';
    copy.addEventListener('click', function () {
      copyLink(copy);
    });
    append(actions, report, raw, copy);
    body.appendChild(actions);
  }

  function addSummary(parent, label, value) {
    var item = el('div', 'compat-summary-item');
    item.appendChild(el('span', 'compat-summary-label', label));
    item.appendChild(el('span', 'compat-summary-value', value));
    parent.appendChild(item);
  }

  function statusSummary(game) {
    var parts = [];
    for (var i = 0; i < STATUS_ORDER.length; i++) {
      var status = STATUS_ORDER[i];
      var count = game.statusCounts[status] || 0;
      if (count) {
        var info = data.statuses && data.statuses[status] ? data.statuses[status] : null;
        parts.push((info ? info.label : status) + ' (' + count + ')');
      }
    }
    return parts.length ? parts.join(', ') : 'Unknown';
  }

  function buildVariantCard(variant) {
    var card = el('article', 'compat-variant');
    var shot = el('div', 'compat-variant-shot');
    if (variant.screenshot) {
      var link = el('a');
      link.href = assetUrl(variant.screenshot);
      link.target = '_blank';
      link.rel = 'noopener';
      var img = el('img');
      img.src = assetUrl(variant.screenshot);
      img.alt = 'Final frame of ' + variant.disc;
      img.loading = 'lazy';
      img.decoding = 'async';
      img.width = 320;
      img.height = 240;
      link.appendChild(img);
      shot.appendChild(link);
    } else {
      shot.appendChild(el('span', 'compat-variant-placeholder', 'No screenshot'));
    }

    var body = el('div', 'compat-variant-body');
    body.appendChild(el('h3', '', variant.disc));

    var badges = el('div', 'compat-variant-badges');
    badges.appendChild(buildStatusPill(variant.status, 0));
    body.appendChild(badges);

    var facts = el('dl', 'compat-facts');
    addFact(facts, 'BIOS', variant.bios ? variant.bios + (variant.bios2 ? ' + ' + variant.bios2 : '') : 'Default (panafz1.bin)');
    addFact(facts, 'Frames', formatNumber(variant.frames));
    addFact(facts, 'Elapsed', variant.elapsed !== null && variant.elapsed !== undefined ? variant.elapsed.toFixed(2) + ' s' : '—');
    addFact(facts, 'Frame hash', variant.frameHash || '—');
    addFact(facts, 'Colors', variant.colors !== null && variant.colors !== undefined ? formatNumber(variant.colors) : '—');
    addFact(facts, 'Non-black', variant.nonBlack !== null && variant.nonBlack !== undefined ? formatNumber(variant.nonBlack) : '—');
    addFact(facts, 'PC', variant.pc !== null && variant.pc !== undefined ? formatNumber(variant.pc) : '—');
    body.appendChild(facts);

    body.appendChild(el('p', 'compat-reason', variant.reason || 'No reason recorded.'));

    var tech = el('details', 'compat-tech');
    tech.appendChild(el('summary', '', 'Technical evidence'));
    tech.appendChild(el('code', '', variant.evidence || 'No evidence path recorded.'));
    body.appendChild(tech);

    append(card, shot, body);
    return card;
  }

  function addFact(parent, label, value) {
    parent.appendChild(el('dt', '', label));
    parent.appendChild(el('dd', '', value));
  }

  function buildReferenceSection(game) {
    var section = el('section', 'compat-reference');
    section.appendChild(el('h3', '', '4DO reference'));
    var ref = game.reference;
    if (ref) {
      var line = [];
      if (ref.score !== null && ref.score !== undefined) line.push('Score ' + ref.score);
      else line.push('No score');
      if (ref.status) line.push(ref.status);
      if (ref.version) line.push('4DO ' + ref.version);
      section.appendChild(el('p', 'compat-ref-line', line.join(' · ')));
      if (ref.notes) section.appendChild(el('p', 'compat-ref-notes', ref.notes));
    } else {
      section.appendChild(el('p', 'compat-ref-notes', 'No 4DO reference entry matched this disc.'));
    }
    section.appendChild(el('h3', '', '5DO manual grade'));
    if (game.manualGrade) {
      var manual = game.manualGrade;
      if (game.manualNotes) manual += ' — ' + game.manualNotes;
      section.appendChild(el('p', 'compat-ref-line', manual));
    } else {
      section.appendChild(el('p', 'compat-ref-notes', 'Not manually graded yet. The automated result above is the only recorded status.'));
    }
    return section;
  }

  function copyLink(button) {
    var url = window.location.href;

    function done() {
      var original = button.textContent;
      button.textContent = 'Copied';
      button.disabled = true;
      setTimeout(function () {
        button.textContent = original;
        button.disabled = false;
      }, 1500);
    }

    function failed() {
      button.textContent = 'Copy failed';
      setTimeout(function () {
        button.textContent = 'Copy link';
      }, 1500);
    }

    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(url).then(done).catch(function () {
        fallbackCopy(url, done, failed);
      });
    } else {
      fallbackCopy(url, done, failed);
    }
  }

  function fallbackCopy(text, done, failed) {
    var textarea = document.createElement('textarea');
    textarea.value = text;
    textarea.setAttribute('readonly', '');
    textarea.style.position = 'fixed';
    textarea.style.opacity = '0';
    document.body.appendChild(textarea);
    textarea.select();
    var ok = false;
    try {
      ok = document.execCommand('copy');
    } catch (error) {
      ok = false;
    }
    document.body.removeChild(textarea);
    if (ok) done();
    else failed();
  }

  readUrl();
  loadData();
})();
