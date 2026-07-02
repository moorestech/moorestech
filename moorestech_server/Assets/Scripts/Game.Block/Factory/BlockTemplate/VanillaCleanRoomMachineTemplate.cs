using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks;
using Game.Block.Blocks.CleanRoom;
using Game.Block.Blocks.Fluid;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.Module;
using Game.Block.Component;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Game.UnlockState;
using Mooresmaster.Model.BlocksModule;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.Block.Factory.BlockTemplate
{
    // 専用の出力/プロセッサに CleanRoomStateReceiverComponent を合成する（blockType CleanRoomMachine 用）。
    // 統合インベントリ・流体インベントリ・モジュール効果は Vanilla の実装をそのまま再利用する。
    // Composes the dedicated output/processor with CleanRoomStateReceiverComponent (for blockType CleanRoomMachine).
    // The unified inventory, fluid inventory, and module effects reuse the Vanilla implementations as-is.
    public class VanillaCleanRoomMachineTemplate : IBlockTemplate
    {
        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdateEvent;

        public VanillaCleanRoomMachineTemplate(BlockOpenableInventoryUpdateEvent blockInventoryUpdateEvent)
        {
            _blockInventoryUpdateEvent = blockInventoryUpdateEvent;
        }

        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams)
        {
            return GetBlock(null, blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return GetBlock(componentStates, blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        private IBlock GetBlock(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var machineParam = blockMasterElement.BlockParam as CleanRoomMachineBlockParam;

            BlockConnectorComponent<IBlockInventory> inventoryConnectorComponent = BlockTemplateUtil.CreateInventoryConnector(machineParam.InventoryConnectors, blockPositionInfo);

            var blockId = MasterHolder.BlockMaster.GetBlockId(blockMasterElement.BlockGuid);

            // 受信ゲートを先に作る（出力インベントリが効果値を参照するため）
            // Build the receiver first because the output inventory references its effect values
            var receiver = new CleanRoomStateReceiverComponent();

            // 流体タンク容量はマスタ値を使う（Vanillaと同じ）
            // Use master values for fluid tank counts/capacity (same as Vanilla)
            var inputTankCount = machineParam.InputTankCount;
            var outputTankCount = machineParam.OutputTankCount;
            var innerTankCapacity = machineParam.InnerTankCapacity;

            // 入力とモジュールは Vanilla を再利用、出力は専用型を組む
            // Reuse Vanilla input/module; build the dedicated output inventory
            var input = new VanillaMachineInputInventory(
                blockId, machineParam.InputSlotCount, inputTankCount, innerTankCapacity,
                _blockInventoryUpdateEvent, blockInstanceId,
                ServerContext.GetService<IGameUnlockStateDataController>());

            // 出力なし通知をプロセッサへ橋渡しする（プロセッサは後で生成するため遅延束縛）
            // Forward the no-output notification to the processor (bound lazily; processor is created later)
            CleanRoomMachineProcessorComponent processorRef = null;

            var output = new CleanRoomMachineOutputInventory(
                machineParam.OutputSlotCount, outputTankCount, innerTankCapacity, ServerContext.ItemStackFactory,
                _blockInventoryUpdateEvent, blockInstanceId, machineParam.InputSlotCount,
                inventoryConnectorComponent, receiver, () => processorRef?.NotifyNoOutput());

            var module = new VanillaMachineModuleInventory(
                machineParam.ModuleSlotCount, _blockInventoryUpdateEvent, blockInstanceId,
                machineParam.InputSlotCount, machineParam.OutputSlotCount);

            // モジュール効果（速度/電力）を専用プロセッサへ供給する（Vanillaと同じ集計器）
            // Feed module effects (speed/power) into the dedicated processor (same aggregator as Vanilla)
            var moduleEffect = new MachineModuleEffectComponent(module);

            var requestPower = machineParam.RequiredPower;

            // 新規作成またはセーブから復元
            // Create new or restore from save
            var processor = componentStates == null
                ? new CleanRoomMachineProcessorComponent(input, output, receiver, moduleEffect, blockInstanceId, requestPower)
                : LoadProcessor(componentStates, input, output, module, receiver, moduleEffect, blockInstanceId, requestPower, blockMasterElement);
            processorRef = processor;

            var blockInventory = new VanillaMachineBlockInventoryComponent(input, output, module);
            var machineSave = new CleanRoomMachineSaveComponent(input, output, module, processor);
            var electric = new CleanRoomMachineElectricComponent(blockInstanceId, processor);

            var components = new List<IBlockComponent>
            {
                blockInventory,
                machineSave,
                moduleEffect,
                processor,
                electric,
                receiver,
                inventoryConnectorComponent,
            };

            // 流体接続のサポートを追加（流体インベントリコネクタが定義されている場合・Vanillaを再利用）
            // Add fluid connection support when fluid connectors are defined (reuses the Vanilla component)
            if (machineParam.FluidInventoryConnectors != null && (inputTankCount > 0 || outputTankCount > 0))
            {
                var fluidConnector = IFluidInventory.CreateFluidInventoryConnector(machineParam.FluidInventoryConnectors, blockPositionInfo);
                var fluidInventory = new VanillaMachineFluidInventoryComponent(input.FluidInputSlot, output.FluidOutputSlot, fluidConnector);

                components.Add(fluidConnector);
                components.Add(fluidInventory);
            }

            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }

        // セーブから入出力・加工状態を復元する（VanillaのMachineLoadState を専用型向けに移植）
        // Restore inventory and processing state from save (ported from MachineLoadState for the dedicated types)
        private CleanRoomMachineProcessorComponent LoadProcessor(
            Dictionary<string, string> componentStates,
            VanillaMachineInputInventory input, CleanRoomMachineOutputInventory output, VanillaMachineModuleInventory module,
            CleanRoomStateReceiverComponent receiver, MachineModuleEffectComponent moduleEffect,
            BlockInstanceId blockInstanceId, float requestPower, BlockMasterElement blockMasterElement)
        {
            var state = componentStates[CleanRoomMachineSaveComponent.SaveKeyStatic];
            var jsonObject = JsonConvert.DeserializeObject<CleanRoomMachineJsonObject>(state);

            // スロット超過はエラーログを残して打ち切る（Vanillaと同じ・サイレント消失させない）
            // Log and stop on slot overflow (same as Vanilla; never drop items silently)
            RestoreSlots(jsonObject.InputSlot, input.InputSlot.Count, input.SetItemWithoutEvent);
            RestoreSlots(jsonObject.OutputSlot, output.OutputSlot.Count, output.SetItemWithoutEvent);
            if (jsonObject.ModuleSlot != null) RestoreSlots(jsonObject.ModuleSlot, module.ModuleSlot.Count, module.SetItemWithoutEvent);

            // 流体タンクを復元（Vanilla の MachineLoadState と同じ）
            // Restore fluid tanks (same as Vanilla MachineLoadState)
            if (jsonObject.InputFluidSlot != null)
            {
                for (var i = 0; i < jsonObject.InputFluidSlot.Count && i < input.FluidInputSlot.Count; i++)
                {
                    var fluidData = jsonObject.InputFluidSlot[i];
                    input.FluidInputSlot[i].FluidId = fluidData.FluidId;
                    input.FluidInputSlot[i].Amount = fluidData.Amount;
                }
            }

            if (jsonObject.OutputFluidSlot != null)
            {
                for (var i = 0; i < jsonObject.OutputFluidSlot.Count && i < output.FluidOutputSlot.Count; i++)
                {
                    var fluidData = jsonObject.OutputFluidSlot[i];
                    output.FluidOutputSlot[i].FluidId = fluidData.FluidId;
                    output.FluidOutputSlot[i].Amount = fluidData.Amount;
                }
            }

            var processorJson = jsonObject.Processor;
            var recipe = processorJson.RecipeGuid == Guid.Empty ? null : MasterHolder.MachineRecipesMaster.GetRecipeElement(processorJson.RecipeGuid);
            var remainingTicks = GameUpdater.SecondsToTicks(processorJson.RemainingSeconds);

            return new CleanRoomMachineProcessorComponent(
                input, output, receiver, moduleEffect, blockInstanceId, requestPower,
                (Game.Block.Blocks.Machine.ProcessState)processorJson.State, remainingTicks, recipe, processorJson.ProcessedCycleCount);

            #region Internal

            void RestoreSlots(List<ItemStackSaveJsonObject> savedItems, int slotCount, Action<int, IItemStack> setItemWithoutEvent)
            {
                var items = savedItems.Select(item => item.ToItemStack()).ToList();
                for (var i = 0; i < items.Count; i++)
                {
                    if (slotCount <= i)
                    {
                        Debug.LogError($"ロードするデータのインベントリサイズが超過しています。一部のアイテムは消失します。ブロック名:{blockMasterElement.Name} Guid:{blockMasterElement.BlockGuid}");
                        break;
                    }
                    setItemWithoutEvent(i, items[i]);
                }
            }

            #endregion
        }
    }
}
