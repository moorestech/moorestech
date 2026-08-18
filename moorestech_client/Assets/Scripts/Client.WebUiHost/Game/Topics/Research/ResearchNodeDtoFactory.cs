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
                State = ToStateString(state),
                IconItemId = MasterHolder.ItemMaster.GetItemId(master.GraphViewSettings.IconItem).AsPrimitive(),
                Position = new ResearchPositionDto { X = master.GraphViewSettings.UIPosition.x, Y = master.GraphViewSettings.UIPosition.y },
                PrevGuids = new List<string>(),
                ConsumeItems = new List<ResearchConsumeItemDto>(),
                RewardItems = new List<ResearchRewardItemDto>(),
                UnlockItemIds = new List<int>(),
                UnlockBlocks = new List<ResearchUnlockBlockDto>(),
                UnlockMachineRecipeOutputItemIds = new List<int>(),
                UnlockConnectToolGuids = new List<string>(),
                UnlockTrainCarGuids = new List<string>(),
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
                else if (action.GameActionType == GameActionElement.GameActionTypeConst.unlockBlock)
                {
                    var unlock = (UnlockBlockGameActionParam)action.GameActionParam;
                    foreach (var blockGuid in unlock.UnlockBlockGuids)
                        dto.UnlockBlocks.Add(new ResearchUnlockBlockDto { BlockId = MasterHolder.BlockMaster.GetBlockId(blockGuid).AsPrimitive(), BlockGuid = blockGuid.ToString() });
                }
                else if (action.GameActionType == GameActionElement.GameActionTypeConst.unlockMachineRecipe)
                {
                    // 機械レシピは出力アイテムのアイコンで代表させる（§8.7の代表出力アイテム前例）
                    // Represent machine recipes by their output item icons (per the §8.7 precedent)
                    var unlock = (UnlockMachineRecipeGameActionParam)action.GameActionParam;
                    foreach (var recipeGuid in unlock.UnlockMachineRecipeGuids)
                    foreach (var output in MasterHolder.MachineRecipesMaster.GetRecipeElement(recipeGuid).OutputItems)
                        dto.UnlockMachineRecipeOutputItemIds.Add(MasterHolder.ItemMaster.GetItemId(output.ItemGuid).AsPrimitive());
                }
                else if (action.GameActionType == GameActionElement.GameActionTypeConst.unlockConnectTool)
                {
                    var unlock = (UnlockConnectToolGameActionParam)action.GameActionParam;
                    foreach (var toolGuid in unlock.UnlockConnectToolGuids) dto.UnlockConnectToolGuids.Add(toolGuid.ToString());
                }
                else if (action.GameActionType == GameActionElement.GameActionTypeConst.unlockTrainCar)
                {
                    var unlock = (UnlockTrainCarGameActionParam)action.GameActionParam;
                    foreach (var carGuid in unlock.UnlockTrainCarGuids) dto.UnlockTrainCarGuids.Add(carGuid.ToString());
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
