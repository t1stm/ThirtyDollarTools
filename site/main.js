import { PLATFORMS, pickChannels, heroChannel, label, mb } from './releases.js';

// Which channel the hero's red button offers. Switch to 'stable' once 2.0.0 ships
// (docs/handover/github-pages-plan.md §9). With no RC newer than Stable it falls back on its own.
const HERO_CHANNEL = 'rc';

const REPO = 'https://github.com/t1stm/ThirtyDollarTools';
const API = 'https://api.github.com/repos/t1stm/ThirtyDollarTools/releases?per_page=100';
const CACHE_KEY = 'tdt-releases-v1';
const CACHE_MS = 5 * 60 * 1000; // reuse the stored response without asking GitHub for 5 minutes
const $ = id => document.getElementById(id);
const reduceMotion = matchMedia('(prefers-reduced-motion: reduce)').matches;

/* ---------------- Hero video: the page follows the cover's !bg ---------------- */

function setupHero() {
  const video = $('hero-video');
  const root = document.documentElement;
  const controls = $('stage-controls');
  const playBtn = $('play');
  const soundBtn = $('sound');
  const sources = video.querySelectorAll('source');
  let userPaused = reduceMotion;
  let visible = true;
  let sampler = null;

  // The Visualizer's margin left of the grid is always pure field colour; sample one pixel there.
  function sampleFrame() {
    if (!sampler) {
      sampler = document.createElement('canvas').getContext('2d', { willReadFrequently: true });
      sampler.canvas.width = sampler.canvas.height = 1;
    }
    const x = Math.round(video.videoWidth * 0.025);
    const y = Math.round(video.videoHeight * 0.476);
    sampler.drawImage(video, x, y, 1, 1, 0, 0, 1, 1);
    const [r, g, b] = sampler.getImageData(0, 0, 1, 1).data;
    root.style.setProperty('--field', `rgb(${r} ${g} ${b})`);
  }
  function tick() {
    if (visible && !video.paused) sampleFrame();
    if ('requestVideoFrameCallback' in video) video.requestVideoFrameCallback(tick);
    else requestAnimationFrame(tick);
  }

  function syncPlayButton() {
    const paused = video.paused;
    playBtn.classList.toggle('paused', paused);
    playBtn.setAttribute('aria-label', paused ? 'Play video' : 'Pause video');
  }
  const play = () => video.play().catch(() => syncPlayButton());

  function start() {
    controls.hidden = false;
    tick();
    if (!userPaused) play();
    syncPlayButton();
  }
  // main.js is a deferred module, so the metadata may already be in by the time this runs.
  if (video.readyState >= 1) start();
  else video.addEventListener('loadedmetadata', start, { once: true });
  video.addEventListener('play', syncPlayButton);
  video.addEventListener('pause', syncPlayButton);
  // No recording yet (or a failed load): keep the poster and the resting colour.
  sources[sources.length - 1].addEventListener('error', () => { controls.hidden = true; });

  playBtn.addEventListener('click', () => {
    userPaused = !video.paused;
    if (userPaused) video.pause(); else play();
  });

  // Always starts silent; the speaker icon is the only way to sound. Not remembered between visits.
  soundBtn.addEventListener('click', () => {
    const on = video.muted;
    video.muted = !on;
    soundBtn.setAttribute('aria-pressed', String(on));
    soundBtn.setAttribute('aria-label', on ? 'Turn sound off' : 'Turn sound on');
  });

  // Out of view: pause and ease back to the resting colour, so nothing below flashes.
  new IntersectionObserver(([entry]) => {
    visible = entry.isIntersecting;
    if (!visible) {
      root.style.removeProperty('--field');
      if (!video.paused) video.pause();
    } else if (!userPaused && video.readyState >= 2) {
      play();
    }
  }, { threshold: 0.15 }).observe(video);
}

/* ---------------- OS detection ---------------- */

