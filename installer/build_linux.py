"""Builds installer/out/HSFriendSearch-<version>-linux.tar.gz from installer/stage (made by build.ps1 first).

    py -3 installer/build_linux.py

The archive holds files/ (BepInEx + the plugin, laid out as in the Hearthstone folder), install.sh,
uninstall.sh, licenses/ (third-party, plus LICENSE.txt and NOTICE.txt) and README.txt. tarfile writes the
scripts with LF endings and the executable bit, which a Windows tar can't.
"""
import io
import tarfile
import time
import xml.etree.ElementTree as ET
from pathlib import Path

here = Path(__file__).resolve().parent
repo = here.parent
stage = here / "stage"

version = next(e.text for e in ET.parse(repo / "plugin" / "HsFriendSearch.csproj").iter("Version"))
top = f"HSFriendSearch-{version}-linux"
out = here / "out" / f"{top}.tar.gz"
now = int(time.time())


def add_bytes(tar, name, data, mode=0o644):
    info = tarfile.TarInfo(f"{top}/{name}")
    info.size, info.mode, info.mtime = len(data), mode, now
    tar.addfile(info, io.BytesIO(data))


def lf(path):
    return path.read_bytes().replace(b"\r\n", b"\n")


if not (stage / "plugin" / "com.rtantrumr.friendsearch.dll").exists():
    raise SystemExit("installer/stage is missing: run installer/build.ps1 first")

out.parent.mkdir(exist_ok=True)
with tarfile.open(out, "w:gz") as tar:
    for src in sorted((stage / "bepinex").rglob("*")):
        if src.is_file():
            add_bytes(tar, f"files/{src.relative_to(stage / 'bepinex').as_posix()}", src.read_bytes())
    add_bytes(tar, "files/BepInEx/plugins/com.rtantrumr.friendsearch.dll",
              (stage / "plugin" / "com.rtantrumr.friendsearch.dll").read_bytes())
    for script in ("install.sh", "uninstall.sh"):
        add_bytes(tar, script, lf(here / "linux" / script), 0o755)
    add_bytes(tar, "README.txt", lf(here / "linux" / "README.txt"))
    for lic in sorted((here / "licenses").glob("*.txt")):
        add_bytes(tar, f"licenses/{lic.name}", lf(lic))
    add_bytes(tar, "licenses/LICENSE.txt", lf(repo / "LICENSE"))
    add_bytes(tar, "licenses/NOTICE.txt", lf(repo / "NOTICE"))
print(out, out.stat().st_size)
