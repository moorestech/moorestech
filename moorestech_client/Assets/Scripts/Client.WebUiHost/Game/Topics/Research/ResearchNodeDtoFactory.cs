using System;
using System.Collections.Generic;
using Core.Master;
using Game.Research;
using Mooresmaster.Model.GameActionModule;
using Mooresmaster.Model.ResearchModule;

namespace Client.WebUiHost.Game.Topics
{
    /// <summary>
    /// 研究マスタ + サーバー状態 → ResearchNodeDto の変換（uGUI ResearchTreeElement の解析を移植）
    /// Converts research master + server state into ResearchNodeDto (ported from uGUI ResearchTreeElement parsing)
    /// </summary>
    public static class ResearchNodeDtoFactory
    {
        public static ResearchNodeDto Create(ResearchNodeMasterElement master, Dictionary<Guid, ResearchNodeState> states)
        {
            // サーバー状態が無いノードは全条件未達扱い（uGUI GetValueOrDefault と同じ既定）
            // Nodes without server state default to all-reasons-unmet (same default as uGUI GetValueOrDefault)
            var state = states.GetValueOrDefault(master.ResearchNodeGuid, ResearchNodeState.UnresearchableAllReasons);
            var dto = new ResearchNodeDto
            {
                Guid = master.ResearchNodeGuid.ToString(),
                Name = master.ResearchNodeName,
                Description = master.ResearchNodeDescription,
                State = ToStateString(state),
                Position = new ResearchPositionDto { X = master.GraphViewSettings.UIPosition.x, Y = master.GraphViewSettings.UIPosition.y },
                PrevGuids = new List<string>(),
                ConsumeItems = new List<ResearchConsumeItemDto>(),
                RewardItems = new List<ResearchRewardItemDto>(),
                UnlockItemIds = new List<int>(),
            };

            foreach (var prev in master.PrevResearchNodeGuids) dto.PrevGuids.Add(prev.ToString());

            // 消費アイテム（GuidをItemIdへ変換）
            // Consume items (convert Guid to ItemId)
            foreach (var consume in master.ConsumeItems)
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(consume.ItemGuid);
                dto.ConsumeItems.Add(new ResearchConsumeItemDto { ItemId = itemId.AsPrimitive(), Count = consume.ItemCount });
            }

            // 報酬/解放アイテムは ClearedActions から抽出（uGUI ResearchTreeElement と同じ分岐）
            // Rewards/unlocks come from ClearedActions (same branching as uGUI ResearchTreeElement)
            AppendActionItems(dto, master);
            return dto;
        }

        private static void AppendActionItems(ResearchNodeDto dto, ResearchNodeMasterElement master)
        {
            // ClearedActions から報酬(giveItem)と解放(unlockItemRecipeView)のアイテムを抽出する
            // Extract reward (giveItem) and unlock (unlockItemRecipeView) items from ClearedActions
            foreach (var action in master.ClearedActions.items)
            {
                if (action.GameActionType == GameActionElement.GameActionTypeConst.giveItem)
                {
                    var give = (GiveItemGameActionParam)action.GameActionParam;
                    foreach (var reward in give.RewardItems)
                        dto.RewardItems.Add(new ResearchRewardItemDto { ItemId = MasterHolder.ItemMaster.GetItemId(reward.ItemGuid).AsPrimitive(), Count = reward.ItemCount });
                }
                else if (action.GameActionType == GameActionElement.GameActionTypeConst.unlockItemRecipeView)
                {
                    var unlock = (UnlockItemRecipeViewGameActionParam)action.GameActionParam;
                    foreach (var itemGuid in unlock.UnlockItemGuids)
                        dto.UnlockItemIds.Add(MasterHolder.ItemMaster.GetItemId(itemGuid).AsPrimitive());
                }
            }
        }

        private static string ToStateString(ResearchNodeState state)
        {
            return state switch
            {
                ResearchNodeState.Completed => "completed",
                ResearchNodeState.Researchable => "researchable",
                ResearchNodeState.UnresearchableNotEnoughItem => "unresearchableNotEnoughItem",
                ResearchNodeState.UnresearchableNotEnoughPreNode => "unresearchableNotEnoughPreNode",
                _ => "unresearchableAllReasons",
            };
        }
    }
}