async function detectPlatform() {
  const ua = navigator.userAgent;
  const uad = navigator.userAgentData;
  if (uad?.mobile || /Android|iPhone|iPad|iPod/i.test(ua)) return 'phone';
  const p = (uad?.platform || navigator.platform || ua).toLowerCase();
  if (p.includes('win')) return 'win-x64';
  if (p.includes('mac')) {
    if (navigator.maxTouchPoints > 1) return 'phone'; // iPadOS reports itself as a Mac
    // Only Chromium can tell Intel from Apple silicon; everyone else gets Apple silicon.
    try {
      const { architecture } = await uad.getHighEntropyValues(['architecture']);
      if (architecture === 'x86') return 'osx-x64';
    } catch { /* not available */ }
    return 'osx-arm64';
  }
  if (p.includes('linux') || p.includes('x11')) return 'linux-x64';
  return null;
}

/* ---------------- Release data ---------------- */

async function loadReleases() {
  let cached = null;
  try { cached = JSON.parse(localStorage.getItem(CACHE_KEY)); } catch { /* storage blocked */ }
  if (cached && Date.now() - cached.savedAt < CACHE_MS) return cached.data;
  const headers = { Accept: 'application/vnd.github.html+json' };
  if (cached?.etag) headers['If-None-Match'] = cached.etag;

  let res;
  try { res = await fetch(API, { headers }); } catch (err) { if (cached) return cached.data; throw err; }
  if (res.status === 304 && cached) { // doesn't count against the 60/hour limit
    save({ ...cached, savedAt: Date.now() });
    return cached.data;
  }
  if (!res.ok) {
    if (cached) return cached.data;
    throw new Error(`GitHub answered ${res.status}`);
  }
  const data = await res.json();
  save({ etag: res.headers.get('ETag'), savedAt: Date.now(), data });
  return data;
}

function save(entry) {
  try { localStorage.setItem(CACHE_KEY, JSON.stringify(entry)); } catch { /* quota or blocked: just don't cache */ }
}

// GitHub already sanitises body_html; this strips anything active anyway before it goes in the page.
function safeFragment(html) {
  const t = document.createElement('template');
  t.innerHTML = html;
  t.content.querySelectorAll('script, style, iframe, object, embed, form, link, meta').forEach(n => n.remove());
  for (const el of t.content.querySelectorAll('*')) {
    for (const attr of [...el.attributes]) {
      const n = attr.name.toLowerCase();
      if (n.startsWith('on') || ((n === 'href' || n === 'src') && /^\s*javascript:/i.test(attr.value))) {
        el.removeAttribute(attr.name);
      }
    }
    if (el.tagName === 'A') { el.target = '_blank'; el.rel = 'noopener'; }
    if (el.tagName === 'IMG') el.loading = 'lazy';
  }
  return t.content;
}

const fmtDate = iso => new Date(iso).toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' });

const BLURB = {
  stable: 'Tested release, for everyday use.',
  rc: 'The next version, nearly finished. Report problems on GitHub or in the TDW Discord.',
  nightly: 'Built from every commit to master. Untested; things may break.',
};

/* ---------------- Rendering ---------------- */

function el(tag, props = {}, ...children) {
  const e = Object.assign(document.createElement(tag), props);
  e.append(...children.filter(c => c != null));
  return e;
}

