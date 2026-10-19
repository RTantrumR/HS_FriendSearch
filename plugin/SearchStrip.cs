using System;
using HarmonyLib;
using UnityEngine;

namespace HsFriendSearch
{
    /// <summary>
    /// The search strip at the top of the friends list, under the player's own name. It is a copy of
    /// the list's own section header bar (the "FRIENDS - x/y Online" row), so it looks like the list;
    /// the list's top bone moves down by <see cref="Shift"/> to make room, and the frame's own layout
    /// (UpdateItemsList, run on every move and resize) keeps the list below it.
    ///
    /// Typing goes through the game's text input (UniversalInputManager.UseTextInput), focused as the
    /// list opens. Each keystroke sets the filter. Esc clears a search; on an empty one it lets the
    /// input go and does what Esc does without the addon. Closing the list (disable or destroy) drops the query.
    /// </summary>
    internal class SearchStrip : MonoBehaviour
    {
        /// <summary>The header bar's height in world units (from the dump) and how much taller the strip's bar is.</summary>
        private const float HeaderHeight = 8.44f, Grow = 1.4f, BarHeight = HeaderHeight * Grow;
        /// <summary>World units the list moves down: the strip's bar and the gap a header row keeps.</summary>
        private const float Shift = BarHeight + 1.56f;
        /// <summary>A header row's bar starts this far above the list's top bone (measured in the dump).</summary>
        private const float BarAboveTop = 0.56f;
        /// <summary>A header row's bar centre sits this far above the row's origin.</summary>
        private const float BarAboveOrigin = 0.09f;
        /// <summary>The header's pieces that belong to a foldable section: the fold arrows, hover glow and bones.</summary>
        private static readonly string[] SectionOnly =
            { "ArrowIdle", "ArrowSelected", "Arrow", "MouseOverHighlight", "FoldoutBone", "FoldinBone" };
        /// <summary>The header's own gold (from its material), for the typed text and the placeholder.</summary>
        private static readonly Color TextColor = new Color(0.914f, 0.820f, 0.424f, 1f);
        /// <summary>The grey the game gives the "#1234" of a BattleTag, for the match count.</summary>
        private static readonly Color CountColor = new Color(0.6f, 0.6f, 0.6f, 1f);
        /// <summary>World units at the bar's right end kept for the count, out of the typing area.</summary>
        private const float CountRoom = 12f;

        private static readonly Action<FriendListFrame> UpdateItemsList =
            AccessTools.MethodDelegate<Action<FriendListFrame>>(AccessTools.Method(typeof(FriendListFrame), "UpdateItemsList"));
        private static readonly Action<FriendListFrame> UpdateItemsView =
            AccessTools.MethodDelegate<Action<FriendListFrame>>(AccessTools.Method(typeof(FriendListFrame), "UpdateItemsView"));

        private FriendListFrame m_frame;
        private GameObject m_bar;
        private UberText m_label, m_count;
        private Renderer m_field;
        private string m_text = "";
        private bool m_typing, m_wantFocus;

        internal static void Attach(FriendListFrame frame)
        {
            if (frame.GetComponent<SearchStrip>() != null) return;
            var strip = frame.gameObject.AddComponent<SearchStrip>();
            strip.m_frame = frame;
            try
            {
                strip.Build();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Search strip: " + e);
                Destroy(strip);
            }
        }

