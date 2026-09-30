// Run at deploy by .github/workflows/pages.yml: node site/prerender.mjs (GITHUB_TOKEN avoids the 60/hour limit).
// Bakes the release panel and notes into index.html so crawlers and no-JS visitors see them.
// main.js still fetches live data on load and replaces both; this markup copies its renderChannel/renderChanges.
import { readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { PLATFORMS, HERO_CHANNEL, API, BLURB, pickChannels, heroChannel, label, mb, fmtDate } from './releases.js';

const attr = names => new RegExp(`\\s(?:${names})\\s*=\\s*("[^"]*"|'[^']*'|[^\\s>]*)`, 'gi');
const esc = s => String(s).replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c]);

// Same rules as main.js's safeFragment, without a DOM. GitHub already sanitises body_html; this is the backstop.
// ponytail: regex, not a parser; it misses entity-encoded "javascript:" and ">" inside attribute values.
// Parse with linkedom here if release notes ever come from anyone but the maintainers.
export function sanitize(html) {
  return html
    .replace(/<(script|style|iframe|object|embed|form)\b[\s\S]*?<\/\1\s*>/gi, '')
    .replace(/<\/?(script|style|iframe|object|embed|form|link|meta)\b[^>]*>/gi, '')
    // Release Markdown uses # and ##; demote them so they sit under the page's own h2 sections.
    .replace(/<(\/?)h[12]\b[^>]*>/gi, '<$1h3>')
    .replace(/<([a-z][a-z0-9]*)\b([^>]*?)\s*(\/?)>/gi, (_, tag, attrs, slash) => {
      attrs = attrs
        .replace(attr('on[\\w-]*'), '')
        .replace(/\s(href|src)\s*=\s*("\s*javascript:[^"]*"|'\s*javascript:[^']*'|javascript:[^\s>]*)/gi, '');
      const t = tag.toLowerCase();
      if (t === 'a') attrs = attrs.replace(attr('target|rel'), '') + ' target="_blank" rel="noopener"';
      if (t === 'img') attrs = attrs.replace(attr('loading'), '') + ' loading="lazy"';
      return `<${tag}${attrs}${slash}>`;
    })
    // Pasted screenshots come back as signed links that expire in 5 minutes; the page outlives that.
    .replace(/https:\/\/private-user-images\.githubusercontent\.com\/\d+\/\d+-([0-9a-f-]{36})\.\w+\?[^"'\s>]*/gi,
      'https://github.com/user-attachments/assets/$1');
}

// No visitor platform at build time, so no "mine" row. HERO_CHANNEL never resolves to nightly, so no commit list.
function renderChannel(picked, open) {
  const r = picked[open];
  if (!r) return `<p class="blurb">${open === 'rc' ? 'No release candidate right now. The stable build is the newest.' : 'No build in this channel yet.'}</p>`;
  const head = `<p class="blurb"><span class="mono">${esc(`${label(r)} · ${fmtDate(r.date)}`)}</span>${esc(BLURB[open])} <a href="${esc(r.url)}">Release page ↗</a></p>`;
  const rows = Object.keys(PLATFORMS).filter(k => r.assets[k]).map(k => {
    const { os, chip } = PLATFORMS[k];
    return `<tr><td>${os}</td><td>${chip}</td><td class="num">${mb(r.assets[k].size)}</td>`
      + `<td><a class="rowbtn" href="${esc(r.assets[k].url)}" aria-label="${esc(`Download ${label(r)} for ${os} ${chip}`)}">Download</a></td></tr>`;
  });
  if (!rows.length) return `${head}<p class="note">This release has no downloads attached yet.</p>`;
  return `${head}<div class="tbl"><table><thead><tr><th>SYSTEM</th><th>CHIP</th><th>SIZE</th>`
    + `<th><span class="visually-hidden">Download</span></th></tr></thead><tbody>${rows.join('')}</tbody></table></div>`;
}

const renderChanges = changes => changes.slice(0, 8).map((r, i) => `<details${i === 0 ? ' open' : ''}><summary>`
  + `<strong>${esc(r.name)}</strong><span class="mono">${esc(`${r.tag} · ${fmtDate(r.date)}`)}</span>`
  + `${r.channel === 'rc' ? '<span class="chip chip-rc">RC</span>' : ''}</summary>`
  + `<div class="notes">${sanitize(r.bodyHtml || '<p>No notes for this release.</p>')}</div></details>`).join('\n');

function fill(html, key, content) {
  const re = new RegExp(`(<!-- prerender:${key} -->)[\\s\\S]*?(<!-- /prerender:${key} -->)`);
  if (!re.test(html)) throw new Error(`index.html has no prerender:${key} markers`);
  return html.replace(re, (_, open, close) => `${open}\n${content}\n${close}`);
}

async function main() {
  // Throws on any failure, so the job stops and the last good deploy stays up.
  const headers = { Accept: 'application/vnd.github.html+json' };
  if (process.env.GITHUB_TOKEN) headers.Authorization = `Bearer ${process.env.GITHUB_TOKEN}`; // optional locally
  const res = await fetch(API, { headers });
  if (!res.ok) throw new Error(`GitHub answered ${res.status}`);
  const picked = pickChannels(await res.json());
  const open = heroChannel(picked, HERO_CHANNEL);

  const file = fileURLToPath(new URL('index.html', import.meta.url));
  let html = readFileSync(file, 'utf8');
  html = fill(html, 'channel', renderChannel(picked, open));
  html = fill(html, 'changes', renderChanges(picked.changes));
  html = html.replace(/<div id="channel-panel" role="tabpanel" aria-labelledby="tab-\w+">/,
    `<div id="channel-panel" role="tabpanel" aria-labelledby="tab-${open}" data-prerendered>`)
    .replace(`id="tab-${open}"`, `id="tab-${open}" aria-selected="true"`);
  writeFileSync(file, html);
  console.log(`Pre-rendered ${open} downloads and ${Math.min(picked.changes.length, 8)} release notes.`);
}

if (process.argv[1] === fileURLToPath(import.meta.url)) await main();
