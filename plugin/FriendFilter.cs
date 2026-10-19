using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace HsFriendSearch
{
    /// <summary>
    /// Applies the search to the game's own virtualized list. While a query is set, every section is
    /// filtered and the list is flat: headers hide, direct hits come first, typo hits after them, and
    /// equal ranks keep the game's order (ItemsSortCompare). A query change scores each row once into
    /// <see cref="s_ranks"/>; IsItemShowable and the sort then only look ranks up. A row the cache has
    /// not seen yet (a friend added mid-search) is scored on first sight.
    /// </summary>
    internal static class FriendFilter
    {
        private static readonly AccessTools.FieldRef<FriendListFrame, List<FriendListFrame.FriendListItem>> AllItems =
            AccessTools.FieldRefAccess<FriendListFrame, List<FriendListFrame.FriendListItem>>("m_allItems");

        private static readonly Action<FriendListFrame> SortAndRefresh =
            AccessTools.MethodDelegate<Action<FriendListFrame>>(AccessTools.Method(typeof(FriendListFrame), "SortAndRefreshTouchList"));

        private static string s_query = "";
        private static readonly Dictionary<object, int> s_ranks = new Dictionary<object, int>();

        public static bool Active => s_query.Length > 0;

        /// <summary>Sets the query and re-sorts the list; an unchanged query does nothing.</summary>
        public static void SetQuery(FriendListFrame frame, string text)
        {
            string query = FriendMatch.Normalize(text);
            if (query == s_query) return;
            s_query = query;
            s_ranks.Clear();
            if (frame == null || !frame.isActiveAndEnabled) return;
            SortAndRefresh(frame);
            frame.items.ScrollValue = 0f;
        }

        /// <summary>Drops the query without touching the list: the frame's own OnEnable re-sorts it.</summary>
        public static void Clear()
        {
            s_query = "";
            s_ranks.Clear();
        }

        /// <summary>The rows the query matches; ranks come from the cache the filter fills anyway.</summary>
        public static int CountMatches(FriendListFrame frame)
        {
            int count = 0;
            foreach (var item in AllItems(frame))
                if (!HiddenByEditMode(frame, item) && Rank(item) > FriendMatch.None) count++;
            return count;
        }

        /// <summary>The game's remove-friends mode hides recent and nearby players; the search keeps that.</summary>
        private static bool HiddenByEditMode(FriendListFrame frame, FriendListFrame.FriendListItem item) =>
            frame.EditMode == FriendListFrame.FriendListEditMode.REMOVE_FRIENDS
            && (item.SubType == MobileFriendListItem.TypeFlags.RecentPlayer
                || item.SubType == MobileFriendListItem.TypeFlags.NearbyPlayer);

        private static object Data(FriendListFrame.FriendListItem item) =>
            (object)item.GetFriend() ?? (object)item.GetRecentPlayer() ?? (object)item.GetNearbyPlayer() ?? item.GetInvite();

        private static int Rank(FriendListFrame.FriendListItem item)
        {
            if (item.IsHeader) return FriendMatch.None;
            object data = Data(item);
            if (data == null) return FriendMatch.None;
            if (s_ranks.TryGetValue(data, out int rank)) return rank;
            rank = data is BnetPlayer player ? ScorePlayer(player)
                : data is BnetInvitation invite ? FriendMatch.Score(s_query, new[] { invite.GetTargetName() }, Array.Empty<string>())
                : FriendMatch.None;
            s_ranks[data] = rank;
            return rank;
        }

        /// <summary>The row's shown name (Real ID name or BattleTag name) and the BattleTag name, typos allowed;
        /// the full "Name#1234" directly only.</summary>
        private static int ScorePlayer(BnetPlayer player)
        {
            var tag = player.GetBattleTag();
            string[] names = { player.GetBestName(), player.GetFullName(), tag?.GetName() };
            string[] direct = { tag?.ToString() };
            return FriendMatch.Score(s_query, names, direct);
        }

        [HarmonyPatch]
        private static class ShowablePatch
        {
            private static readonly Type Behavior = AccessTools.Inner(typeof(FriendListFrame), "VirtualizedFriendsListBehavior");

            private static readonly FieldInfo FriendList = AccessTools.Field(Behavior, "m_friendList");

            // One behavior per frame; remembered so the per-row call skips the reflection read.
            private static object s_behavior;
            private static FriendListFrame s_frame;

            private static MethodBase TargetMethod() => AccessTools.Method(Behavior, "IsItemShowable");

            private static void Postfix(object __instance, int allItemsIndex, ref bool __result)
            {
                if (!Active) return;
                if (!ReferenceEquals(__instance, s_behavior) || s_frame == null)
                {
                    s_behavior = __instance;
                    s_frame = (FriendListFrame)FriendList.GetValue(__instance);
                }
                var frame = s_frame;
                var all = AllItems(frame);
                if (allItemsIndex < 0 || allItemsIndex >= all.Count) return;
                var item = all[allItemsIndex];
                __result = !HiddenByEditMode(frame, item) && Rank(item) > FriendMatch.None;
            }
        }

        [HarmonyPatch(typeof(FriendListFrame), "ItemsSortCompare")]
        private static class SortPatch
        {
            private static bool Prefix(FriendListFrame.FriendListItem item1, FriendListFrame.FriendListItem item2, ref int __result)
            {
                if (!Active) return true;
                int rank1 = Rank(item1), rank2 = Rank(item2);
                if (rank1 == rank2) return true;
                __result = rank2.CompareTo(rank1);
                return false;
            }
        }
    }
}
