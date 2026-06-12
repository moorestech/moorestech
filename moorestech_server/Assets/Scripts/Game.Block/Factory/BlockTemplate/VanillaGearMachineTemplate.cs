using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks;
using Game.Block.Blocks.Gear;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.Module;
using Game.Block.Component;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaGearMachineTemplate : IBlockTemplate
    {
        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdateEvent;
        
        public VanillaGearMachineTemplate(BlockOpenableInventoryUpdateEvent blockInventoryUpdateEvent)
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
            var machineParam = blockMasterElement.BlockParam as GearMachineBlockParam;
            BlockConnectorComponent<IBlockInventory> inventoryConnectorComponent = BlockTemplateUtil.CreateInventoryConnector(machineParam.InventoryConnectors, blockPositionInfo);
            
            var blockId = MasterHolder.BlockMaster.GetBlockId(blockMasterElement.BlockGuid);
            var (input, output, module) = BlockTemplateUtil.GetMachineIOInventory(blockId, blockInstanceId, machineParam, inventoryConnectorComponent, _blockInventoryUpdateEvent);
            
            var connectSetting = machineParam.Gear.GearConnects;
            var gearConnector = new BlockConnectorComponent<IGearEnergyTransformer>(connectSetting, connectSetting, blockPositionInfo);
            var gearConsumption = machineParam.GearConsumption;
            var gearEnergyTransformer = new GearEnergyTransformer(gearConsumption, blockInstanceId, gearConnector);

            var requirePower = (float)(gearConsumption.BaseTorque * gearConsumption.BaseRpm);
            
            // モジュール効果コンポーネントをプロセッサより先に生成して渡す
            // Create the module effect component before the processor and pass it in
            var effectComponent = new MachineModuleEffectComponent(module);

            // パラメーターをロードするか、新規作成する
            // Load the parameters or create new ones
            var processor = componentStates == null
                ? new VanillaMachineProcessorComponent(input, output, null, requirePower, effectComponent)
                : BlockTemplateUtil.MachineLoadState(componentStates, input, output, module, effectComponent, requirePower, blockMasterElement);

            // 省エネモジュール倍率を要求トルクへ配線する（照会時に都度集計。加工中のみ適用、Idleは中立1.0）
            // Wire the efficiency multiplier into required torque (aggregated per query; applied only while processing, neutral when idle)
            gearEnergyTransformer.SetConsumptionMultiplierSource(processor);

            var blockInventory = new VanillaMachineBlockInventoryComponent(input, output, module);
            var machineSave = new VanillaMachineSaveComponent(input, output, module, processor);

            var machineComponent = new VanillaGearMachineComponent(processor, gearEnergyTransformer);

            var components = new List<IBlockComponent>
            {
                blockInventory,
                machineSave,
                effectComponent,
                processor,
                machineComponent,
                inventoryConnectorComponent,
                gearConnector,
                gearEnergyTransformer,
            };
            
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}