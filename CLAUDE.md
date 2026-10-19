# HS Friend Search

A search bar at the top of Hearthstone's friends list: a BepInEx plugin, released as a setup exe. Separate
from HS Replay Watcher (`E:\PYPY\Test_HS_Server`), whose recon and client knowledge it reuses. The two
installers share BepInEx.

- **Client:** `E:\Games\Hearthstone`, client 37.0 (37.0.0.255130 in `.product.db`), Unity 6000.3.11 Mono x64, BepInEx 5.4.23.5 (shared with the
  Replay Watcher's and HS_CardExtractor's plugins; don't break their setup). Launch via Battle.net Play.
- **Layout:** `plugin/` (C#, net48; `dotnet build -c Release --no-incremental` builds into HS's
  `BepInEx\plugins`, so close HS first) · `tests/` (`dotnet run --project tests`, the match rule outside the
  game) · `installer/` · `images/` (README shots).
- **Match rule:** direct hits (anywhere, name starts first) above typo hits (4+ letters, `len/3` letters off a
  whole name, word or word start: the hsbg.cards rule). See [docs/search.md](docs/search.md).
- **Filter and strip:** hooks the game's virtualized list (`IsItemShowable`, `ItemsSortCompare`); the bar is a
  copy of the list's own header row. Traps: leave the text input's layer unset (else the text is invisible);
  never close the list from Esc (it swallowed the next click). See [docs/search.md](docs/search.md).
- **Installer:** `installer/build.ps1` → Inno Setup exe; BepInEx files are `sharedfile`-counted, so either
  addon's uninstall leaves BepInEx for the other. Admin only. See [docs/installer.md](docs/installer.md).
- **Repo:** public on GitHub; `docs/` is a nested private repo (`RTantrumR/HS_FriendSearch-docs`), ignored
  here. Version: `<Version>` in `plugin/HsFriendSearch.csproj` (plugin and exe both read it).
