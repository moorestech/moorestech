using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Train.RailGraph;
using System.Diagnostics;
using System.Numerics;
using UnityEngine;

namespace Game.Block.Factory.BlockTemplate.Utility
{
    public static class RailComponentFactory
    {
        /// <summary>
        /// 指定数のRailComponentを作成し、必要に応じて自動的に接続します。
        /// 今のところstation,cargoなど1つのブロックに2つのRailComponentを持つものだけを想定しています。
        /// </summary>
        public static RailComponent[] Create2RailComponents(BlockPositionInfo positionInfo, UnityEngine.Vector3 entryPosition, UnityEngine.Vector3 exitPosition)
        {
            var positions = RailComponentUtility.CalculateRailComponentPositions(positionInfo, entryPosition, exitPosition);
            var components = new RailComponent[2];
            for (int i = 0; i < 2; i++)
            {
                var componentId = new RailComponentID(positionInfo.OriginalPos, i);
                components[i] = new RailComponent(positions[i], positionInfo.BlockDirection , componentId);
            }
            // stationの前と後ろにそれぞれrailComponentがある、自動で接続する
            components[0].ConnectRailComponent(components[1], true, true);

            // 駅ブロック隣接時に自動接続
            // もしpositions[0]がRailPositionToConnectionDestinationにみつかってかつpairのうちどちらか1個が存在するならそこに接続、0個なら新規登録、2個の場合は考えない
            while (true) 
            {
                if (RailGraphDatastore.RailPositionToConnectionDestination.TryGetValue(positions[0], out var pair))
                {
                    var item1 = pair.Item1;
                    var item2 = pair.Item2;
                    if ((item1 != null) & (item2 != null)) 
                    {
                        UnityEngine.Debug.Assert(false, "RailComponentFactory.Create2RailComponents: Found multiple connection destinations for a single rail position.");
                        break;
                    }
                        
                    if ((pair.Item1 == null) & (pair.Item2 == null)) 
                    {
                        //RailGraphDatastore.RailPositionToConnectionDestinationのpositions[0]キーを削除
                        RailGraphDatastore.RailPositionToConnectionDestination.Remove(positions[0]);
                        continue;
                    }

                    var destinationConnection = (pair.Item1 != null) ? pair.Item1 : pair.Item2;
                    var useFrontSideOfTarget = destinationConnection.IsFront;
                    var targetComponent = DestinationConnectionToRailComponent(destinationConnection);
                    if (targetComponent == null) break;
                    //相手から自分に接続を考える(どっちでもいいが)
                    targetComponent.ConnectRailComponent(components[0], useFrontSideOfTarget, true);

                    var newdata = new ConnectionDestination(components[0].ComponentID, false);
                    if (pair.Item1 == null)
                    {
                        pair.Item1 = newdata;
                    }else{
                        pair.Item2 = newdata;
                    }
                    RailGraphDatastore.RailPositionToConnectionDestination[positions[0]] = pair;
                    break;
                }
                else
                {
                    // 新規登録
                    var pair= new System.Tuple<ConnectionDestination, ConnectionDestination>(null, null);
                    RailGraphDatastore.RailPositionToConnectionDestination[positions[0]] = new System.Tuple<ConnectionDestination, ConnectionDestination>(new ConnectionDestination(components[0], true), null);
                    break;
                }
            }




            return components;
        }



        // DestinationConnectionからRailComponentを復元する、ワールドブロックデータを使うversion
        static private RailComponent DestinationConnectionToRailComponent(ConnectionDestination destinationConnection)
        {
            var destinationComponentId = destinationConnection.DestinationID;

            var destinationPosition = destinationComponentId.Position;
            var componentIndex = destinationComponentId.ID;

            // 対象ブロックをワールドから取得
            var targetBlock = ServerContext.WorldBlockDatastore.GetBlock(destinationPosition);
            if (targetBlock == null) return null;

            // 対象ブロックがRailSaverComponentを持っているか確認
            if (!targetBlock.TryGetComponent<RailSaverComponent>(out var targetRailSaver))
                return null;

            // RailComponents配列から対象のRailComponentを取得
            if (componentIndex < 0 || componentIndex >= targetRailSaver.RailComponents.Length)
                return null;

            var targetComponent = targetRailSaver.RailComponents[componentIndex];
            return targetComponent;
        }



        /// <summary>
        /// RailSaverComponentを生成します。
        /// </summary>
        public static RailSaverComponent CreateRailSaverComponent(RailComponent[] railComponents)
        {
            return new RailSaverComponent(railComponents);
        }
    }
}
