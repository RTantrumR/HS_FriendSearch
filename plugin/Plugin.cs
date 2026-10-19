using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace HsFriendSearch
{
    // Name and version come from HsFriendSearch.csproj (BepInEx.PluginInfoProps), as the installer's do.
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> DumpFrame;

        private void Awake()
        {
            Log = Logger;
            DumpFrame = Config.Bind("Debug", "DumpFrame", false,
                "Write the friends list's object tree to BepInEx/friendlist_dump.txt each time the list starts.");
            new Harmony(MyPluginInfo.PLUGIN_GUID).PatchAll(typeof(Plugin).Assembly);
            Log.LogInfo("HS Friend Search loaded");
        }
    }
}
