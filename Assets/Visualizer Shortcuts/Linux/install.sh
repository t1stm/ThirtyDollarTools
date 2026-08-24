#!/usr/bin/env bash
# Installs the desktop entries, the .tdwproj file type and the icon for the current
# user. Pass the folder holding the ThirtyDollarVisualizer binary; with no argument
# it looks for one next to this script, then in the repo's Release output.
set -euo pipefail

here=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
apps=${XDG_DATA_HOME:-$HOME/.local/share}/applications
mime=${XDG_DATA_HOME:-$HOME/.local/share}/mime
icons=${XDG_DATA_HOME:-$HOME/.local/share}/icons/hicolor/256x256/apps

if [ "${1:-}" = "--uninstall" ]; then
    rm -fv "$apps/thirty-dollar-visualizer.desktop" "$apps/thirty-dollar-editor.desktop" \
           "$apps/thirty-dollar-sequence.desktop" \
           "$mime/packages/thirty-dollar-visualizer.xml" "$icons/thirty-dollar-visualizer.png"
    update-mime-database "$mime"
    update-desktop-database "$apps"
    echo "Removed."
    exit 0
fi

dir=${1:-}
if [ -z "$dir" ]; then
    for candidate in "$here" "$here/../../../Visualizer/ThirtyDollarVisualizer/bin/Release/net10.0"; do
        [ -x "$candidate/ThirtyDollarVisualizer" ] && dir=$candidate && break
    done
fi
[ -n "$dir" ] || { echo "usage: $0 /path/to/folder/with/ThirtyDollarVisualizer" >&2; exit 1; }

dir=$(cd -- "$dir" && pwd)
bin=$dir/ThirtyDollarVisualizer
[ -x "$bin" ] || { echo "No ThirtyDollarVisualizer in $dir" >&2; exit 1; }

mkdir -p "$apps" "$mime/packages" "$icons"

# Path= points the app at its own folder: settings and Editor Backups are written
# relative to the working directory, which is the home folder on a menu launch.
for entry in thirty-dollar-visualizer thirty-dollar-editor thirty-dollar-sequence; do
    sed -e "s|@BIN@|$bin|g" -e "s|@DIR@|$dir|g" "$here/$entry.desktop" > "$apps/$entry.desktop"
done
cp "$here/thirty-dollar-visualizer.xml" "$mime/packages/"
cp "$here/thirty-dollar-visualizer.png" "$icons/"

update-mime-database "$mime"
update-desktop-database "$apps"
gtk-update-icon-cache -f -t "${XDG_DATA_HOME:-$HOME/.local/share}/icons/hicolor" 2>/dev/null || true
xdg-mime default thirty-dollar-editor.desktop application/x-tdwproj
xdg-mime default thirty-dollar-sequence.desktop application/x-tdw

echo "Installed, pointing at $bin"
