// Run: node site/releases.test.mjs
import assert from 'node:assert/strict';
import { channelOf, platformOf, pickChannels, heroChannel, label } from './releases.js';
import { sanitize } from './prerender.mjs';

const rel = (tag, published_at, extra = {}) => ({
  tag_name: tag, name: tag, published_at, prerelease: false, draft: false, html_url: '', assets: [], ...extra,
});
const asset = (name, updated_at = '2026-01-01T00:00:00Z') => ({ name, size: 1048576 * 88, browser_download_url: name, updated_at });

// Channels come from the tag, not the flag.
assert.equal(channelOf(rel('v2.0.0-rc6', '')), 'rc');
assert.equal(channelOf(rel('v1.0.0-rc10.1_bugfix', '')), 'rc');
assert.equal(channelOf(rel('v1.0.0-rc9-3', '')), 'rc');
assert.equal(channelOf(rel('nightly-master', '', { prerelease: true })), 'nightly');
assert.equal(channelOf(rel('v0.9.2-beta-fix.3', '', { prerelease: true })), null);
assert.equal(channelOf(rel('v1.1.6', '')), 'stable');

// Both asset naming schemes; debug builds are dropped.
assert.equal(platformOf('win-x64.zip'), 'win-x64');
assert.equal(platformOf('ThirtyDollarTools-osx-arm64-Release.zip'), 'osx-arm64');
assert.equal(platformOf('ThirtyDollarTools-linux-x64-Debug.zip'), null);

// Today's shape: stale nightly published_at, RC newer than Stable.
const today = [
  rel('nightly-master', '2025-12-06T16:47:16Z', { prerelease: true, assets: [
    asset('ThirtyDollarTools-win-x64-Release.zip', '2026-09-17T00:12:30Z'),
    asset('ThirtyDollarTools-win-x64-Debug.zip', '2026-09-17T00:10:00Z'),
  ] }),
  rel('v2.0.0-rc6', '2026-08-19T21:58:43Z', { assets: [asset('ThirtyDollarTools-win-x64-Release.zip')] }),
  rel('v2.0.0-rc5', '2026-08-10T10:56:32Z'),
  rel('v1.1.6', '2024-09-17T17:23:37Z', { assets: [asset('win-x64.zip')] }),
  rel('v0.9.3', '2023-01-01T00:00:00Z', { prerelease: true }),
];
let p = pickChannels(today);
assert.equal(p.stable.tag, 'v1.1.6');
assert.equal(p.rc.tag, 'v2.0.0-rc6');
assert.equal(p.nightly.date, '2026-09-17T00:12:30Z');
assert.deepEqual(Object.keys(p.nightly.assets), ['win-x64']);
assert.deepEqual(p.changes.map(r => r.tag), ['v2.0.0-rc6', 'v2.0.0-rc5', 'v1.1.6']);
assert.equal(heroChannel(p, 'rc'), 'rc');
assert.equal(label(p.rc), '2.0.0 RC 6');

// After 2.0.0 ships: the RC tab empties and the hero falls back to Stable.
p = pickChannels([rel('v2.0.0', '2026-10-01T00:00:00Z'), ...today]);
assert.equal(p.stable.tag, 'v2.0.0');
assert.equal(p.rc, null);
assert.equal(heroChannel(p, 'rc'), 'stable');

// prerender.mjs's DOM-free copy of main.js's safeFragment.
assert.equal(sanitize('<h1 dir="auto">A</h1><h2>B</h2><h3>C</h3>'), '<h3>A</h3><h3>B</h3><h3>C</h3>');
assert.equal(sanitize('<p onclick="x()">hi<script>alert(1)</script><style>p{}</style><link rel="x"></p>'), '<p>hi</p>');
assert.equal(sanitize('<a href="javascript:alert(1)" target="_self">x</a>'), '<a target="_blank" rel="noopener">x</a>');
assert.equal(sanitize('<p>set only= 3 and onload=4</p>'), '<p>set only= 3 and onload=4</p>'); // text is left alone
assert.equal(sanitize('<img src="https://private-user-images.githubusercontent.com/88944096/626700124-26e34312-d459-4549-8095-61732e8054d0.png?jwt=abc.def" />'),
  '<img src="https://github.com/user-attachments/assets/26e34312-d459-4549-8095-61732e8054d0" loading="lazy"/>');

console.log('releases.js, prerender.mjs: all checks passed');
