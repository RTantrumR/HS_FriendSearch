HS Friend Search for Linux (Hearthstone through Wine: Lutris, Bottles, Steam/Proton or plain Wine)

Install
  1. Close Battle.net.
  2. In this folder, run:   bash install.sh
     It looks for Hearthstone in the usual Wine prefixes. If it finds none, or more than one, give the path:
                            bash install.sh "/path/to/drive_c/Program Files (x86)/Hearthstone"
  3. Start Hearthstone from Battle.net as usual.

  The script copies BepInEx (the mod loader; files Hearthstone already has are kept) and the plugin into the
  Hearthstone folder, and tells Wine to load BepInEx from there: a "version" DLL override, native first, in
  that prefix. A backup of the prefix's user.reg is kept beside it as user.reg.hsfriendsearch.bak.
  If it can't write the override, it shows how to set it in Lutris, Steam or winecfg.

Uninstall
  bash uninstall.sh   (or with the same path as above)
  The plugin is removed. The BepInEx files and the Wine override the install added are removed too, unless
  another BepInEx plugin is still installed.

No search bar? Check that BepInEx/LogOutput.log in the Hearthstone folder contains "HS Friend Search loaded".
If the file doesn't exist, BepInEx didn't start: check the "version" DLL override (native, then builtin).
