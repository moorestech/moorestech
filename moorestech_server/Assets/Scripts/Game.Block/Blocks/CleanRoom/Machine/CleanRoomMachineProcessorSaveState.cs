using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.Machine.State.Save;
using Game.Block.Blocks.CleanRoom.Machine.YieldPreview;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.Inventory.Input;
using Game.Block.Blocks.Machine.State;
using Game.Context;
using Game.Fluid;
using Game.Block.Factory.BlockTemplate;
using Newtonsoft.Json.Linq;
using Mooresmaster.Model.MachineRecipesModule;
using Game.Block.Interface.Component;
using UnityEngine;

namespace Game.Block.Blocks.CleanRoom.Machine
{
    internal static class CleanRoomMachineProcessorSaveState
    {
        public static CleanRoomMachineProcessorSaveJsonObject Build(VanillaMachineInputInventory input, VanillaMachineOutputInventory output, VanillaMachineModuleInventory module, ProcessState currentState, ProcessingMachineProcessState processingState, uint cycleCount, MachineRecipeMasterElement selectedRecipe, IdleMachineProcessState idleState)
        {
            // 通常機械の加工状態にクリーンルーム固有の抽選カウンタを加えて保存する
            // Save normal machine processing state plus the clean-room draw counter
            return new CleanRoomMachineProcessorSaveJsonObject
            {
                CycleCount = cycleCount,
                TotalTicks = processingState.TotalTicks,
                ConsumedInputItemGuids = processingState.ConsumedInputItemIds?.Select(id => MasterHolder.ItemMaster.GetItemMaster(id).ItemGuid.ToString()).ToList(),
                PendingRealization = MachineRealizationSaveJsonObject.Capture(idleState, idleState.TryGetPendingRealization(out var pending) ? CleanRoomDrawRecordSave.Capture(pending.DrawRecord) : null),
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

        public static void Restore(Dictionary<string, object> componentStates, string saveKey, VanillaMachineInputInventory input, VanillaMachineOutputInventory output, VanillaMachineModuleInventory module, out ProcessState state, out MachineProcessingJob processingJob, out uint cycleCount, out MachineRecipeMasterElement selectedRecipe, out MachineRealization pendingRealization)
        {
            state = ProcessState.Idle;
            processingJob = null;
            cycleCount = 0;
            selectedRecipe = null;
            pendingRealization = null;
            // 旧セーブではサイクル数以外が欠けるため、各値は個別に復元する
            // Older saves lack fields other than cycle count, so restore each value independently
            if (!BlockComponentStateReader.TryRead<CleanRoomMachineProcessorSaveJsonObject>(componentStates, saveKey, out var saveData)) return;
            MachineRealizationSaveJsonObject.WarnIfLegacy(JObject.FromObject(componentStates[saveKey]));
            cycleCount = saveData.CycleCount;
            pendingRealization = saveData.PendingRealization?.Restore(CleanRoomDrawRecordSave.Restore(saveData.PendingRealization.DrawRecord));
            RestoreSlots(saveData);
            BlockTemplateUtil.RestoreMachineFluids(saveData.InputFluidSlot, saveData.OutputFluidSlot, input, output, true);

            // 加工中ジョブは平らなキーから組む（レシピがマスタから消えていれば null）
            // Build the running job from the flat keys (null when its recipe left the master)
            processingJob = MachineProcessingJob.RestoreFromFlatSave(saveData.RecipeGuidStr, saveData.RemainingSeconds, saveData.PendingOutputs, saveData.InheritedLevel, saveData.TotalTicks, saveData.ConsumedInputItemGuids);

            // 選択レシピを復元。GUIDがマスタから消えていれば未選択に戻す（機械は停止するが壊れない）
            // Restore the selected recipe; a GUID missing from the master falls back to unselected
            if (!string.IsNullOrEmpty(saveData.SelectedRecipeGuidStr) && Guid.TryParse(saveData.SelectedRecipeGuidStr, out var selectedGuid))
            {
                selectedRecipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(selectedGuid);
            }
            state = (ProcessState)saveData.State;

            // 加工中（出力詰まり含む）でジョブか選択レシピを復元できない場合は破損扱いで、ジョブも捨ててIdleへ戻す
            // A mid-job state (output blockage included) that cannot restore its job or selected recipe is corrupt, so drop the job too and fall back to Idle
            var isMidJob = state == ProcessState.Processing || state == ProcessState.OutputBlocked;
            if (isMidJob && (processingJob == null || selectedRecipe == null))
            {
                Debug.LogWarning($"[CleanRoomMachineProcessorSaveState] 加工中セーブのジョブまたは選択レシピが欠けているためIdleへ戻します 状態:{state}");
                state = ProcessState.Idle;
                processingJob = null;
            }

            #region Internal

            void RestoreSlots(CleanRoomMachineProcessorSaveJsonObject data)
            {
                // 旧スロット数が現在の上限を超えるときは通常機械と同様にログを残す
                // Match normal machines by logging saved slot ranges exceeding current limits
                if (data.InputSlot?.Count > input.InputSlot.Count || data.OutputSlot?.Count > output.OutputSlot.Count || data.ModuleSlot?.Count > module.ModuleSlot.Count)
                    Debug.LogError("清浄室機械のインベントリサイズがセーブより少ないため、超過分のアイテムは消失します");
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

            #endregion
        }
    }
}
