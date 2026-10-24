using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.Inventory.Input;
using Game.Block.Blocks.Machine.State;
using Game.Context;
using Game.Fluid;
using Mooresmaster.Model.MachineRecipesModule;
using Game.Block.Interface.Component;
using UnityEngine;

namespace Game.Block.Blocks.CleanRoom.Machine
{
    internal static class CleanRoomMachineProcessorSaveState
    {
        public static CleanRoomMachineProcessorSaveJsonObject Build(VanillaMachineInputInventory input, VanillaMachineOutputInventory output, VanillaMachineModuleInventory module, ProcessState currentState, ProcessingMachineProcessState processingState, uint cycleCount, MachineRecipeMasterElement selectedRecipe)
        {
            // 通常機械の加工状態にクリーンルーム固有の抽選カウンタを加えて保存する
            // Save normal machine processing state plus the clean-room draw counter
            return new CleanRoomMachineProcessorSaveJsonObject
            {
                CycleCount = cycleCount,
                State = (int)currentState,
                RemainingSeconds = GameUpdater.TicksToSeconds(processingState.RemainingTicks),
                RecipeGuidStr = processingState.RecipeGuid.ToString(),
                PendingOutputs = processingState.PendingOutputs?.Select(item => new ItemStackSaveJsonObject(item)).ToList(),
                InputSlot = input.InputSlot.Select(item => new ItemStackSaveJsonObject(item)).ToList(),
                OutputSlot = output.OutputSlot.Select(item => new ItemStackSaveJsonObject(item)).ToList(),
                ModuleSlot = module.ModuleSlot.Select(item => new ItemStackSaveJsonObject(item)).ToList(),
                SelectedRecipeGuidStr = selectedRecipe?.MachineRecipeGuid.ToString(),
                InheritedLevel = processingState.InheritedLevel,
                InputFluidSlot = input.FluidInputSlot.Select(fluid => new FluidContainerSaveJsonObject(fluid)).ToList(),
                OutputFluidSlot = output.FluidOutputSlot.Select(fluid => new FluidContainerSaveJsonObject(fluid)).ToList(),
            };
        }

        public static void Restore(Dictionary<string, object> componentStates, string saveKey, VanillaMachineInputInventory input, VanillaMachineOutputInventory output, VanillaMachineModuleInventory module, out ProcessState state, out MachineProcessingJob processingJob, out uint cycleCount, out MachineRecipeMasterElement selectedRecipe)
        {
            state = ProcessState.Idle;
            processingJob = null;
            cycleCount = 0;
            selectedRecipe = null;
            // 旧セーブではサイクル数以外が欠けるため、各値は個別に復元する
            // Older saves lack fields other than cycle count, so restore each value independently
            if (!BlockComponentStateReader.TryRead<CleanRoomMachineProcessorSaveJsonObject>(componentStates, saveKey, out var saveData)) return;
            cycleCount = saveData.CycleCount;
            RestoreSlots(saveData);
            RestoreFluids(saveData);

            // 加工中ジョブは平らなキーから組む（レシピがマスタから消えていれば null）
            // Build the running job from the flat keys (null when its recipe left the master)
            processingJob = MachineProcessingJob.RestoreFromFlatSave(saveData.RecipeGuidStr, saveData.RemainingSeconds, saveData.PendingOutputs, saveData.InheritedLevel);

            // 選択レシピを復元。GUIDがマスタから消えていれば未選択に戻す（機械は停止するが壊れない）
            // Restore the selected recipe; a GUID missing from the master falls back to unselected
            if (!string.IsNullOrEmpty(saveData.SelectedRecipeGuidStr) && Guid.TryParse(saveData.SelectedRecipeGuidStr, out var selectedGuid))
            {
                selectedRecipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(selectedGuid);
            }
            state = (ProcessState)saveData.State;

            // 加工中（出力詰まり含む）でジョブか選択レシピを復元できない場合は破損扱いでIdleへ戻す
            // A mid-job state (output blockage included) that cannot restore its job or selected recipe is corrupt and falls back to Idle
            var isMidJob = state == ProcessState.Processing || state == ProcessState.OutputBlocked;
            if (isMidJob && (processingJob == null || selectedRecipe == null)) state = ProcessState.Idle;

            #region Internal

            void RestoreSlots(CleanRoomMachineProcessorSaveJsonObject data)
            {
                if (data.InputSlot != null)
                {
                    for (var i = 0; i < data.InputSlot.Count && i < input.InputSlot.Count; i++) input.SetItemWithoutEvent(i, data.InputSlot[i].ToItemStack());
                }

                if (data.OutputSlot != null)
                {
                    for (var i = 0; i < data.OutputSlot.Count && i < output.OutputSlot.Count; i++) output.SetItemWithoutEvent(i, data.OutputSlot[i].ToItemStack());
                }

                if (data.ModuleSlot != null)
                {
                    for (var i = 0; i < data.ModuleSlot.Count && i < module.ModuleSlot.Count; i++) module.SetItemWithoutEvent(i, data.ModuleSlot[i].ToItemStack());
                }
            }

            void RestoreFluids(CleanRoomMachineProcessorSaveJsonObject data)
            {
                // キーは版9の保存とマイグレーションで必ずある。欠けたセーブは液体を黙って捨てず、継承レベルの欠けと同じく明示の例外でロードを止める
                // Version-9 saves and the migration always write the keys; a save missing them stops the load with an explicit exception, like a missing inherited level, instead of silently dropping fluid
                if (data.InputFluidSlot == null || data.OutputFluidSlot == null)
                {
                    throw new InvalidOperationException($"清浄室機械のセーブに液体タンクのキーがありません。 inputFluidSlot={(data.InputFluidSlot == null ? "missing" : "present")} outputFluidSlot={(data.OutputFluidSlot == null ? "missing" : "present")}");
                }

                if (input.FluidInputSlot.Count < data.InputFluidSlot.Count || output.FluidOutputSlot.Count < data.OutputFluidSlot.Count)
                {
                    Debug.LogError($"清浄室機械のタンク数がセーブより少ないため、超過分の液体は消失します。 input={data.InputFluidSlot.Count}->{input.FluidInputSlot.Count} output={data.OutputFluidSlot.Count}->{output.FluidOutputSlot.Count}");
                }

                for (var i = 0; i < data.InputFluidSlot.Count && i < input.FluidInputSlot.Count; i++)
                {
                    input.FluidInputSlot[i].FluidId = data.InputFluidSlot[i].FluidId;
                    input.FluidInputSlot[i].Amount = data.InputFluidSlot[i].Amount;
                }

                for (var i = 0; i < data.OutputFluidSlot.Count && i < output.FluidOutputSlot.Count; i++)
                {
                    output.FluidOutputSlot[i].FluidId = data.OutputFluidSlot[i].FluidId;
                    output.FluidOutputSlot[i].Amount = data.OutputFluidSlot[i].Amount;
                }
            }

            #endregion
        }
    }
}
