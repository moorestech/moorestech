using System.Collections.Generic;
using Core.Master;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.MachineRecipesModule;

namespace Client.WebUiHost.Game.Topics.BlockDetail
{
    /// <summary>
    ///     選択レシピのスロット束縛をDTOへ組み立てる。サーバーの束縛規則（入力スロットi＝素材i、出力スロットj＝生産物j、
    ///     タンクi＝液体i）をここ1箇所で写し、Web側は結果を読むだけにする
    ///     Builds the selected recipe's slot binding for the DTO. The server's rule (input slot i = input i,
    ///     output slot j = output j, tank i = fluid i) is mirrored here alone so the Web side only reads the result
    /// </summary>
    public static class MachineSlotBindingDtoBuilder
    {
        public static List<MachineSlotBindingDto> BuildSlotBindings(MachineRecipeMasterElement recipe, IMachineParam machineParam)
        {
            var bindings = new List<MachineSlotBindingDto>();
            if (recipe == null) return bindings;

            for (var i = 0; i < recipe.InputItems.Length; i++)
            {
                bindings.Add(CreateBinding(i, recipe.InputItems[i].ItemGuid, recipe.InputItems[i].Count));
            }

            for (var j = 0; j < recipe.OutputItems.Length; j++)
            {
                bindings.Add(CreateBinding(machineParam.InputSlotCount + j, recipe.OutputItems[j].ItemGuid, recipe.OutputItems[j].Count));
            }

            return bindings;

            #region Internal

            MachineSlotBindingDto CreateBinding(int slot, System.Guid itemGuid, int count)
            {
                return new MachineSlotBindingDto
                {
                    Slot = slot,
                    ItemId = MasterHolder.ItemMaster.GetItemId(itemGuid).AsPrimitive(),
                    Count = count,
                };
            }

            #endregion
        }

        public static List<MachineTankBindingDto> BuildTankBindings(MachineRecipeMasterElement recipe, IMachineParam machineParam)
        {
            var bindings = new List<MachineTankBindingDto>();
            if (recipe == null) return bindings;

            for (var i = 0; i < recipe.InputFluids.Length; i++)
            {
                bindings.Add(CreateBinding(i, recipe.InputFluids[i].FluidGuid, recipe.InputFluids[i].Amount));
            }

            for (var j = 0; j < recipe.OutputFluids.Length; j++)
            {
                bindings.Add(CreateBinding(machineParam.InputTankCount + j, recipe.OutputFluids[j].FluidGuid, recipe.OutputFluids[j].Amount));
            }

            return bindings;

            #region Internal

            MachineTankBindingDto CreateBinding(int tank, System.Guid fluidGuid, double amount)
            {
                return new MachineTankBindingDto
                {
                    Tank = tank,
                    FluidGuid = fluidGuid.ToString("D"),
                    Amount = amount,
                };
            }

            #endregion
        }
    }
}
