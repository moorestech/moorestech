using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Block.Blocks;
using Game.Block.Blocks.CleanRoom;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Component;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Game.UnlockState;
using Mooresmaster.Model.BlocksModule;
using Newtonsoft.Json;

namespace Game.Block.Factory.BlockTemplate
{
    // VanillaMachineTemplate をコピーし、専用コンポーネント＋CleanRoomStateReceiverComponent を合成する（blockType CleanRoomMachine 用）。
    // Copied from VanillaMachineTemplate; composes the dedicated components + CleanRoomStateReceiverComponent for blockType CleanRoomMachine.
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

            // 入力とモジュールは Vanilla を再利用、出力は専用型を組む
            // Reuse Vanilla input/module; build the dedicated output inventory
            var input = new VanillaMachineInputInventory(
                blockId, machineParam.InputSlotCount, 0, 0f,
                _blockInventoryUpdateEvent, blockInstanceId,
                ServerContext.GetService<IGameUnlockStateDataController>());

            // 出力なし通知をプロセッサへ橋渡しする（プロセッサは後で生成するため遅延束縛）
            // Forward the no-output notification to the processor (bound lazily; processor is created later)
            CleanRoomMachineProcessorComponent processorRef = null;

            var output = new CleanRoomMachineOutputInventory(
                machineParam.OutputSlotCount, 0, 0f, ServerContext.ItemStackFactory,
                _blockInventoryUpdateEvent, blockInstanceId, machineParam.InputSlotCount,
                inventoryConnectorComponent, receiver, () => processorRef?.NotifyNoOutput());

            var module = new VanillaMachineModuleInventory(
                machineParam.ModuleSlotCount, _blockInventoryUpdateEvent, blockInstanceId,
                machineParam.InputSlotCount, machineParam.OutputSlotCount);

            var requestPower = (float)machineParam.RequiredPower;

            // 新規作成またはセーブから復元
            // Create new or restore from save
            var processor = componentStates == null
                ? new CleanRoomMachineProcessorComponent(input, output, receiver, blockInstanceId, requestPower)
                : LoadProcessor(componentStates, input, output, module, receiver, blockInstanceId, requestPower);
            processorRef = processor;

            var blockInventory = new CleanRoomMachineBlockInventoryComponent(input, output, module);
            var machineSave = new CleanRoomMachineSaveComponent(input, output, module, processor);
            var electric = new CleanRoomMachineElectricComponent(blockInstanceId, processor);

            var components = new List<IBlockComponent>
            {
                blockInventory,
                machineSave,
                processor,
                electric,
                receiver,
                inventoryConnectorComponent,
            };

            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }

        // セーブから入出力・加工状態を復元する（VanillaのMachineLoadState を専用型向けに移植）
        // Restore inventory and processing state from save (ported from MachineLoadState for the dedicated types)
        private CleanRoomMachineProcessorComponent LoadProcessor(
            Dictionary<string, string> componentStates,
            VanillaMachineInputInventory input, CleanRoomMachineOutputInventory output, VanillaMachineModuleInventory module,
            CleanRoomStateReceiverComponent receiver, BlockInstanceId blockInstanceId, float requestPower)
        {
            var state = componentStates[CleanRoomMachineSaveComponent.SaveKeyStatic];
            var jsonObject = JsonConvert.DeserializeObject<CleanRoomMachineJsonObject>(state);

            var inputItems = jsonObject.InputSlot.Select(item => item.ToItemStack()).ToList();
            for (var i = 0; i < inputItems.Count && i < input.InputSlot.Count; i++) input.SetItemWithoutEvent(i, inputItems[i]);

            var outputItems = jsonObject.OutputSlot.Select(item => item.ToItemStack()).ToList();
            for (var i = 0; i < outputItems.Count && i < output.OutputSlot.Count; i++) output.SetItemWithoutEvent(i, outputItems[i]);

            if (jsonObject.ModuleSlot != null)
            {
                var moduleItems = jsonObject.ModuleSlot.Select(item => item.ToItemStack()).ToList();
                for (var i = 0; i < moduleItems.Count && i < module.ModuleSlot.Count; i++) module.SetItemWithoutEvent(i, moduleItems[i]);
            }

            var processorJson = jsonObject.Processor;
            var recipe = processorJson.RecipeGuid == Guid.Empty ? null : MasterHolder.MachineRecipesMaster.GetRecipeElement(processorJson.RecipeGuid);
            var remainingTicks = GameUpdater.SecondsToTicks(processorJson.RemainingSeconds);

            return new CleanRoomMachineProcessorComponent(
                input, output, receiver, blockInstanceId, requestPower,
                (Game.Block.Blocks.Machine.ProcessState)processorJson.State, remainingTicks, recipe, processorJson.ProcessedCycleCount);
        }
    }
}
