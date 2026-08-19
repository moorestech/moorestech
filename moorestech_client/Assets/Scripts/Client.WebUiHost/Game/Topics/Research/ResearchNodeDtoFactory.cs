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

            // 報酬と解放はClearedActionsで抽出
            // Rewards and unlocks come from ClearedActions
            AppendRewardsAndUnlocks();
            return dto;

            #region Internal

            void AppendRewardsAndUnlocks()
            {
                // 報酬と表示対象6種を抽出し、表示しない種別も明示して未知の種別だけを弾く
                // Extract the reward and the 6 displayed kinds, naming the non-displayed ones so only unknown kinds fall through
                foreach (var action in master.ClearedActions.items)
                {
                    switch (action.GameActionType)
                    {
                        case GameActionElement.GameActionTypeConst.giveItem:
                        {
                            var give = (GiveItemGameActionParam)action.GameActionParam;
                            foreach (var reward in give.RewardItems)
                                dto.RewardItems.Add(new ResearchRewardItemDto { ItemId = MasterHolder.ItemMaster.GetItemId(reward.ItemGuid).AsPrimitive(), Count = reward.ItemCount });
                            break;
                        }
                        case GameActionElement.GameActionTypeConst.unlockItemRecipeView:
                        {
                            var unlock = (UnlockItemRecipeViewGameActionParam)action.GameActionParam;
                            foreach (var itemGuid in unlock.UnlockItemGuids)
                                dto.UnlockItemRecipeViewItemIds.Add(MasterHolder.ItemMaster.GetItemId(itemGuid).AsPrimitive());
                            break;
                        }
                        case GameActionElement.GameActionTypeConst.unlockBlock:
                        {
                            var unlock = (UnlockBlockGameActionParam)action.GameActionParam;
                            foreach (var blockGuid in unlock.UnlockBlockGuids)
                                dto.UnlockBlocks.Add(new ResearchUnlockBlockDto { BlockId = MasterHolder.BlockMaster.GetBlockId(blockGuid).AsPrimitive(), BlockGuid = blockGuid.ToString() });
                            break;
                        }
                        case GameActionElement.GameActionTypeConst.unlockMachineRecipe:
                        {
                            var unlock = (UnlockMachineRecipeGameActionParam)action.GameActionParam;
                            foreach (var recipeGuid in unlock.UnlockMachineRecipeGuids)
                                dto.UnlockMachineRecipes.Add(BuildUnlockMachineRecipeDto(recipeGuid));
                            break;
                        }
                        case GameActionElement.GameActionTypeConst.unlockConnectTool:
                        {
                            var unlock = (UnlockConnectToolGameActionParam)action.GameActionParam;
                            foreach (var toolGuid in unlock.UnlockConnectToolGuids) dto.UnlockConnectToolGuids.Add(toolGuid.ToString());
                            break;
                        }
                        case GameActionElement.GameActionTypeConst.unlockTrainCar:
                        {
                            var unlock = (UnlockTrainCarGameActionParam)action.GameActionParam;
                            foreach (var carGuid in unlock.UnlockTrainCarGuids) dto.UnlockTrainCarGuids.Add(carGuid.ToString());
                            break;
                        }
                        // 解放されるが研究画面には出さないと決めた種別（ADR 0014 決定3の表示集合6種の外）
                        // Kinds that are unlocked but deliberately not shown on the research screen (outside ADR 0014 decision 3's set of 6)
                        case GameActionElement.GameActionTypeConst.unlockCraftRecipe:
                        case GameActionElement.GameActionTypeConst.unlockChallengeCategory:
                        case GameActionElement.GameActionTypeConst.unlockItemStackLevel:
                        case GameActionElement.GameActionTypeConst.unlockPlayerInventorySlotLevel:
                        case GameActionElement.GameActionTypeConst.playSkit:
                        case GameActionElement.GameActionTypeConst.playBackgroundSkit:
                            break;
                        // 種別が増えた日に無言で解放物を落とさず、ここで気づかせる
                        // A newly added kind must not silently drop its unlocks; fail here instead
                        default:
                            throw new InvalidOperationException($"研究の解放物として未対応のGameActionTypeです: {action.GameActionType}");
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
                    ResearchNodeState.UnresearchableAllReasons => "unresearchableAllReasons",
                    _ => throw new InvalidOperationException($"未対応のResearchNodeStateです: {s}"),
                };
            }

            #endregion
        }
    }
}
