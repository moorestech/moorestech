using System;
using System.Collections.Generic;
using System.Linq;
using Client.Ui.State.Research.Logic;
using Core.Master;
using Game.Block.Interface.MachineUpgrade;
using Mooresmaster.Model.GameActionModule;
using Mooresmaster.Model.ResearchModule;

namespace Client.Ui.State.Research.Catalog
{
    /// <summary>
    /// 完了アクションから解放物8種を抜く
    /// Extracts eight unlock kinds from actions
    /// </summary>
    public static class ResearchUnlockSetFromMaster
    {
        // Append の switch が case を持つ表示 8 種。網羅テストが全 GameActionType との突き合わせに使う
        // The eight shown kinds that Append's switch has cases for; the coverage test reconciles them with every GameActionType
        internal static readonly IReadOnlyCollection<string> DisplayedGameActionTypes = new HashSet<string>
        {
            GameActionElement.GameActionTypeConst.giveItem,
            GameActionElement.GameActionTypeConst.unlockItemRecipeView,
            GameActionElement.GameActionTypeConst.unlockBlock,
            GameActionElement.GameActionTypeConst.unlockMachineRecipe,
            GameActionElement.GameActionTypeConst.unlockConnectTool,
            GameActionElement.GameActionTypeConst.unlockTrainCar,
            GameActionElement.GameActionTypeConst.unlockModuleSlotStackLevel,
            GameActionElement.GameActionTypeConst.unlockLevelInheritanceCraftLevel,
        };

        // 解放はされるが研究画面に出さない種別（Web の ResearchUnlockGameActionCoverageTest の非表示台帳と同じ 7 種）
        // Kinds unlocked but not shown on the research screen (same seven as the Web ResearchUnlockGameActionCoverageTest hidden ledger)
        internal static readonly IReadOnlyCollection<string> HiddenGameActionTypes = new HashSet<string>
        {
            GameActionElement.GameActionTypeConst.unlockCraftRecipe,
            GameActionElement.GameActionTypeConst.unlockChallengeCategory,
            GameActionElement.GameActionTypeConst.unlockItemStackLevel,
            GameActionElement.GameActionTypeConst.unlockPlayerInventorySlotLevel,
            GameActionElement.GameActionTypeConst.unlockBlueprint,
            GameActionElement.GameActionTypeConst.playSkit,
            GameActionElement.GameActionTypeConst.playBackgroundSkit,
        };

        public static ResearchUnlockSet Build(ResearchNodeMasterElement master)
        {
            var blocks = new List<UnlockBlock>();
            var machineRecipeUnlocks = new List<UnlockMachineRecipe>();
            var itemRecipeViews = new List<ItemId>();
            var rewards = new List<RewardItem>();
            var connectTools = new List<Guid>();
            var trainCars = new List<Guid>();
            var moduleSlotLimits = new List<int>();
            var craftCaps = new List<LevelInheritanceCraftCap>();
            foreach (var action in master.ClearedActions.items) Append(action);
            return new ResearchUnlockSet(blocks, machineRecipeUnlocks, itemRecipeViews, rewards, connectTools, trainCars, moduleSlotLimits, craftCaps);

            #region Internal

            void Append(GameActionElement action)
            {
                switch (action.GameActionParam)
                {
                    case GiveItemGameActionParam give:
                        foreach (var reward in give.RewardItems) rewards.Add(new RewardItem(MasterHolder.ItemMaster.GetItemId(reward.ItemGuid), reward.ItemCount));
                        break;
                    case UnlockItemRecipeViewGameActionParam view:
                        foreach (var itemGuid in view.UnlockItemGuids) itemRecipeViews.Add(MasterHolder.ItemMaster.GetItemId(itemGuid));
                        break;
                    case UnlockBlockGameActionParam block:
                        foreach (var blockGuid in block.UnlockBlockGuids) blocks.Add(new UnlockBlock(MasterHolder.BlockMaster.GetBlockId(blockGuid), blockGuid));
                        break;
                    case UnlockMachineRecipeGameActionParam recipe:
                        foreach (var recipeGuid in recipe.UnlockMachineRecipeGuids) AppendMachineRecipe(recipeGuid);
                        break;
                    case UnlockConnectToolGameActionParam tool:
                        connectTools.AddRange(tool.UnlockConnectToolGuids);
                        break;
                    case UnlockTrainCarGameActionParam car:
                        trainCars.AddRange(car.UnlockTrainCarGuids);
                        break;
                    case UnlockModuleSlotStackLevelGameActionParam moduleSlot:
                        // 上限値で運ぶ
                        // Carry the limit value
                        moduleSlotLimits.Add(ModuleSlotStackLimitMasterUtil.GetStackLimit(moduleSlot.Level));
                        break;
                    case UnlockLevelInheritanceCraftLevelGameActionParam craftCap:
                        foreach (var itemGuid in craftCap.TargetItemGuids) craftCaps.Add(new LevelInheritanceCraftCap(MasterHolder.ItemMaster.GetItemId(itemGuid), craftCap.Level));
                        break;
                    default:
                        // 出さないと決めた 7 種だけ通し、それ以外は表示の裁定が要るので止める（.decisions 2026-08-19「研究の未対応GameActionTypeは例外で落とす」）
                        // Only the seven decided-hidden kinds pass; anything else needs a show-or-hide decision, so stop (.decisions 2026-08-19)
                        if (!HiddenGameActionTypes.Contains(action.GameActionType)) throw new InvalidOperationException($"[ResearchUnlockSetFromMaster] 研究 {master.ResearchNodeGuid} の未対応の GameActionType {action.GameActionType}");
                        break;
                }
            }

            // 機械レシピはレシピ単位でアイテムと液体の両方を運ぶ。未解放のレシピも引くのでマスタから読む（D-13）
            // Each machine recipe carries both item and fluid outputs; locked recipes are needed too, so read the master (D-13)
            void AppendMachineRecipe(Guid recipeGuid)
            {
                // 参照切れは ResearchMasterUtil の検証で MasterHolder.Load が止めるので、ここでは null を見ない
                // Dangling GUIDs are rejected by ResearchMasterUtil validation in MasterHolder.Load, so no null check here
                var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(recipeGuid);
                var items = new List<ItemId>(recipe.OutputItems.Length);
                foreach (var output in recipe.OutputItems) items.Add(MasterHolder.ItemMaster.GetItemId(output.ItemGuid));
                var fluids = new List<UnlockFluid>(recipe.OutputFluids.Length);
                foreach (var output in recipe.OutputFluids) fluids.Add(new UnlockFluid(MasterHolder.FluidMaster.GetFluidId(output.FluidGuid), output.Amount));
                machineRecipeUnlocks.Add(new UnlockMachineRecipe(recipeGuid, items, fluids));
            }

            #endregion
        }
    }
}
