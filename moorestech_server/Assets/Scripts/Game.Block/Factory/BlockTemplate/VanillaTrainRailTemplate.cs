using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;
using Game.Block.Blocks.TrainRail;
using Newtonsoft.Json;
using Game.Train.RailGraph;
using Game.Context;
using Game.Block.Interface.Extension;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaTrainRailTemplate : IBlockTemplate
    {
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            RailComponent[] railComponents = new RailComponent[1];//railブロックは必ず1つだけのrailcomponentを持つ
            var railSaver = new RailSaverComponent(railComponents);
            for (int i = 0; i < railComponents.Length; i++)
            {
                RailComponentID railComponentID = new RailComponentID(blockPositionInfo.OriginalPos, i);
                var railcomponent = new RailComponent(blockPositionInfo, railComponentID);
                railComponents[i] = railcomponent;
            }

            var components = new List<IBlockComponent>
            {
                railSaver
            };
            components.AddRange(railComponents);

            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }

        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var railComponents = LoadSaveData(componentStates, blockPositionInfo);
            var railSaver = new RailSaverComponent(railComponents);

            var components = new List<IBlockComponent>
            {
                railSaver
            };
            components.AddRange(railComponents);
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }


        public RailComponent[] LoadSaveData(Dictionary<string, string> componentStates,BlockPositionInfo blockPositionInfo)
        {
            string json = componentStates["RailSaverComponent"];
            var _saveData = JsonConvert.DeserializeObject<RailSaverData>(json);
            var count = _saveData.values.Count;
            //生成
            var railComponents = new RailComponent[count];
            for (int i = 0; i < count; i++)
            {
                var railcomponentinfo = _saveData.values[i];
                railComponents[i] = new RailComponent(blockPositionInfo, railcomponentinfo.myID);
            }
            //接続情報を復元
            for (int i = 0; i < count; i++)
            {
                var railcomponentinfo = _saveData.values[i];
                var railcomponent = railComponents[i];
                //FrontNode
                foreach (var cd in railcomponentinfo.connectMyFrontTo)
                {
                    var (nextRailComponentInfo, isFront) = cd.destination;
                    var position = nextRailComponentInfo.Position;
                    var id = nextRailComponentInfo.ID;
                    //World座標からBlockを取得、なければskip
                    var block = ServerContext.WorldBlockDatastore.GetBlock(position);
                    if (block == null) continue;
                    //あればRailSaverComponentを取得
                    if (!block.TryGetComponent<RailSaverComponent>(out var railSavercomponent)) continue;
                    var nextRailComponent = railSavercomponent.railComponents[id];
                    railcomponent.ConnectRailComponent(nextRailComponent, true, isFront);
                }
                //BackNode
                foreach (var cd in railcomponentinfo.connectMyBackTo)
                {
                    var (nextRailComponentInfo, isFront) = cd.destination;
                    var position = nextRailComponentInfo.Position;
                    var id = nextRailComponentInfo.ID;
                    //World座標からBlockを取得、なければskip
                    var block = ServerContext.WorldBlockDatastore.GetBlock(position);
                    if (block == null) continue;
                    //あればRailSaverComponentを取得
                    if (!block.TryGetComponent<RailSaverComponent>(out var railSavercomponent)) continue;
                    var nextRailComponent = railSavercomponent.railComponents[id];
                    railcomponent.ConnectRailComponent(nextRailComponent, false, isFront);
                }
            }
            return railComponents;
        }



    }
}
