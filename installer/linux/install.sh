#!/usr/bin/env bash
# HS Friend Search for Hearthstone running under Wine (Lutris, Bottles, Steam/Proton, plain Wine).
#   bash install.sh [path to the Hearthstone folder]
# Copies BepInEx (only the files the folder doesn't have yet) and the plugin into the Hearthstone folder, then
# tells Wine to load BepInEx's version.dll from there (a DLL override in that Wine prefix's user.reg).
# What it adds is listed in BepInEx/HSFriendSearch/linux-install.txt; uninstall.sh reads that list.
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
files="$here/files"
plugin="BepInEx/plugins/com.rtantrumr.friendsearch.dll"

die() { echo "Error: $*" >&2; exit 1; }

find_hearthstone() {
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
        find "$root" -maxdepth 8 -type f -path '*/drive_c/*' -name Hearthstone.exe -printf '%h\n' 2>/dev/null
    done | sort -u
}

if [ $# -ge 1 ]; then
    hs="${1%/}"
else
    mapfile -t found < <(find_hearthstone)
    if [ ${#found[@]} -eq 0 ]; then
        die "no Hearthstone found in the usual Wine prefixes. Run: bash install.sh \"/path/to/Hearthstone\""
    elif [ ${#found[@]} -gt 1 ]; then
        echo "Found more than one Hearthstone:" >&2
        printf '  %s\n' "${found[@]}" >&2
        die "pick one: bash install.sh \"/path/to/Hearthstone\""
    fi
    hs="${found[0]}"
fi
[ -f "$hs/Hearthstone.exe" ] || die "no Hearthstone.exe in $hs"
echo "Hearthstone: $hs"

# The Wine prefix is the folder holding drive_c.
case "$hs" in
    */drive_c/*) prefix="${hs%%/drive_c/*}" ;;
    *) prefix="" ;;
esac

state_dir="$hs/BepInEx/HSFriendSearch"
state="$state_dir/linux-install.txt"
mkdir -p "$state_dir"
[ -f "$state" ] || : > "$state"

# BepInEx and its doorstop: only where missing, so another mod's BepInEx stays as it is.
added=0
while IFS= read -r -d '' src; do
    rel="${src#"$files"/}"
    [ "$rel" = "$plugin" ] && continue
    dest="$hs/$rel"
    if [ ! -e "$dest" ]; then
        mkdir -p "$(dirname "$dest")"
        cp "$src" "$dest"
        grep -qxF "file $rel" "$state" || echo "file $rel" >> "$state"
        added=$((added + 1))
    fi
done < <(find "$files" -type f -print0)
echo "BepInEx: $added file(s) added (files already there were kept)"

mkdir -p "$hs/BepInEx/plugins"
cp "$files/$plugin" "$hs/$plugin"
mkdir -p "$state_dir/licenses"
cp "$here"/licenses/* "$state_dir/licenses/"
echo "Plugin installed"

# The DLL override: Wine prefers its own version.dll unless told to use the one in the game's folder.
override_help() {
    echo
    echo "Set the override yourself, then start Hearthstone from Battle.net as usual:"
    echo "  - Lutris: Configure > System options > Environment variables: WINEDLLOVERRIDES = version=n,b"
    echo "  - Steam (Proton): launch options: WINEDLLOVERRIDES=\"version=n,b\" %command%"
    echo "  - winecfg: Libraries > add \"version\" > Edit > Native then Builtin"
}

reg="$prefix/user.reg"
if [ -z "$prefix" ] || [ ! -f "$reg" ]; then
    echo "Couldn't find this Hearthstone's Wine prefix (its user.reg)."
    override_help
    exit 0
fi
if pgrep -x wineserver >/dev/null 2>&1; then
    echo "Wine is running (Battle.net or another program): the override can't be written now,"
    echo "Wine would overwrite it when it exits. Close Battle.net and rerun this script, or:"
    override_help
    exit 0
fi

# awk gets the strings through ENVIRON: -v would turn the doubled backslashes into single ones.
# In user.reg the key reads [Software\\Wine\\DllOverrides] (backslashes doubled), with an optional "#time=" line
# under it (Wine's server/registry.c: the timestamp after the bracket is optional too).
header='[Software\\Wine\\DllOverrides]'
want='"version"="native,builtin"'
in_section='index($0, h) == 1 {s = 1; next} /^\[/ {s = 0}'
current="$(H="$header" awk 'BEGIN {h = ENVIRON["H"]} '"$in_section"' s && /^"version"=/ {print; exit}' "$reg")"

if [ "$current" = "$want" ]; then
    echo "Wine override: already set"
else
    cp "$reg" "$reg.hsfriendsearch.bak"
    if [ -n "$current" ]; then
        # Set differently by someone: replaced, and theirs kept for uninstall.sh to put back.
        grep -q '^override-was ' "$state" || echo "override-was $current" >> "$state"
        H="$header" W="$want" awk 'BEGIN {h = ENVIRON["H"]; w = ENVIRON["W"]}
            index($0, h) == 1 {s = 1; print; next}
            /^\[/ {s = 0}
            s && /^"version"=/ {print w; next}
            {print}' "$reg.hsfriendsearch.bak" > "$reg"
        echo "Wine override: set (was $current)"
    elif grep -qF "$header" "$reg"; then
        # The key exists: the value goes right under its header (and its #time line, if any).
        H="$header" W="$want" awk 'BEGIN {h = ENVIRON["H"]; w = ENVIRON["W"]}
            p && /^#time=/ {print; next}
            p {print w; p = 0}
            {print}
            index($0, h) == 1 {p = 1}
            END {if (p) print w}' "$reg.hsfriendsearch.bak" > "$reg"
        grep -qxF "override added" "$state" || echo "override added" >> "$state"
        echo "Wine override: set"
    else
        printf '\n%s %s\n%s\n' "$header" "$(date +%s)" "$want" >> "$reg"
        grep -qxF "override added" "$state" || echo "override added" >> "$state"
        echo "Wine override: set"
    fi
fi
echo "Wine prefix: $prefix (backup of its user.reg: user.reg.hsfriendsearch.bak)"
echo
echo "Done. Start Hearthstone from Battle.net as usual: the friends list has a search bar at its top."
