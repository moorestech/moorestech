using System;
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Logic.Machine;
using Core.Master;
using Game.Block.Blocks.Machine.RecipeSelection;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.MachineRecipesModule;

namespace Client.Ui.State.BlockInventory.Machine.Detail
{
    /// <summary>
    /// 選択レシピの束縛を組む
    /// Builds the selected recipe's binding, mirroring the server rule like the Web host
    /// </summary>
    public static class MachineSlotBindingConversion
    {
        public static MachineBindingDetail Build(MachineRecipeMasterElement selectedRecipe, IMachineParam param)
        {
            var layout = new MachineSlotLayout(param.InputSlotCount, param.OutputSlotCount, param.ModuleSlotCount);
            var slots = new List<MachineItemSlotBinding>();
            var tanks = new List<MachineTankBinding>();
            // silent-ok: 未選択の機械は束縛を持たない（Web ホストと同じ） / ゴースト（空の束縛）が出ないことを MachineDetailConversionTest が見る
            if (selectedRecipe == null) return new MachineBindingDetail(layout, slots, tanks);

            // 入力はサーバーと同じ util 経由
            // Inputs take accepted IDs from the server's util; outputs start at InputSlotCount
            for (var i = 0; i < selectedRecipe.InputItems.Length; i++) slots.Add(ItemBinding(i, selectedRecipe.InputItems[i].ItemGuid, selectedRecipe.InputItems[i].Count, MachineRecipeSlotBindingUtil.GetAcceptedInputItemIds(selectedRecipe, i)));
            for (var j = 0; j < selectedRecipe.OutputItems.Length; j++) slots.Add(ItemBinding(param.InputSlotCount + j, selectedRecipe.OutputItems[j].ItemGuid, selectedRecipe.OutputItems[j].Count, MachineRecipeSlotBindingUtil.GetAcceptedOutputItemIds(selectedRecipe, j)));

            // タンクは入力 i、出力は入力数から
            // Tanks: input i, outputs from InputTankCount
            for (var i = 0; i < selectedRecipe.InputFluids.Length; i++) tanks.Add(new MachineTankBinding(i, MasterHolder.FluidMaster.GetFluidId(selectedRecipe.InputFluids[i].FluidGuid), selectedRecipe.InputFluids[i].Amount));
            for (var j = 0; j < selectedRecipe.OutputFluids.Length; j++) tanks.Add(new MachineTankBinding(param.InputTankCount + j, MasterHolder.FluidMaster.GetFluidId(selectedRecipe.OutputFluids[j].FluidGuid), selectedRecipe.OutputFluids[j].Amount));
            return new MachineBindingDetail(layout, slots, tanks);

            #region Internal

            MachineItemSlotBinding ItemBinding(int slot, Guid itemGuid, int count, IReadOnlyList<ItemId> accepted)
            {
                return new MachineItemSlotBinding(slot, MasterHolder.ItemMaster.GetItemId(itemGuid), count, accepted);
            }

            #endregion
        }
    }
}
