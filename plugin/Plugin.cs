using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace HsFriendSearch
{
    [BepInPlugin("com.rtantrumr.friendsearch", "HS Friend Search", "0.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> DumpFrame;

        private void Awake()
        {
            Log = Logger;
            DumpFrame = Config.Bind("Debug", "DumpFrame", false,
                "Write the friends list's object tree to BepInEx/friendlist_dump.txt each time the list starts.");
            new Harmony("com.rtantrumr.friendsearch").PatchAll(typeof(Plugin).Assembly);
            Log.LogInfo("HS Friend Search loaded");
        }
    }
}