        private void Build()
        {
            var top = m_frame.listInfo.topLeft;
            top.position -= new Vector3(0f, 0f, Shift);

            // A copy of the section header, made under an inactive holder so its scripts never wake.
            var holder = new GameObject("FriendSearchHolder");
            holder.SetActive(false);
            m_bar = Instantiate(m_frame.prefabs.headerItem.gameObject, holder.transform);
            m_bar.name = "FriendSearchStrip";
            DestroyImmediate(m_bar.GetComponent<FriendListItemHeader>());
            DestroyImmediate(m_bar.GetComponent<MobileFriendListItem>());
            var root = m_bar.transform.Find("Root");
            foreach (var name in SectionOnly)
            {
                var part = root.Find(name);
                if (part != null) DestroyImmediate(part.gameObject);
            }
            m_label = root.Find("Text").GetComponent<UberText>();
            // The count: the same text box, right-aligned, so it ends just inside the bar's right cap.
            m_count = Instantiate(m_label.gameObject, m_label.transform.parent).GetComponent<UberText>();
            m_count.name = "Count";
            m_count.Alignment = UberText.AlignmentOptions.Right;
            m_count.RichText = false;
            m_count.TextColor = CountColor;
            m_count.gameObject.SetActive(false);
            var slices = root.Find("FriendListNew_ItemHeader_3slice");
            m_field = slices.Find("FriendListNew_ItemHeader_Center_mesh").GetComponent<Renderer>();

            // Under the list's own transform, as the header rows are, so rotation and scale match theirs.
            m_bar.transform.SetParent(m_frame.items.transform, false);
            m_bar.transform.localRotation = Quaternion.identity;
            m_bar.transform.localScale = new Vector3(1f, 1f, -1f);
            Destroy(holder);
            SetLayer(m_bar.transform, m_frame.gameObject.layer);

            m_bar.AddComponent<PegUIElement>().AddEventListener(UIEventType.RELEASE, e => Focus());

            m_bar.SetActive(true);
            Grow3Slice(slices);
            Place();
            ShowLabel();
            // The frame's layout (ChatMgr sizes it after creating it) re-runs these with the moved bone;
            // a frame made early has no live TouchList yet and must wait for that.
            if (m_frame.items.IsInitialized)
            {
                UpdateItemsList(m_frame);
                UpdateItemsView(m_frame);
            }
        }

        /// <summary>Puts the bar where the list's first header row stood before the list moved down.</summary>
        internal void Place()
        {
            if (m_bar == null) return;
            var top = m_frame.listInfo.topLeft.position;
            var bottom = m_frame.listInfo.bottomRightWithScrollbar.position;
            var items = m_frame.items.transform;
            // The header rows sit 1.675 right of the list's centre (local x 6.70 at the list's 0.25 scale).
            float x = (top.x + bottom.x) / 2f + 1.675f;
            float barTop = top.z + Shift + BarAboveTop;
            m_bar.transform.position = new Vector3(x, items.position.y, barTop - BarHeight / 2f - BarAboveOrigin);
        }

        /// <summary>Makes the bar's art <see cref="Grow"/> times taller, kept centred where it was so the
        /// text stays in its middle. Only the screen-vertical axis grows: the width stays the list's.</summary>
        private static void Grow3Slice(Transform slices)
        {
            float before = SliceBounds(slices).center.z;
            var local = slices.InverseTransformDirection(Vector3.forward);
            int axis = Mathf.Abs(local.x) > Mathf.Abs(local.y)
                ? (Mathf.Abs(local.x) > Mathf.Abs(local.z) ? 0 : 2)
                : (Mathf.Abs(local.y) > Mathf.Abs(local.z) ? 1 : 2);
            var scale = slices.localScale;
            scale[axis] *= Grow;
            slices.localScale = scale;
            slices.position += new Vector3(0f, 0f, before - SliceBounds(slices).center.z);
        }

        private static Bounds SliceBounds(Transform slices)
        {
            var renderers = slices.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            return bounds;
        }

