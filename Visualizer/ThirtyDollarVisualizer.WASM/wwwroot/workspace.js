export async function chooseWorkspace() {
    if (!window.showDirectoryPicker) throw new Error('This browser cannot open a writable local folder.');
    const root = await window.showDirectoryPicker({ mode: 'readwrite' });
    const entries = [];

    async function list(directory, prefix) {
        for await (const [name, handle] of directory.entries()) {
            const path = prefix + name;
            const isDirectory = handle.kind === 'directory';
            entries.push({ Path: path, IsDirectory: isDirectory });
            if (isDirectory) await list(handle, `${path}/`);
        }
    }

    await list(root, '');

    async function file(path, create) {
        const parts = path.split('/');
        if (parts.some(part => !part || part === '.' || part === '..' || part.includes('\\')))
            throw new Error('Invalid workspace path.');
        const name = parts.pop();
        let parent = root;
        for (const directory of parts) parent = await parent.getDirectoryHandle(directory);
        return parent.getFileHandle(name, { create });
    }

    return {
        entries,
        async read(path) {
            const handle = await file(path, false);
            return { bytes: new Uint8Array(await (await handle.getFile()).arrayBuffer()) };
        },
        async write(path, bytes) {
            const handle = await file(path, true);
            const stream = await handle.createWritable();
            try {
                await stream.write(bytes);
                await stream.close();
            } catch (error) {
                try { await stream.abort(); } catch { }
                throw error;
            }
        }
    };
}
