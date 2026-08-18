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
                UnlockItemRecipeViewItemIds = new List<int>(),
                UnlockBlocks = new List<ResearchUnlockBlockDto>(),
                UnlockMachineRecipes = new List<ResearchUnlockMachineRecipeDto>(),
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

            // 報酬/解放は ClearedActions から抽出（uGUI ResearchTreeElement と同じ分岐）
            // Rewards/unlocks come from ClearedActions (same branching as uGUI ResearchTreeElement)
            AppendActionItems();
            return dto;

            #region Internal

            void AppendActionItems()
            {
                // 報酬(giveItem)と各種解放(アイテム/ブロック/機械レシピ/接続ツール/列車車両)を抽出する
                // Extract the reward (giveItem) and every unlock-kind action (item/block/machine recipe/connect tool/train car)
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
                            dto.UnlockItemRecipeViewItemIds.Add(MasterHolder.ItemMaster.GetItemId(itemGuid).AsPrimitive());
                    }
                    else if (action.GameActionType == GameActionElement.GameActionTypeConst.unlockBlock)
                    {
                        var unlock = (UnlockBlockGameActionParam)action.GameActionParam;
                        foreach (var blockGuid in unlock.UnlockBlockGuids)
                            dto.UnlockBlocks.Add(new ResearchUnlockBlockDto { BlockId = MasterHolder.BlockMaster.GetBlockId(blockGuid).AsPrimitive(), BlockGuid = blockGuid.ToString() });
                    }
                    else if (action.GameActionType == GameActionElement.GameActionTypeConst.unlockMachineRecipe)
                    {
                        var unlock = (UnlockMachineRecipeGameActionParam)action.GameActionParam;
                        foreach (var recipeGuid in unlock.UnlockMachineRecipeGuids)
                            dto.UnlockMachineRecipes.Add(BuildUnlockMachineRecipeDto(recipeGuid));
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

            // 機械レシピをレシピ単位で運ぶ（液体のみ出力の消失を防ぐ）
            // Carry each machine recipe individually (avoids losing fluid-only outputs)
            ResearchUnlockMachineRecipeDto BuildUnlockMachineRecipeDto(Guid recipeGuid)
            {
                var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(recipeGuid);
                var outputItemIds = new List<int>();
                foreach (var output in recipe.OutputItems)
                    outputItemIds.Add(MasterHolder.ItemMaster.GetItemId(output.ItemGuid).AsPrimitive());
                var outputFluids = new List<ResearchUnlockFluidDto>();
                foreach (var output in recipe.OutputFluids)
                    outputFluids.Add(new ResearchUnlockFluidDto
                    {
                        FluidId = MasterHolder.FluidMaster.GetFluidId(output.FluidGuid).AsPrimitive(),
                        FluidGuid = output.FluidGuid.ToString(),
                        Amount = output.Amount,
                    });
                return new ResearchUnlockMachineRecipeDto { RecipeGuid = recipeGuid.ToString(), OutputItemIds = outputItemIds, OutputFluids = outputFluids };
            }

            string ToStateString(ResearchNodeState s)
            {
                return s switch
                {
                    ResearchNodeState.Completed => "completed",
                    ResearchNodeState.Researchable => "researchable",
                    ResearchNodeState.UnresearchableNotEnoughItem => "unresearchableNotEnoughItem",
                    ResearchNodeState.UnresearchableNotEnoughPreNode => "unresearchableNotEnoughPreNode",
                    _ => "unresearchableAllReasons",
                };
            }

            #endregion
        }
    }
}
