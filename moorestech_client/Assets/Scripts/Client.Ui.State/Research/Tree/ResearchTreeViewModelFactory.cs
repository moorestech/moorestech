using System;
using System.Collections.Generic;
using Client.Ui.State.Research.Catalog;
using Client.Ui.State.Research.Logic;
using Core.Master;
using Game.Research;

namespace Client.Ui.State.Research.Tree
{
    /// <summary>
    /// 定義・サーバーの状態・所持数・選択から研究ツリーの表示を組む純関数
    /// Pure function building the research tree display from definitions, server states, owned counts and selection
    /// </summary>
    public static class ResearchTreeViewModelFactory
    {
        public static ResearchTreeViewModel Create(IReadOnlyList<ResearchNodeDefinition> definitions, IReadOnlyDictionary<Guid, ResearchNodeState> states, IReadOnlyDictionary<ItemId, int> owned, ResearchSelection selection)
        {
            var cards = new List<ResearchNodeCardViewModel>(definitions.Count);
            var summaries = new List<ResearchNodeSummary>(definitions.Count);
            var hasDetail = false;
            ResearchDetailViewModel detail = null;
            foreach (var definition in definitions) AddNode(definition);

            // 注目点は研究可能を優先し、無ければ素材待ちの最前線
            // Focus prefers researchable, else the item-waiting frontier
            var focus = ResearchLogic.TryFindInitialFocusNode(summaries, out var focusNode) ? ResearchFocusPoint.At(PositionOf(focusNode.Guid)) : ResearchFocusPoint.None;
            return hasDetail ? ResearchTreeViewModel.CreateWithDetail(cards, focus, detail) : ResearchTreeViewModel.Create(cards, focus);

            #region Internal

            void AddNode(ResearchNodeDefinition definition)
            {
                var state = StateOf(definition.Guid);
                var cardState = ResearchLogic.DeriveNodeCardState(state);
                var selected = selection.Is(definition.Guid);
                cards.Add(new ResearchNodeCardViewModel(definition.Guid, definition.IconItemId, definition.TreePosition, definition.PreviousGuids, cardState, ResearchLogic.DeriveNodeStateLabelKey(cardState), selected));
                summaries.Add(new ResearchNodeSummary(definition.Guid, state));
                if (!selected) return; // silent-ok: 選択していないノードは詳細を組まない / 選択は ResearchSelectionStore が持ち L1 が詳細の有無を見る
                hasDetail = true;
                detail = BuildDetail(definition, state);
            }

            // サーバーの状態が無いノードは全条件未達（Web の橋と同じ既定）
            // Nodes without server state are all-reasons-unmet (same default as the Web bridge)
            ResearchNodeState StateOf(Guid guid)
            {
                // silent-ok: 取り直しの前は全ノードが未着で Web の橋と同じ既定 / 受信後の欠落は ResearchTreeModel が受信ごとに件数を LogWarning へ出す
                return states.TryGetValue(guid, out var state) ? state : ResearchNodeState.UnresearchableAllReasons;
            }

            ResearchDetailViewModel BuildDetail(ResearchNodeDefinition definition, ResearchNodeState state)
            {
                // 割当はサーバーと同じ 2 パス。不足の強調は完了で出さない
                // Allocation follows the server's two passes; completed nodes never show shortage
                var allocated = ResearchLogic.AllocateResearchItems(definition.ConsumeItems, owned);
                var slots = new List<ResearchConsumeSlotViewModel>(definition.ConsumeItems.Count);
                for (var i = 0; i < definition.ConsumeItems.Count; i++)
                {
                    var consume = definition.ConsumeItems[i];
                    slots.Add(new ResearchConsumeSlotViewModel(consume.ItemId, consume.Count, allocated[i], ResearchLogic.IsConsumeItemLacking(state, consume.Count, allocated[i])));
                }
                return new ResearchDetailViewModel(definition.Guid, ResearchLogic.DeriveResearchButton(state), slots, UnlockEntries.ToUnlockEntries(definition.Unlocks));
            }

            UnityEngine.Vector2 PositionOf(Guid guid)
            {
                foreach (var definition in definitions)
                {
                    if (definition.Guid == guid) return definition.TreePosition;
                }
                throw new InvalidOperationException($"注目点のノード {guid} が定義に無い");
            }

            #endregion
        }
    }
}
