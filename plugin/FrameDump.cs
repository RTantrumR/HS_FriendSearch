using System;
using System.IO;
using System.Text;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace HsFriendSearch
{
    /// <summary>
    /// Dev tool (Debug.DumpFrame): writes the friends list's object tree, and the Add Friend popup's
    /// prefab, to BepInEx/friendlist_dump.txt when the list starts: positions, scales, bounds,
    /// materials, textures and texts, enough to place and dress the search strip without the game open.
    /// </summary>
    [HarmonyPatch(typeof(FriendListFrame), "Start")]
    internal static class FrameDump
    {
        private static void Postfix(FriendListFrame __instance)
        {
            if (!Plugin.DumpFrame.Value) return;
            try
            {
                var sb = new StringBuilder();
                var cam = BaseUI.Get()?.GetBnetCamera();
                sb.AppendLine($"screen {Screen.width}x{Screen.height}; bnet camera {cam?.name} ortho={cam?.orthographic} size={cam?.orthographicSize} pos={cam?.transform.position} rot={cam?.transform.eulerAngles}");
                var f = __instance;
                sb.AppendLine($"listInfo.topLeft {f.listInfo.topLeft?.position} bottomRight {f.listInfo.bottomRight?.position} bottomRightWithScrollbar {f.listInfo.bottomRightWithScrollbar?.position}");
                sb.AppendLine($"items pos {f.items.transform.position} clip {f.items.ClipSize}; me.nameText {f.me.nameText?.transform.position} '{f.me.nameText?.Text}'");
                sb.AppendLine();
                sb.AppendLine("== FriendListFrame ==");
                Walk(f.transform, "", sb);
                if (f.prefabs.addFriendFrame != null)
                {
                    sb.AppendLine();
                    sb.AppendLine("== prefab AddFriendFrame ==");
                    Walk(f.prefabs.addFriendFrame.transform, "", sb);
                }
                string path = Path.Combine(Paths.BepInExRootPath, "friendlist_dump.txt");
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
                Plugin.Log.LogInfo($"Friends list dump written to {path}");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Friends list dump: " + e);
            }
        }

        private static void Walk(Transform t, string indent, StringBuilder sb)
        {
            var go = t.gameObject;
            sb.Append(indent).Append(go.activeSelf ? "" : "(off) ").Append(go.name)
              .Append($"  layer={go.layer} lpos={t.localPosition} lrot={t.localEulerAngles} lscale={t.localScale} wpos={t.position}");
            sb.AppendLine();
            foreach (var c in go.GetComponents<Component>())
            {
                if (c == null || c is Transform) continue;
                sb.Append(indent).Append("  - ").Append(c.GetType().Name);
                switch (c)
                {
                    case Renderer r:
                        sb.Append($" bounds={r.bounds.center}/{r.bounds.size}");
                        foreach (var m in r.sharedMaterials)
                        {
                            if (m == null) continue;
                            var tex = m.HasProperty("_MainTex") ? m.mainTexture : null;
                            sb.Append($" [mat {m.name} shader={m.shader?.name} tex={tex?.name} {tex?.width}x{tex?.height} col={(m.HasProperty("_Color") ? m.color.ToString() : "-")}]");
                        }
                        break;
                    case MeshFilter mf:
                        sb.Append($" mesh={mf.sharedMesh?.name} {mf.sharedMesh?.bounds.size}");
                        break;
                    case UberText u:
                        sb.Append($" text='{u.Text}' size={u.Width}x{u.Height} font={u.GetLocalizedFont()?.name}");
                        break;
                    case BoxCollider b:
                        sb.Append($" center={b.center} size={b.size}");
                        break;
                    case TextField tf:
                        sb.Append($" topLeft={tf.inputTopLeft?.name} bottomRight={tf.inputBottomRight?.name} max={tf.maxCharacters}");
                        break;
                }
                sb.AppendLine();
            }
            for (int i = 0; i < t.childCount; i++) Walk(t.GetChild(i), indent + "  ", sb);
        }
    }
}
