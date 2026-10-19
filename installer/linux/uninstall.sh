#!/usr/bin/env bash
# Removes HS Friend Search from a Hearthstone running under Wine.
#   bash uninstall.sh [path to the Hearthstone folder]
# The plugin always goes. The BepInEx files and the Wine override that install.sh added (listed in
# BepInEx/HSFriendSearch/linux-install.txt) go too, unless another BepInEx plugin is still installed.
set -euo pipefail

plugin="BepInEx/plugins/com.rtantrumr.friendsearch.dll"

die() { echo "Error: $*" >&2; exit 1; }

find_installed() {
    local roots=(
        "$HOME/Games"
        "$HOME/.wine"
        "$HOME/.local/share/bottles/bottles"
        "$HOME/.var/app/com.usebottles.bottles/data/bottles/bottles"
        "$HOME/.local/share/Steam/steamapps/compatdata"
        "$HOME/.steam/steam/steamapps/compatdata"
        "$HOME/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/compatdata"
        "$HOME/.local/share/lutris"
    )
    local root
    for root in "${roots[@]}"; do
        [ -d "$root" ] || continue
        find "$root" -maxdepth 10 -type f -path '*/drive_c/*' -name com.rtantrumr.friendsearch.dll 2>/dev/null |
            sed 's|/BepInEx/plugins/com.rtantrumr.friendsearch.dll$||'
    done | sort -u
}

if [ $# -ge 1 ]; then
    hs="${1%/}"
else
    mapfile -t found < <(find_installed)
    if [ ${#found[@]} -eq 0 ]; then
        die "HS Friend Search isn't installed in the usual Wine prefixes. Run: bash uninstall.sh \"/path/to/Hearthstone\""
    elif [ ${#found[@]} -gt 1 ]; then
        echo "Found it in more than one Hearthstone:" >&2
        printf '  %s\n' "${found[@]}" >&2
        die "pick one: bash uninstall.sh \"/path/to/Hearthstone\""
    fi
    hs="${found[0]}"
fi
[ -f "$hs/Hearthstone.exe" ] || die "no Hearthstone.exe in $hs"
echo "Hearthstone: $hs"

state_dir="$hs/BepInEx/HSFriendSearch"
state="$state_dir/linux-install.txt"

rm -f "$hs/$plugin"
echo "Plugin removed"

others="$(find "$hs/BepInEx/plugins" -type f -name '*.dll' 2>/dev/null | head -1 || true)"
if [ ! -f "$state" ]; then
    rm -rf "$state_dir"
    echo "No install list: BepInEx and the Wine settings are left as they are."
    exit 0
fi
if [ -n "$others" ]; then
    rm -rf "$state_dir"
    echo "Another BepInEx plugin is still installed: BepInEx and the Wine override stay for it."
    exit 0
fi

case "$hs" in
    */drive_c/*) prefix="${hs%%/drive_c/*}" ;;
    *) prefix="" ;;
esac
reg="$prefix/user.reg"
header='[Software\\Wine\\DllOverrides]'

if grep -qx "override added" "$state" || grep -q '^override-was ' "$state"; then
    if [ -z "$prefix" ] || [ ! -f "$reg" ]; then
        echo "Couldn't find the Wine prefix: remove the \"version\" DLL override yourself (winecfg > Libraries)."
    elif pgrep -x wineserver >/dev/null 2>&1; then
        echo "Wine is running: close Battle.net and rerun this script to remove the Wine override."
        exit 1
    else
        # Back to how it was: the value install.sh replaced, or no "version" value at all.
        was="$(sed -n 's/^override-was //p' "$state" | head -1)"
        cp "$reg" "$reg.hsfriendsearch-uninstall.bak"
        H="$header" W="$was" awk 'BEGIN {h = ENVIRON["H"]; w = ENVIRON["W"]}
            index($0, h) == 1 {s = 1; print; next}
            /^\[/ {s = 0}
            s && /^"version"=/ {if (w != "") print w; next}
            {print}' "$reg.hsfriendsearch-uninstall.bak" > "$reg"
        if [ -n "$was" ]; then echo "Wine override: put back to $was"; else echo "Wine override: removed"; fi
    fi
fi

# The BepInEx files install.sh added, then the folders that are left empty.
removed=0
while IFS= read -r rel; do
    [ -n "$rel" ] || continue
    rm -f "$hs/$rel"
    removed=$((removed + 1))
done < <(sed -n 's/^file //p' "$state")
rm -rf "$state_dir"
for dir in BepInEx/plugins BepInEx/config BepInEx/unstripped_corlib BepInEx/core BepInEx; do
    [ -d "$hs/$dir" ] && rmdir "$hs/$dir" 2>/dev/null || true
done
echo "BepInEx: $removed file(s) removed (files that were there before stay)"
echo "Done."