function renderHero(picked, platform) {
  const ch = heroChannel(picked, HERO_CHANNEL);
  const r = picked[ch];
  const btn = $('hero-dl');
  const sub = $('hero-sub');
  const links = $('hero-links');
  links.replaceChildren();

  if (!r) return;
  const asset = platform && platform !== 'phone' ? r.assets[platform] : null;
  if (asset) {
    btn.textContent = `Download for ${PLATFORMS[platform].os}`;
    btn.href = asset.url;
    sub.textContent = `${label(r)} · ${mb(asset.size)} · ${fmtDate(r.date)}`;
  } else {
    btn.textContent = 'Choose your platform';
    btn.href = '#download';
    sub.textContent = label(r);
  }

  const items = [];
  for (const other of ['stable', 'rc', 'nightly']) {
    if (other === ch || !picked[other]) continue;
    const text = other === 'stable' ? `Stable ${label(picked.stable)}` : other === 'rc' ? label(picked.rc) : 'Nightly';
    items.push(el('button', { type: 'button', textContent: text, onclick: () => openChannel(other, true) }));
  }
  items.push(el('a', { href: '#download', textContent: platform === 'osx-arm64' ? 'Intel Mac?' : 'Other platforms' }));
  items.forEach((item, i) => { if (i) links.append(' · '); links.append(item); });
}

let state = null;

function openChannel(ch, focus) {
  state.open = ch;
  renderChannel();
  if (focus) {
    $('download').scrollIntoView({ behavior: reduceMotion ? 'auto' : 'smooth' });
    $('tab-' + ch).focus({ preventScroll: true });
  }
}

function renderChannel() {
  const { picked, platform, open } = state;
  for (const t of document.querySelectorAll('#channel-tabs [role="tab"]')) {
    const on = t.dataset.ch === open;
    t.setAttribute('aria-selected', String(on));
    t.tabIndex = on ? 0 : -1;
  }
  const panel = $('channel-panel');
  panel.setAttribute('aria-labelledby', 'tab-' + open);

  const r = picked[open];
  if (!r) {
    panel.replaceChildren(el('p', { className: 'blurb', textContent:
      open === 'rc' ? 'No release candidate right now. The stable build is the newest.' : 'No build in this channel yet.' }));
    return;
  }

  const head = el('p', { className: 'blurb' },
    el('span', { className: 'mono', textContent: `${label(r)} · ${fmtDate(r.date)}` }),
    BLURB[open] + ' ',
    el('a', { href: r.url, textContent: 'Release page ↗' }));

  const mine = platform && platform !== 'phone' ? platform : null;
  const order = Object.keys(PLATFORMS).filter(k => r.assets[k]).sort((a, b) => (b === mine) - (a === mine));
  const rows = order.map(k => el('tr', { className: k === mine ? 'mine' : '' },
    el('td', { textContent: PLATFORMS[k].os }),
    el('td', { textContent: PLATFORMS[k].chip }),
    el('td', { className: 'num', textContent: mb(r.assets[k].size) }),
    el('td', {}, el('a', { className: 'rowbtn', href: r.assets[k].url, textContent: 'Download',
      ariaLabel: `Download ${label(r)} for ${PLATFORMS[k].os} ${PLATFORMS[k].chip}` }))));

  const table = el('div', { className: 'tbl' }, el('table', {},
    el('thead', {}, el('tr', {},
      el('th', { textContent: 'SYSTEM' }), el('th', { textContent: 'CHIP' }),
      el('th', { textContent: 'SIZE' }), el('th', {}, el('span', { className: 'visually-hidden', textContent: 'Download' })))),
    el('tbody', {}, ...rows)));

  const parts = [head];
  if (platform === 'phone') parts.push(el('p', { className: 'note', textContent: 'These are desktop apps. Open this page on a computer to download.' }));
  parts.push(order.length ? table : el('p', { className: 'note', textContent: 'This release has no downloads attached yet.' }));

  if (open === 'nightly' && r.bodyHtml) {
    // The changelog builder lists commits; show the first 8.
    const frag = safeFragment(r.bodyHtml);
    const list = frag.querySelector('ul');
    if (list) {
      [...list.children].slice(8).forEach(li => li.remove());
      parts.push(el('details', { className: 'commits' },
        el('summary', { textContent: 'Latest commits in this build' }), list,
        el('a', { href: r.url, textContent: 'Full list on GitHub ↗' })));
    }
  }
  panel.replaceChildren(...parts);
}

