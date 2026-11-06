using System;
using System.Collections.Generic;
using Core.Master;
using Game.Research;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.State.Research.Logic
{
    /// <summary>
    /// 研究ツリーの判断（Web researchLogic.ts の写し）。state 5 値の意味は Traits の 1 表に畳む
    /// Research tree decisions (port of Web researchLogic.ts); the meaning of the five states is folded into the single Traits table
    /// </summary>
    public static class ResearchLogic
    {
        // サーバーと同じ 2 パス: 単一受付 → 複数受付を、それぞれ宣言順に割り当てる
        // Same two passes as the server: single acceptance, then multiple acceptance, each in declaration order
        public static int[] AllocateResearchItems(IReadOnlyList<ResearchConsumeItem> consumeItems, IReadOnlyDictionary<ItemId, int> owned)
        {
            var remaining = new Dictionary<ItemId, int>();
            foreach (var pair in owned) remaining[pair.Key] = pair.Value;
            var allocated = new int[consumeItems.Count];
            AllocatePass(true);
            AllocatePass(false);
            return allocated;

            #region Internal

            void AllocatePass(bool singleAcceptance)
            {
                for (var index = 0; index < consumeItems.Count; index++)
                {
                    var item = consumeItems[index];
                    if ((item.AcceptedItemIds.Count == 1) != singleAcceptance) continue;
                    foreach (var id in item.AcceptedItemIds)
                    {
                        var have = remaining.TryGetValue(id, out var count) ? count : 0;
                        var amount = Math.Min(have, item.Count - allocated[index]);
                        allocated[index] += amount;
                        remaining[id] = have - amount;
                    }
                }
            }

            #endregion
        }

        // 1 件ごとの不足強調は表示専用。完了と所持数未受信（null）では出さない
        // Per-item shortage is display-only; hidden when completed or while owned counts are unknown (null)
        public static bool IsConsumeItemLacking(ResearchNodeState state, int required, int? allocated)
        {
            return allocated.HasValue && state != ResearchNodeState.Completed && allocated.Value < required;
        }

        // ボタン活性の正本はサーバーの state だけ。所持数は表示にしか使わない
        // Server state is the sole authority for availability; owned counts are display-only
        public static ResearchButtonState DeriveResearchButton(ResearchNodeState state)
        {
            if (state == ResearchNodeState.Completed) return new ResearchButtonState(true, false, LocalizationKeys.Ui.Research.Completed);
            Traits(state, out var preNodeMet, out var itemsSufficient);
            var tooltipKey = preNodeMet
                ? itemsSufficient ? LocalizationKeys.Ui.Research.ClickToResearch : LocalizationKeys.Ui.Research.MissingItems
                : itemsSufficient ? LocalizationKeys.Ui.Research.MissingPrerequisites : LocalizationKeys.Ui.Research.MissingItemsAndPrerequisites;
            return new ResearchButtonState(false, preNodeMet && itemsSufficient, tooltipKey);
        }

        public static NodeCardState DeriveNodeCardState(ResearchNodeState state)
        {
            var completed = state == ResearchNodeState.Completed;
            Traits(state, out var preNodeMet, out _);
            return new NodeCardState(completed, !completed && DeriveResearchButton(state).Interactable, !completed && !preNodeMet);
        }

        // 状態ラベルは枠色と同じ導出から引く。不可の理由は詳細ペインが担うのでカードでは 3 語へ畳む（ADR 0044）
        // The label reads the same derivation as the border; the detail pane explains "unavailable", so cards fold to three words (ADR 0044)
        public static LocalizationKey DeriveNodeStateLabelKey(NodeCardState cardState)
        {
            if (cardState.Completed) return LocalizationKeys.Ui.Research.Completed;
            return cardState.Ready ? LocalizationKeys.Ui.Research.StateAvailable : LocalizationKeys.Ui.Research.StateUnavailable;
        }

        // 初期フォーカス: 研究可能を優先し、無ければ素材待ちの最前線。どちらも無ければ選ばない
        // Initial focus: researchable first, else the item-lacking frontier; none when neither exists
        public static bool TryFindInitialFocusNode(IReadOnlyList<ResearchNodeSummary> nodes, out ResearchNodeSummary node)
        {
            if (TryFindFirst(nodes, ResearchNodeState.Researchable, out node)) return true;
            return TryFindFirst(nodes, ResearchNodeState.UnresearchableNotEnoughItem, out node);
        }

        private static bool TryFindFirst(IReadOnlyList<ResearchNodeSummary> nodes, ResearchNodeState state, out ResearchNodeSummary found)
        {
            foreach (var candidate in nodes)
            {
                if (candidate.State != state) continue;
                found = candidate;
                return true;
            }
            found = default;
            return false;
        }

        // state 5 値の意味の表。state が増えたらここで例外になる
        // The table of what the five states mean; a new state throws right here
        private static void Traits(ResearchNodeState state, out bool preNodeMet, out bool itemsSufficient)
        {
            (preNodeMet, itemsSufficient) = state switch
            {
                ResearchNodeState.Completed => (true, true),
                ResearchNodeState.Researchable => (true, true),
                ResearchNodeState.UnresearchableNotEnoughItem => (true, false),
                ResearchNodeState.UnresearchableNotEnoughPreNode => (false, true),
                ResearchNodeState.UnresearchableAllReasons => (false, false),
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
            };
        }
    }
}
