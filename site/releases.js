// Pure release logic: no DOM, so releases.test.mjs can run it under node.
// Same rules as Visualizer/Shared/Updates/UpdateChecker.cs: compare by date, nightlies by tag prefix.

export const PLATFORMS = {
  'win-x64': { os: 'Windows', chip: 'x64' },
  'linux-x64': { os: 'Linux', chip: 'x64' },
  'osx-arm64': { os: 'macOS', chip: 'Apple silicon' },
  'osx-x64': { os: 'macOS', chip: 'Intel' },
};

// Decided by tag, never by the prerelease flag: every RC so far is published as a normal release.
export function channelOf(release) {
  if (release.tag_name.startsWith('nightly-')) return 'nightly';
  if (/-rc\d/i.test(release.tag_name)) return 'rc';
  if (release.prerelease) return null; // the v0.9.x betas
  return 'stable';
}

// Names changed between versions: "win-x64.zip" in 1.1.6, "ThirtyDollarTools-win-x64-Release.zip" in 2.0.
export function platformOf(assetName) {
  if (/-debug/i.test(assetName)) return null;
  const m = assetName.match(/(win|linux|osx)-(x64|arm64)/);
  return m ? m[0] : null;
}

function normalise(release) {
  const channel = channelOf(release);
  const assets = {};
  for (const a of release.assets) {
    const rid = platformOf(a.name);
    if (rid) assets[rid] = { size: a.size, url: a.browser_download_url };
  }
  // A nightly's release is edited in place, so published_at goes stale; its newest upload is the build date.
  const date = channel === 'nightly' && release.assets.length
    ? release.assets.map(a => a.updated_at).sort().at(-1)
    : release.published_at;
  return {
    channel, assets, date,
    tag: release.tag_name,
    name: release.name || release.tag_name,
    url: release.html_url,
    bodyHtml: release.body_html || '',
  };
}

export function pickChannels(raw) {
  const all = raw.filter(r => !r.draft && channelOf(r)).map(normalise).sort((a, b) => b.date.localeCompare(a.date));
  const newest = ch => all.find(r => r.channel === ch) || null;
  const stable = newest('stable');
  const rc = newest('rc');
  return {
    stable,
    // An RC older than Stable is outdated (e.g. every 2.0 RC once 2.0.0 ships), so the tab goes empty.
    rc: rc && (!stable || rc.date > stable.date) ? rc : null,
    nightly: newest('nightly'),
    changes: all.filter(r => r.channel !== 'nightly'),
  };
}

export const heroChannel = (picked, preferred) => picked[preferred] ? preferred : 'stable';

export function label(r) {
  const v = r.tag.replace(/^v/, '');
  if (r.channel === 'rc') return v.replace(/-rc(\d+).*$/i, ' RC $1');
  if (r.channel === 'nightly') return 'Nightly';
  return v;
}

export const mb = bytes => Math.round(bytes / 1048576) + ' MB';
