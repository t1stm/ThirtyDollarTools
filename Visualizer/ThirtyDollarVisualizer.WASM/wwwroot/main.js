import { dotnet } from './_framework/dotnet.js';
import { chooseWorkspace } from './workspace.js';

const canvas = document.getElementById('sundex');
const status = document.getElementById('status');
const message = document.getElementById('message');
const start = document.getElementById('start');
const verify = new URLSearchParams(location.search).has('verify');
const settingsKey = 'visualizer-settings';
let stopped = false;

function fail(error) {
    stopped = true;
    status.hidden = false;
    start.hidden = true;
    message.textContent = String(error);
    console.error(error);
}

function size() {
    const width = Math.max(1, canvas.clientWidth);
    const height = Math.max(1, canvas.clientHeight);
    const pixelsWide = Math.max(1, Math.round(width * devicePixelRatio));
    const pixelsHigh = Math.max(1, Math.round(height * devicePixelRatio));
    if (canvas.width !== pixelsWide) canvas.width = pixelsWide;
    if (canvas.height !== pixelsHigh) canvas.height = pixelsHigh;
    return [width, height, pixelsWide, pixelsHigh];
}

async function setup() {
    const runtime = await dotnet.create();
    let workspace;
    runtime.setModuleImports('visualizer', {
        setTitle: title => { document.title = title; },
        setCursor: cursor => { canvas.style.cursor = cursor; },
        writeClipboard: value => navigator.clipboard?.writeText(value).catch(console.warn),
        readSettings: () => {
            try { return localStorage.getItem(settingsKey); } catch { return null; }
        },
        writeSettings: text => {
            try { localStorage.setItem(settingsKey, text); } catch (error) { console.warn(error); }
        },
        readWorkspaceFile: path => workspace.read(path),
        writeWorkspaceFile: (path, bytes) => workspace.write(path, bytes)
    });
    await runtime.runMain(runtime.getConfig().mainAssemblyName, []);
    const exports = await runtime.getAssemblyExports(runtime.getConfig().mainAssemblyName);
    const visualizer = exports.ThirtyDollarVisualizer.WASM.Program;
    message.hidden = true;
    start.hidden = false;

    start.addEventListener('click', async () => {
        start.disabled = true;
        try {
            workspace = await chooseWorkspace();
        } catch (error) {
            start.disabled = false;
            if (error.name !== 'AbortError') {
                message.hidden = false;
                message.textContent = String(error);
            }
            return;
        }
        try {
            const initialSize = size();
            const assets = new URL('../visualizer-assets/', location.href);
            visualizer.Start(...initialSize, assets.href, JSON.stringify(workspace.entries), verify);
            window.miniaudio?.unlock?.();
            status.hidden = true;
            canvas.focus({ preventScroll: true });
            run(visualizer, initialSize);
        } catch (error) {
            message.hidden = false;
            fail(error);
        }
    });
}

function run(visualizer, dimensions) {
    let x = -1;
    let y = -1;
    let inside = false;
    let buttons = 0;
    let previousButtons = 0;
    let pressed = 0;
    let released = 0;
    let scrollX = 0;
    let scrollY = 0;

    function point(event) {
        const bounds = canvas.getBoundingClientRect();
        x = event.clientX - bounds.left;
        y = event.clientY - bounds.top;
        inside = x >= 0 && x < bounds.width && y >= 0 && y < bounds.height;
        pressed |= event.buttons & ~buttons;
        released |= buttons & ~event.buttons;
        buttons = event.buttons;
    }

    function cancelPointer() {
        released |= buttons;
        buttons = 0;
        inside = false;
        x = y = -1;
    }

    canvas.addEventListener('pointerdown', event => {
        if (!event.isPrimary) return;
        canvas.focus({ preventScroll: true });
        canvas.setPointerCapture(event.pointerId);
        window.miniaudio?.unlock?.();
        point(event);
    });
    canvas.addEventListener('pointermove', event => { if (event.isPrimary) point(event); });
    canvas.addEventListener('pointerup', event => { if (event.isPrimary) point(event); });
    canvas.addEventListener('pointerleave', event => { if (event.isPrimary && !buttons) cancelPointer(); });
    canvas.addEventListener('pointercancel', event => { if (event.isPrimary) cancelPointer(); });
    canvas.addEventListener('lostpointercapture', () => { if (buttons) cancelPointer(); });
    canvas.addEventListener('contextmenu', event => event.preventDefault());
    canvas.addEventListener('wheel', event => {
        event.preventDefault();
        const step = event.deltaMode === 0 ? 1 / 100 : event.deltaMode === 1 ? 1 / 3 : 1;
        scrollX -= event.deltaX * step;
        scrollY -= event.deltaY * step;
    }, { passive: false });

    canvas.addEventListener('keydown', event => {
        const printable = !event.ctrlKey && !event.metaKey && !event.altKey && [...event.key].length === 1;
        window.miniaudio?.unlock?.();
        const taken = visualizer.KeyDown(event.code, printable ? event.key : '', event.repeat, event.shiftKey,
            event.ctrlKey, event.altKey, event.metaKey, event.getModifierState('CapsLock'),
            event.getModifierState('NumLock'));
        if (taken) event.preventDefault();
    });
    canvas.addEventListener('keyup', event => visualizer.KeyUp(event.code));
    canvas.addEventListener('blur', () => visualizer.ClearKeys());
    window.addEventListener('blur', cancelPointer);
    canvas.addEventListener('webglcontextlost', () => fail('WebGL context lost. Reload to restart.'));

    const audioTimer = setInterval(() => {
        if (stopped) clearInterval(audioTimer);
        else visualizer.PumpAudio();
    }, 10);

    let lastFrame;
    function animate(now) {
        if (stopped) return;
        try {
            const next = size();
            if (next.some((value, index) => value !== dimensions[index])) {
                dimensions = next;
                visualizer.Resize(...dimensions);
            }
            const delta = lastFrame === undefined ? 0 : Math.min((now - lastFrame) / 1000, 0.1);
            visualizer.Tick(delta, x, y, scrollX, scrollY, buttons, previousButtons, pressed, released, inside);
            scrollX = scrollY = pressed = released = 0;
            previousButtons = buttons;
            lastFrame = now;
            if (verify) {
                const scene = visualizer.ActiveScene();
                canvas.dataset.scene = scene;
                if (scene === 'Visualizer') canvas.dataset.playback = visualizer.PlaybackMilliseconds();
            }
            requestAnimationFrame(animate);
        } catch (error) {
            message.hidden = false;
            fail(error);
        }
    }
    requestAnimationFrame(animate);
}

setup().catch(fail);
