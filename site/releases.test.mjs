// Run: node site/releases.test.mjs
import assert from 'node:assert/strict';
import { channelOf, platformOf, pickChannels, heroChannel, label } from './releases.js';

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

console.log('releases.js: all checks passed');
