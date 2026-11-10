using System;
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Machine;
using Core.Master;
using Game.Block.Interface.State;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.MachineRecipesModule;
using UnityEngine;

namespace Client.Ui.State.BlockInventory.Machine.Detail
{
    /// <summary>
    /// 機械の 2 つの状態詳細と param から詳細を組む（Web ホスト BlockDetailDtoBuilder の機械部分の写し）
    /// Builds the machine detail from its two state details and param (port of the machine part of the Web host's BlockDetailDtoBuilder)
    /// </summary>
    public static class MachineDetailConversion
    {
        public static bool TryBuild(CommonMachineBlockStateDetail common, MachineBlockStateDetail machine, IMachineParam param, bool gearDriven, out MachineDetail detail)
        {
            detail = null;
            // 読めない値は機械の区画ごと出さず、開発者ログへ出す
            // Unreadable values drop the whole machine section and go to the developer log
            if (!MachineRecipeSelection.TryParse(machine.SelectedRecipeGuid, out var selection))
            {
                Debug.LogError($"[MachineDetailConversion] unreadable SelectedRecipeGuid: '{machine.SelectedRecipeGuid}'");
                return false;
            }
            if (!Guid.TryParse(machine.MachineRecipeGuid, out var runningGuid))
            {
                Debug.LogError($"[MachineDetailConversion] unreadable MachineRecipeGuid: '{machine.MachineRecipeGuid}'");
                return false;
            }
            if (!MachineStateTypeConversion.TryConvert(common.CurrentStateType, out var state))
            {
                Debug.LogError($"[MachineDetailConversion] unknown CurrentStateType: '{common.CurrentStateType}'");
                return false;
            }

            // 束縛は選択レシピ、秒と出力は加工中レシピから引く（Web ホストと同じ）
            // Binding follows the selected recipe; time and outputs follow the running recipe (same as the Web host)
            var running = MasterHolder.MachineRecipesMaster.GetRecipeElement(runningGuid);
            var selected = selection is SelectedMachineRecipe s ? MasterHolder.MachineRecipesMaster.GetRecipeElement(s.RecipeGuid) : null;
            var binding = MachineSlotBindingConversion.Build(selected, param);
            detail = new MachineDetail(selection, state, common.CurrentPower, common.RequestPower, machine.ProcessingRate, machine.IsOutputSpaceShort, gearDriven, RunningTime(running), RunningOutputs(running), binding);
            return true;

            #region Internal

            // silent-ok: 加工中レシピが無い（空 GUID）機械は秒 0・出力なしで、分間生産数を出さない（Web と同じ） / MachineDetailConversionTest が見る
            static double RunningTime(MachineRecipeMasterElement recipe) => recipe == null ? 0 : recipe.Time;

            static IReadOnlyList<RecipeOutputItem> RunningOutputs(MachineRecipeMasterElement recipe)
            {
                var outputs = new List<RecipeOutputItem>();
                if (recipe == null) return outputs;
                foreach (var output in recipe.OutputItems) outputs.Add(new RecipeOutputItem(MasterHolder.ItemMaster.GetItemId(output.ItemGuid), output.Count));
                return outputs;
            }

            #endregion
        }
    }
}