        private static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++) SetLayer(t.GetChild(i), layer);
        }

        private void OnEnable()
        {
            m_wantFocus = true;
        }

        private void Update()
        {
            // Focused a frame after the list opens, once the layout has placed the bar.
            if (m_wantFocus && m_frame != null && m_frame.IsStarted)
            {
                m_wantFocus = false;
                Focus();
            }
        }

        private void OnDisable()
        {
            m_wantFocus = false;
            m_text = "";
            FriendFilter.Clear();
            if (m_typing) UniversalInputManager.Get()?.CancelTextInput(m_bar);
            m_typing = false;
            ShowLabel();
        }

        private void Focus()
        {
            if (m_bar == null || m_field == null) return;
            var input = UniversalInputManager.Get();
            if (input == null) return;
            var bounds = m_field.bounds;
            var cam = BaseUI.Get().GetBnetCamera();
            // The text area: the bar's centre piece from the placeholder's left edge, a header-text tall
            // (the input sizes its font from the rect's height).
            float left = m_label.GetComponentInChildren<Renderer>()?.bounds.min.x ?? bounds.min.x + 2f;
            float half = HeaderHeight * 0.32f;
            var topLeft = new Vector3(left, bounds.center.y, bounds.center.z + half);
            var bottomRight = new Vector3(bounds.max.x - CountRoom, bounds.center.y, bounds.center.z - half);
            input.UseTextInput(new UniversalInputManager.TextInputParams
            {
                m_owner = m_bar,
                m_rect = CameraUtils.CreateGUIViewportRect(cam, topLeft, bottomRight),
                m_updatedCallback = OnInputUpdated,
                m_completedCallback = OnInputUpdated,
                m_canceledCallback = OnInputCanceled,
                m_unfocusedCallback = OnInputUnfocused,
                m_font = m_label.GetLocalizedFont(),
                m_text = m_text,
                m_color = TextColor,
                m_alignment = TextAnchor.MiddleLeft,
                m_inputKeepFocusOnComplete = true,
                m_maxCharacters = 64,
            }, force: true);
            m_typing = true;
            ShowLabel();
        }

        private void OnInputUpdated(string text)
        {
            m_text = text ?? "";
            FriendFilter.SetQuery(m_frame, m_text);
            ShowLabel();
        }

        private void OnInputCanceled(bool userRequested, GameObject requester)
        {
            m_typing = false;
            if (userRequested && m_text.Length > 0)
            {
                OnInputUpdated("");
                Focus();
                return;
            }
            // On an empty search the key is the game's again: its release reaches BnetBar's own Esc
            // handling, as with no addon. Closing the list here as well collided with that and ate a click.
            ShowLabel();
        }

        private void OnInputUnfocused()
        {
            m_typing = false;
            ShowLabel();
        }

        /// <summary>The bar's own text shows the query while the input is away, the placeholder while it is empty.</summary>
        private void ShowLabel()
        {
            if (m_label == null) return;
            // Plain text, so a typed "<" stays a letter.
            m_label.RichText = false;
            if (m_text.Length == 0)
            {
                m_label.gameObject.SetActive(true);
                m_label.TextColor = TextColor;
                m_label.Text = GameStrings.Get("GLUE_COLLECTION_SEARCH");
            }
            else
            {
                m_label.gameObject.SetActive(!m_typing);
                m_label.TextColor = TextColor;
                m_label.Text = m_text;
            }
            UpdateCount();
        }

        /// <summary>How many rows match, while a search is on.</summary>
        internal void UpdateCount()
        {
            if (m_count == null) return;
            bool show = FriendFilter.Active && m_frame != null;
            m_count.gameObject.SetActive(show);
            if (show) m_count.Text = FriendFilter.CountMatches(m_frame).ToString();
        }

        [HarmonyPatch(typeof(FriendListFrame), "Awake")]
        private static class AwakePatch
        {
            private static void Postfix(FriendListFrame __instance) => Attach(__instance);
        }

        /// <summary>Every re-sort (a keystroke, or a friend coming online mid-search) refreshes the count.</summary>
        [HarmonyPatch(typeof(FriendListFrame), "SortAndRefreshTouchList")]
        private static class SortPatch
        {
            private static void Postfix(FriendListFrame __instance) => __instance.GetComponent<SearchStrip>()?.UpdateCount();
        }

        [HarmonyPatch(typeof(FriendListFrame), "UpdateItemsList")]
        private static class LayoutPatch
        {
            private static void Postfix(FriendListFrame __instance) => __instance.GetComponent<SearchStrip>()?.Place();
        }
    }
}
