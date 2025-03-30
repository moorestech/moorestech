using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;
using Game.Block.Blocks.TrainRail;
using Game.Train.RailGraph;
using Newtonsoft.Json;
using Game.Context;
using Game.Block.Interface.Extension;


namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaTrainStationTemplate : IBlockTemplate
    {
        /*
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var transformer = new RailComponent(blockPositionInfo);
            var components = new List<IBlockComponent>
            {
                transformer,
            };

            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
        */
        /// <summary>
        /// 新規にブロック（と対応するRailComponent等）を生成
        /// </summary>
        public IBlock New(
            BlockMasterElement blockMasterElement,
            BlockInstanceId blockInstanceId,
            BlockPositionInfo blockPositionInfo)
        {
            // stationは常にRailComponentが2つ
            var railComponents = new RailComponent[2];
            var railSaver = new RailSaverComponent(railComponents);

            // RailComponentを生成
            for (int i = 0; i < railComponents.Length; i++)
            {
                var railComponentId = new RailComponentID(blockPositionInfo.OriginalPos, i);///////////////要修正 TODO
                railComponents[i] = new RailComponent(blockPositionInfo, railComponentId);
            }

            // コンポーネントをまとめてブロックに登録
            var components = new List<IBlockComponent>();
            components.Add(railSaver);
            components.AddRange(railComponents);
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }

        /*
        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var transformer = new RailComponent(blockPositionInfo);
            var components = new List<IBlockComponent>
            {
                transformer,
            };

            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }*/
        public IBlock Load(
        Dictionary<string, string> componentStates,
        BlockMasterElement blockMasterElement,
        BlockInstanceId blockInstanceId,
        BlockPositionInfo blockPositionInfo)
        {
            // RailComponent群を復元
            var railComponents = LoadRailComponents(componentStates, blockPositionInfo);

            // 生成したRailComponentをまとめるRailSaverComponentを作成
            var railSaver = new RailSaverComponent(railComponents);

            // まとめてBlockSystemに載せる
            var components = new List<IBlockComponent>();
            components.Add(railSaver);
            components.AddRange(railComponents);
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }


        /// <summary>
        /// RailSaverComponentに相当するJSON文字列をもとにRailComponent配列を復元する
        /// </summary>
        private RailComponent[] LoadRailComponents(
            Dictionary<string, string> componentStates,
            BlockPositionInfo blockPositionInfo)
        {
            // セーブデータ(JSON)を取得・復元
            string json = componentStates["RailSaverComponent"];
            var railSaverData = JsonConvert.DeserializeObject<RailSaverData>(json);

            int count = railSaverData.Values.Count;
            var railComponents = new RailComponent[count];

            // まずRailComponent自体を生成
            for (int i = 0; i < count; i++)
            {
                var info = railSaverData.Values[i];
                railComponents[i] = new RailComponent(blockPositionInfo, info.MyID);
                // ベジェ強度などを設定
                railComponents[i].ChangeBezierStrength(info.BezierStrength);
            }

            // 接続情報を復元（Front/Back）
            for (int i = 0; i < count; i++)
            {
                var info = railSaverData.Values[i];
                var railComponent = railComponents[i];

                // 自分のFrontNodeに接続する先を復元
                foreach (var connDest in info.ConnectMyFrontTo)
                {
                    TryConnect(railComponent, connDest, isFrontThis: true);
                }
                // 自分のBackNodeに接続する先を復元
                foreach (var connDest in info.ConnectMyBackTo)
                {
                    TryConnect(railComponent, connDest, isFrontThis: false);
                }
            }

            return railComponents;
        }

        /// <summary>
        /// 実際にRailComponentの接続を行うヘルパーメソッド
        /// </summary>
        private void TryConnect(RailComponent fromRail, ConnectionDestination connDest, bool isFrontThis)
        {
            var destinationRailComponentId = connDest.DestinationID;
            var isFrontTarget = connDest.IsFront;

            var position = destinationRailComponentId.Position;
            var idIndex = destinationRailComponentId.ID;

            // 実際のブロックをワールドから取得
            var block = ServerContext.WorldBlockDatastore.GetBlock(position);
            if (block == null) return;

            // そのブロックがRailSaverComponentを持っているか確認
            if (!block.TryGetComponent<RailSaverComponent>(out var railSaverComponent))
                return;

            // 配列からターゲットのRailComponentを取得
            if (idIndex < 0 || idIndex >= railSaverComponent.RailComponents.Length)
                return;

            var targetRail = railSaverComponent.RailComponents[idIndex];

            // 接続（既に接続済みなら距離が上書きされるだけ）
            fromRail.ConnectRailComponent(targetRail, isFrontThis, isFrontTarget);
        }



    }
}


/*
using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;
using Game.Block.Blocks.TrainRail;
using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.TrainRail;
using Game.Block.Component;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using Newtonsoft.Json;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaTrainStationTemplate : IBlockTemplate
    {
        private readonly BlockOpenableInventoryUpdateEvent _openableInventoryUpdateEvent;

        public VanillaTrainStationTemplate(BlockOpenableInventoryUpdateEvent openableInventoryUpdateEvent)
        {
            _openableInventoryUpdateEvent = openableInventoryUpdateEvent;
        }

        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            // 駅ブロックが持つインベントリスロット数などを Param から取得
            var param = blockMasterElement.BlockParam as TrainStationBlockParam;
            var stationLength = param.StationLength;
            var inventorySlotCount = param.ItemSlotCount;

            // インベントリ接続用コンポーネントを作成
            var connector = new BlockConnectorComponent<IBlockInventory>(
                param.InventoryConnectors.InputConnects,
                param.InventoryConnectors.OutputConnects,
                blockPositionInfo
            );

            // 駅コンポーネントを作成
            var stationComponent = new StationComponent(
                blockInstanceId,
                stationLength,
                "MyTrainStation",
                inventorySlotCount,
                _openableInventoryUpdateEvent
            );

            // コンポーネントをまとめて返す
            var components = new List<IBlockComponent>
            {
                stationComponent,
                connector // ベルトコンベア等と繋がる
            };
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }

        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement,
            BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var param = blockMasterElement.BlockParam as TrainStationBlockParam;
            var stationLength = param.StationLength;
            var inventorySlotCount = param.ItemSlotCount;

            // インベントリ接続用コンポーネント
            var connector = new BlockConnectorComponent<IBlockInventory>(
                param.InventoryConnectors.InputConnects,
                param.InventoryConnectors.OutputConnects,
                blockPositionInfo
            );

            // 駅コンポーネントを作成
            var stationComponent = new StationComponent(
                blockInstanceId,
                stationLength,
                "MyTrainStation",
                inventorySlotCount,
                _openableInventoryUpdateEvent
            );

            // セーブ文字列を読み込む (あれば)
            if (componentStates.TryGetValue(stationComponent.SaveKey, out var savedJson))
            {
                stationComponent.LoadFromJsonString(savedJson);
            }

            var components = new List<IBlockComponent>
            {
                stationComponent,
                connector
            };
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}

*/