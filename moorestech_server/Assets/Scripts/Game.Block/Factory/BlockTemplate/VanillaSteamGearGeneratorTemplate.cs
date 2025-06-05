using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.Fluid;
using Game.Block.Blocks.Gear;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Component;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaSteamGearGeneratorTemplate : IBlockTemplate
    {
        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdateEvent;
        
        public VanillaSteamGearGeneratorTemplate(BlockOpenableInventoryUpdateEvent blockInventoryUpdateEvent)
        {
            _blockInventoryUpdateEvent = blockInventoryUpdateEvent;
        }
        
        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return CreateSteamGearGenerator(blockMasterElement, blockInstanceId, blockPositionInfo);
        }
        
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return CreateSteamGearGenerator(blockMasterElement, blockInstanceId, blockPositionInfo);
        }
        
        private IBlock CreateSteamGearGenerator(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var configParam = blockMasterElement.BlockParam as SteamGearGeneratorBlockParam;
            
            // ギア接続の設定
            var gearConnectSetting = configParam.Gear.GearConnects;
            var gearConnectorComponent = new BlockConnectorComponent<IGearEnergyTransformer>(gearConnectSetting, gearConnectSetting, blockPositionInfo);
            
            // 流体接続の設定
            var fluidConnector = IFluidInventory.CreateFluidInventoryConnector(configParam.FluidInventoryConnectors, blockPositionInfo);
            
            // ブロックIDを取得
            var blockId = Core.Master.MasterHolder.BlockMaster.GetBlockId(blockMasterElement.BlockGuid);
            
            // 入力インベントリの作成（流体タンクのみ、アイテムスロットなし）
            var inputInventory = new VanillaMachineInputInventory(
                blockId,
                0, // アイテムスロットなし
                1, // 蒸気用タンク1つ
                configParam.FluidCapacity, 
                _blockInventoryUpdateEvent, 
                blockInstanceId
            );
            
            // 出力インベントリの作成（出力なし）
            var outputInventory = new VanillaMachineOutputInventory(
                0, // アイテムスロットなし
                0, // 出力タンクなし
                0, // タンク容量（使用しない）
                Game.Context.ServerContext.ItemStackFactory,
                _blockInventoryUpdateEvent,
                blockInstanceId,
                0, // 入力スロットサイズ
                new BlockConnectorComponent<IBlockInventory>(null, null, blockPositionInfo) // ダミーのコネクタ
            );
            
            // 流体インベントリコンポーネント
            var fluidInventory = new VanillaMachineFluidInventoryComponent(
                inputInventory,
                outputInventory,
                fluidConnector
            );
            
            // スチームギアジェネレータコンポーネント
            var steamGearGeneratorComponent = new SteamGearGeneratorComponent(
                configParam, 
                blockInstanceId, 
                gearConnectorComponent,
                inputInventory
            );
            
            var components = new List<IBlockComponent>
            {
                steamGearGeneratorComponent,
                gearConnectorComponent,
                fluidConnector,
                fluidInventory,
            };
            
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}