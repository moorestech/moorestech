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
using UnityEngine;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaTrainStationTemplate : IBlockTemplate
    {
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
            var railComponentVec3pos = CalcRailComponentVec3pos(blockPositionInfo);
            // RailComponentを生成。場所の計算は↑
            for (int i = 0; i < railComponents.Length; i++)
            {
                var railComponentId = new RailComponentID(blockPositionInfo.OriginalPos, i);
                railComponents[i] = new RailComponent(railComponentVec3pos[i], blockPositionInfo.BlockDirection, railComponentId);
            }

            // コンポーネントをまとめてブロックに登録
            var components = new List<IBlockComponent>();
            components.Add(railSaver);
            components.AddRange(railComponents);
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }

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
        /// TODO? VanillaTrainRailTemplateと同じなので共通化したい
        /// </summary>
        private RailComponent[] LoadRailComponents(
            Dictionary<string, string> componentStates,
            BlockPositionInfo blockPositionInfo)
        {
            // セーブデータ(JSON)を取得・復元
            string json = componentStates[typeof(RailSaverComponent).FullName];
            var railSaverData = JsonConvert.DeserializeObject<RailSaverData>(json);

            int count = railSaverData.Values.Count;
            var railComponents = new RailComponent[count];
            var railComponentVec3pos = CalcRailComponentVec3pos(blockPositionInfo);

            // まずRailComponent自体を生成
            for (int i = 0; i < count; i++)
            {
                var info = railSaverData.Values[i];
                railComponents[i] = new RailComponent(railComponentVec3pos[i], blockPositionInfo.BlockDirection, info.MyID);
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
        /// TODO? VanillaTrainRailTemplateと同じなので共通化したい
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

        private Vector3[] CalcRailComponentVec3pos(BlockPositionInfo blockPositionInfo) 
        {
            var blockDirection = blockPositionInfo.BlockDirection;
            Vector3 blockBaseOriginPos = blockDirection.GetBlockBaseOriginPos(blockPositionInfo);
            var blockPosConvertAction = blockDirection.GetCoordinateConvertAction();
            Vector3Int blocksize = blockPositionInfo.BlockSize;
            Vector3 point0 = blockPosConvertAction(new Vector3Int(0, 0, 0));
            Vector3 point1 = blockPosConvertAction(new Vector3Int(0, 0, blocksize.z));
            Vector3 point2 = blockPosConvertAction(new Vector3Int(blocksize.x, 0, 0));
            Vector3 point3 = blockPosConvertAction(new Vector3Int(blocksize.x, 0, blocksize.z));
            Vector3[] railComponentVec3pos = new Vector3[2];
            railComponentVec3pos[0] = (point0 + point1) * 0.5f + blockBaseOriginPos;
            railComponentVec3pos[1] = (point2 + point3) * 0.5f + blockBaseOriginPos;
            Debug.Log($"railComponentVec3pos[0]: {railComponentVec3pos[0]}");
            Debug.Log($"railComponentVec3pos[1]: {railComponentVec3pos[1]}");
            return railComponentVec3pos;
        }

    }
}

