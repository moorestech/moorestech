using System.Collections.Generic;
using Client.Ui.State.Items;
using Client.Ui.State.Recipe.Data;
using Core.Master;
using Game.Block.Interface.MachineUpgrade;
using Game.UnlockState;
using Mooresmaster.Model.MachineRecipesModule;

namespace Client.Ui.State.Recipe.Source
{
    /// <summary>
    /// 解放済みの機械レシピを並べる（Web ホスト MachineRecipesTopic.BuildJson と同じ規則。UITK は橋を参照しない）
    /// Lists unlocked machine recipes (same rule as the Web host MachineRecipesTopic.BuildJson; UITK never references the bridge)
    /// </summary>
    public static class UnlockedMachineRecipeListBuilder
    {
        public static IReadOnlyList<MachineRecipeData> Build(IGameUnlockStateData unlockState, ILevelInheritanceCraftCapLookup craftCapLookup)
        {
            var recipes = new List<MachineRecipeData>();
            var unlockInfos = unlockState.MachineRecipeUnlockStateInfos;
            foreach (var recipe in MasterHolder.MachineRecipesMaster.MachineRecipes.Data)
            {
                // silent-ok: 解放表に無いレシピはロック扱い（ホストと同じ） / 解放されると UnlockedEventPacket で作り直され一覧に出る
                if (!unlockInfos.TryGetValue(recipe.MachineRecipeGuid, out var unlockInfo)) continue;
                // silent-ok: 未解放のレシピは一覧に出さない（ホストと同じ） / 解放されると UnlockedEventPacket で作り直され一覧に出る
                if (!unlockInfo.IsUnlocked) continue;
                recipes.Add(ToData(recipe));
            }
            return recipes;

            #region Internal

            MachineRecipeData ToData(MachineRecipeMasterElement element)
            {
                // マスタの Guid を実行時 ID へ写し、継承はホストと同じ util で読む
                // Map master GUIDs to runtime IDs and read inheritance through the same util as the host
                var inputItems = new List<ItemAmount>();
                foreach (var input in element.InputItems) inputItems.Add(new ItemAmount(MasterHolder.ItemMaster.GetItemId(input.ItemGuid), input.Count));
                var outputItems = new List<ItemAmount>();
                foreach (var output in element.OutputItems) outputItems.Add(new ItemAmount(MasterHolder.ItemMaster.GetItemId(output.ItemGuid), output.Count));
                var inputFluids = new List<RecipeFluidAmount>();
                foreach (var input in element.InputFluids) inputFluids.Add(new RecipeFluidAmount(MasterHolder.FluidMaster.GetFluidId(input.FluidGuid), input.Amount));
                var outputFluids = new List<RecipeFluidAmount>();
                foreach (var output in element.OutputFluids) outputFluids.Add(new RecipeFluidAmount(MasterHolder.FluidMaster.GetFluidId(output.FluidGuid), output.Amount));
                var blockId = MasterHolder.BlockMaster.GetBlockId(element.BlockGuid);
                var inheritance = MachineRecipeLevelInheritanceConversion.Convert(element, craftCapLookup);
                return new MachineRecipeData(element.MachineRecipeGuid, blockId, element.Time, inputItems, outputItems, inputFluids, outputFluids, inheritance);
            }

            #endregion
        }
    }
}
