using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.State;
using Game.Context;
using Mooresmaster.Model.MachineRecipesModule;
using Newtonsoft.Json;

namespace Game.Block.Blocks.CleanRoom.Machine
{
    internal static class CleanRoomMachineProcessorSaveState
    {
        public static CleanRoomMachineProcessorSaveJsonObject Build(VanillaMachineInputInventory input, VanillaMachineOutputInventory output, ProcessState currentState, ProcessingMachineProcessState processingState, uint cycleCount)
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
            };
        }

        public static void Restore(Dictionary<string, string> componentStates, string saveKey, VanillaMachineInputInventory input, VanillaMachineOutputInventory output, out ProcessState state, out uint remainingTicks, out MachineRecipeMasterElement recipe, out List<IItemStack> pendingOutputs, out uint cycleCount)
        {
            state = ProcessState.Idle;
            remainingTicks = 0;
            recipe = null;
            pendingOutputs = null;
            cycleCount = 0;
            if (componentStates == null || !componentStates.TryGetValue(saveKey, out var stateRaw)) return;

            // 旧セーブではサイクル数以外が欠けるため、各値は個別に復元する
            // Older saves lack fields other than cycle count, so restore each value independently
            var saveData = JsonConvert.DeserializeObject<CleanRoomMachineProcessorSaveJsonObject>(stateRaw);
            cycleCount = saveData.CycleCount;
            RestoreSlots(saveData);

            if (saveData.RecipeGuidStr != null && saveData.RecipeGuid != Guid.Empty)
            {
                recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(saveData.RecipeGuid);
            }

            remainingTicks = GameUpdater.SecondsToTicks(saveData.RemainingSeconds);
            pendingOutputs = saveData.PendingOutputs?.Select(item => item.ToItemStack()).ToList();
            state = (ProcessState)saveData.State;

            // Processingでレシピを復元できない場合だけ破損扱いでIdleへ戻す
            // Only Processing without a restorable recipe is corrupt and falls back to Idle
            if (state == ProcessState.Processing && recipe == null) state = ProcessState.Idle;

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
            }

            #endregion
        }
    }
}
