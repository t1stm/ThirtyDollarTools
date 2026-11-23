import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { join } from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';

const upstream = 'https://thirtydollar.website';
const twemoji = 'https://cdnjs.cloudflare.com/ajax/libs/twemoji/14.0.2/72x72';
const pngSignature = Buffer.from('89504e470d0a1a0a', 'hex');
const assetId = /^[a-zA-Z0-9_-]+$/;

async function download(url) {
  for (let attempt = 1; ; attempt++) {
    try {
      const response = await fetch(url, { signal: AbortSignal.timeout(30_000) });
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      return Buffer.from(await response.arrayBuffer());
    } catch (error) {
      if (attempt === 3) throw new Error(`Could not download ${url}`, { cause: error });
      await delay(attempt * 500);
    }
  }
}

async function stageAssets(destination) {
  const catalog = await download(`${upstream}/sounds.json`);
  const sounds = JSON.parse(catalog);
  if (!Array.isArray(sounds) || sounds.length === 0) throw new Error('The sound catalog is empty or invalid');

  const assets = new Map();
  for (const { id, emoji } of sounds) {
    if (typeof id !== 'string' || !assetId.test(id) || assets.has(`sounds/${id}.wav`)) {
      throw new Error(`Invalid or duplicate sound ID ${JSON.stringify(id)}`);
    }
    if (emoji != null && (typeof emoji !== 'string' || emoji.length === 0)) {
      throw new Error(`Invalid emoji for sound ${id}`);
    }
    assets.set(`sounds/${id}.wav`, `${upstream}/sounds/${id}.wav`);
    assets.set(`icons/${id}.png`, emoji == null
      ? `${upstream}/icons/${id}.png`
      : `${twemoji}/${emoji.codePointAt(0).toString(16)}.png`);
  }

  // The desktop loader already defines which action icons the Visualizer uses.
  const holder = await readFile(new URL('../../Converter/ThirtyDollarConverter/SampleHolder.cs', import.meta.url), 'utf8');
  const actionList = holder.match(/\bActionsArray\s*=\s*\[([^\]]+)\]/)?.[1];
  const actions = [...(actionList ?? '').matchAll(/"(action_[a-zA-Z0-9_-]+)"/g)].map(match => match[1]);
  if (actions.length === 0) throw new Error('Could not read the Visualizer action icons from SampleHolder');
  for (const action of actions) assets.set(`assets/${action}.png`, `${upstream}/assets/${action}.png`);

  await mkdir(destination);
  for (const folder of ['sounds', 'icons', 'assets']) await mkdir(join(destination, folder));

  const pending = assets.entries();
  await Promise.all(Array.from({ length: 8 }, async () => {
    for (const [path, url] of pending) {
      const bytes = await download(url);
      const valid = path.endsWith('.wav')
        ? bytes.toString('ascii', 0, 4) === 'RIFF' && bytes.toString('ascii', 8, 12) === 'WAVE'
        : bytes.subarray(0, pngSignature.length).equals(pngSignature);
      if (!valid) throw new Error(`Unexpected file format for ${url}`);
      await writeFile(join(destination, path), bytes);
    }
  }));

  await writeFile(join(destination, 'sounds.json'), catalog);
  console.log(`Staged ${sounds.length} sounds and ${sounds.length + actions.length} icons in ${destination}`);
}

const destination = process.argv[2];
if (!destination) throw new Error('Usage: node stage-visualizer-assets.mjs <new destination directory>');
await stageAssets(destination);
