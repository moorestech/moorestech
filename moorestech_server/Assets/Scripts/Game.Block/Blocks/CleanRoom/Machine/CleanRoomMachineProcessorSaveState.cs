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
using Game.Fluid;
using Newtonsoft.Json;

namespace Game.Block.Blocks.CleanRoom.Machine
{
    internal static class CleanRoomMachineProcessorSaveState
    {
        public static CleanRoomMachineProcessorSaveJsonObject Build(VanillaMachineInputInventory input, VanillaMachineOutputInventory output, VanillaMachineModuleInventory module, Guid? recipeGuid, ProcessState currentState, ProcessingMachineProcessState processingState, uint cycleCount)
        {
            // 通常機械の加工状態にクリーンルーム固有の抽選カウンタを加えて保存する
            // Save normal machine processing state plus the clean-room draw counter
            return new CleanRoomMachineProcessorSaveJsonObject
            {
                CycleCount = cycleCount,
                State = (int)currentState,
                TotalSeconds = GameUpdater.TicksToSeconds(processingState.TotalTicks),
                RemainingSeconds = GameUpdater.TicksToSeconds(processingState.RemainingTicks),
                RecipeGuidStr = recipeGuid?.ToString(),
                PendingOutputs = processingState.PendingOutputs?.Select(item => new ItemStackSaveJsonObject(item)).ToList(),
                PendingFluidOutputs = processingState.PendingFluidOutputs?.Select(fluid => new MachineFluidStackSaveJsonObject(fluid)).ToList(),
                ConsumedItems = processingState.ConsumedItems?.Select(item => new ItemStackSaveJsonObject(item)).ToList(),
                InputSlot = input.InputSlot.Select(item => new ItemStackSaveJsonObject(item)).ToList(),
                OutputSlot = output.OutputSlot.Select(item => new ItemStackSaveJsonObject(item)).ToList(),
                ModuleSlot = module.ModuleSlot.Select(item => new ItemStackSaveJsonObject(item)).ToList(),
            };
        }

        public static void Restore(Dictionary<string, string> componentStates, string saveKey, VanillaMachineInputInventory input, VanillaMachineOutputInventory output, VanillaMachineModuleInventory module,
            out Guid? recipeGuid, out ProcessState state, out uint totalTicks, out uint remainingTicks, out List<IItemStack> pendingOutputs,
            out List<FluidStack> pendingFluidOutputs, out List<IItemStack> consumedItems, out uint cycleCount)
        {
            recipeGuid = null;
            state = ProcessState.Idle;
            totalTicks = 0;
            remainingTicks = 0;
            pendingOutputs = null;
            pendingFluidOutputs = null;
            consumedItems = null;
            cycleCount = 0;
            if (componentStates == null || !componentStates.TryGetValue(saveKey, out var stateRaw)) return;

            // 旧セーブではサイクル数以外が欠けるため、各値は個別に復元する
            // Older saves lack fields other than cycle count, so restore each value independently
            var saveData = JsonConvert.DeserializeObject<CleanRoomMachineProcessorSaveJsonObject>(stateRaw);
            cycleCount = saveData.CycleCount;
            RestoreSlots(saveData);

            if (saveData.RecipeGuidStr != null) recipeGuid = saveData.GetRecipeGuid();
            totalTicks = GameUpdater.SecondsToTicks(saveData.TotalSeconds);
            remainingTicks = GameUpdater.SecondsToTicks(saveData.RemainingSeconds);
            pendingOutputs = saveData.PendingOutputs?.Select(item => item.ToItemStack()).ToList();
            pendingFluidOutputs = saveData.PendingFluidOutputs?.Select(fluid => fluid.ToFluidStack()).ToList();
            consumedItems = saveData.ConsumedItems?.Select(item => item.ToItemStack()).ToList();
            state = (ProcessState)saveData.State;

            // 加工中の欠損データを資源消失として黙認しない
            // Reject incomplete processing data instead of silently losing consumed resources
            if (state == ProcessState.Processing && (pendingOutputs == null || pendingFluidOutputs == null || consumedItems == null))
                throw new InvalidOperationException("Clean-room machine save is missing its processing snapshot.");

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

            #endregion
        }
    }
}
