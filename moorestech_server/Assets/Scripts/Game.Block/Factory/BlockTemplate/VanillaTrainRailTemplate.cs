using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface.Extension;
using Game.Context;
using UnityEditor.Overlays;
using Core.Item.Interface;
using Newtonsoft.Json;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaTrainRailTemplate : IBlockTemplate
    {
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var transformer = new RailComponent(blockPositionInfo);
            var components = new List<IBlockComponent>
            {
                transformer,
            };

            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }

        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {

            var itemJsons = JsonConvert.DeserializeObject<List<ItemStackSaveJsonObject>>(componentStates[RailSaverComponent.SaveKey]);
            for (var i = 0; i < itemJsons.Count; i++)
            {
                var itemStack = itemJsons[i].ToItemStack();
                _itemDataStoreService.SetItem(i, itemStack);
            }



            var transformer = new RailComponent(blockPositionInfo);
            var components = new List<IBlockComponent>
            {
                transformer,
            };

            var blockPosition = blockPositionInfo.OriginalPos;
            // WorldBlockDatastoreを通じてその座標にあるブロックを取得する
            var block = ServerContext.WorldBlockDatastore.GetBlock(blockPosition);
            var railSaverComponent = block.ComponentManager.GetComponent<RailSaverComponent>();


            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}

/*
    public class VanillaSimpleGearGeneratorTemplate : IBlockTemplate
    {
        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return CreateGear(blockMasterElement, blockInstanceId, blockPositionInfo);
        }
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return CreateGear(blockMasterElement, blockInstanceId, blockPositionInfo);
        }
        
        private IBlock CreateGear(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var configParam = blockMasterElement.BlockParam as SimpleGearGeneratorBlockParam;
            var connectSetting = configParam.Gear.GearConnects;
            
            var blockComponent = new BlockConnectorComponent<IGearEnergyTransformer>(connectSetting, connectSetting, blockPositionInfo);
            var gearComponent = new SimpleGearGeneratorComponent(configParam, blockInstanceId, blockComponent);
            
            var components = new List<IBlockComponent>
            {
                gearComponent,
                blockComponent,
            };
            
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
 */