function renderChanges(changes) {
  const box = $('changes-list');
  box.replaceChildren(...changes.slice(0, 8).map((r, i) => el('details', { open: i === 0 },
    el('summary', {},
      el('strong', { textContent: r.name }),
      el('span', { className: 'mono', textContent: `${r.tag} · ${fmtDate(r.date)}` }),
      r.channel === 'rc' ? el('span', { className: 'chip chip-rc', textContent: 'RC' }) : null),
    el('div', { className: 'notes' }, safeFragment(r.bodyHtml || '<p>No notes for this release.</p>')))));
}

// GitHub unreachable or rate-limited: links that always resolve server side, and a plain explanation.
function renderFallback(platform) {
  const rid = platform && platform !== 'phone' ? platform : null;
  const latest = rid => `${REPO}/releases/latest/download/ThirtyDollarTools-${rid}-Release.zip`;
  if (rid) {
    $('hero-dl').textContent = `Download for ${PLATFORMS[rid].os}`;
    $('hero-dl').href = latest(rid);
  }
  $('hero-sub').textContent = '';
  document.querySelector('#channel-tabs').hidden = true;
  const rows = Object.keys(PLATFORMS).map(k => el('tr', { className: k === rid ? 'mine' : '' },
    el('td', { textContent: PLATFORMS[k].os }), el('td', { textContent: PLATFORMS[k].chip }),
    el('td', {}, el('a', { className: 'rowbtn', href: latest(k), textContent: 'Download' }))));
  $('channel-panel').replaceChildren(
    el('p', { className: 'blurb' }, "Couldn't load release details from GitHub. The download buttons still work; sizes and notes are on the ",
      el('a', { href: `${REPO}/releases`, textContent: 'releases page ↗' }), '.'),
    el('div', { className: 'tbl' }, el('table', {}, el('tbody', {}, ...rows))));
  $('changes-list').replaceChildren(el('p', { className: 'note' }, 'Release notes are on ',
    el('a', { href: `${REPO}/releases`, textContent: 'GitHub ↗' }), '.'));
}

/* ---------------- Tabs ---------------- */

function arrowKeys(tablist, onPick) {
  tablist.addEventListener('keydown', e => {
    const tabs = [...tablist.querySelectorAll('[role="tab"]')];
    const i = tabs.indexOf(document.activeElement);
    const d = { ArrowRight: 1, ArrowLeft: -1 }[e.key];
    if (i < 0 || !d) return;
    e.preventDefault();
    const next = tabs[(i + d + tabs.length) % tabs.length];
    onPick(next);
    next.focus();
  });
}

function setupEditorTabs() {
  const list = $('editor-tabs');
  const pick = tab => {
    for (const t of list.querySelectorAll('[role="tab"]')) {
      const on = t === tab;
      t.setAttribute('aria-selected', String(on));
      t.tabIndex = on ? 0 : -1;
    }
    $('ep').setAttribute('aria-labelledby', tab.id);
    $('ep-img').src = tab.dataset.img;
    $('ep-img').alt = tab.dataset.alt;
    $('ep-cap').textContent = tab.dataset.cap;
  };
  list.addEventListener('click', e => { const t = e.target.closest('[role="tab"]'); if (t) pick(t); });
  arrowKeys(list, pick);
}

/* ---------------- Boot ---------------- */

setupHero();
setupEditorTabs();

const platform = await detectPlatform();
try {
  const picked = pickChannels(await loadReleases());
  state = { picked, platform, open: heroChannel(picked, HERO_CHANNEL) };
  renderHero(picked, platform);
  renderChannel();
  renderChanges(picked.changes);

  const chTabs = $('channel-tabs');
  chTabs.addEventListener('click', e => { const t = e.target.closest('[role="tab"]'); if (t) openChannel(t.dataset.ch); });
  arrowKeys(chTabs, t => openChannel(t.dataset.ch));
} catch (err) {
  console.warn('Release fetch failed:', err);
  renderFallback(platform);
}
