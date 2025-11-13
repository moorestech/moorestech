using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Train.RailGraph;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Block.Factory.BlockTemplate.Utility
{
    public static class RailComponentUtility
    {
        //駅のように2つのRailComponentを持つブロックの接続情報復元処理
        static public RailComponent[] Restore2RailComponents(Dictionary<string, string> componentStates, BlockPositionInfo blockPositionInfo, Vector3 entryPosition, Vector3 exitPosition)
        {
            string railSaverJson = componentStates[typeof(RailSaverComponent).FullName];
            var saverData = JsonConvert.DeserializeObject<RailSaverData>(railSaverJson);
            int count = saverData.Values.Count;
            //2!=countならエラー
            if (count != 2)
            {
                Debug.LogError($"駅復元処理エラー。RailComponentUtility.Restore2RailComponents: Expected 2 RailComponents, but got {count}.");
                return Array.Empty<RailComponent>();
            }

            /*
            // JSON形式の保存データを取得・復元
            string railSaverJson = componentStates[typeof(RailSaverComponent).FullName];
            var saverData = JsonConvert.DeserializeObject<RailSaverData>(railSaverJson);

            int count = saverData.Values.Count;
            //2!=countならエラー
            if (count != 2)
            {
                Debug.LogError($"駅復元処理エラー。RailComponentUtility.Restore2RailComponents: Expected 2 RailComponents, but got {count}.");
                return Array.Empty<RailComponent>();
            }

            var railComponents = new RailComponent[count];
            var railComponentPositions = new Vector3[2];
            railComponentPositions[0] = CalculateRailComponentPosition(positionInfo, entryPosition);
            railComponentPositions[1] = CalculateRailComponentPosition(positionInfo, exitPosition);

            // 各RailComponentを生成
            for (int i = 0; i < count; i++)
            {
                var componentInfo = saverData.Values[i];
                railComponents[i] = new RailComponent(railComponentPositions[i], componentInfo.RailDirection.Vector3, componentInfo.MyID);
                // ベジェ曲線の強度を設定
                railComponents[i].UpdateControlPointStrength(componentInfo.BezierStrength);
            }

            // 接続情報の復元 (Front/Back)
            for (int i = 0; i < count; i++)
            {
                var componentInfo = saverData.Values[i];
                var currentComponent = railComponents[i];

                // FrontNodeへの接続情報を復元
                foreach (var destinationConnection in componentInfo.ConnectMyFrontTo)
                {
                    EstablishConnection(currentComponent, destinationConnection, true);
                }
                // BackNodeへの接続情報を復元
                foreach (var destinationConnection in componentInfo.ConnectMyBackTo)
                {
                    EstablishConnection(currentComponent, destinationConnection, false);
                }
            }*/

            var railComponentPositions = new Vector3[2];
            railComponentPositions[0] = entryPosition;
            railComponentPositions[1] = exitPosition;
            var railComponents = RestoreMain(componentStates, blockPositionInfo, railComponentPositions);
            // 自分の駅内の接続情報も復元、距離は自動計算（もともとセーブに距離情報はない）
            railComponents[0].ConnectRailComponent(railComponents[1], true, true);
            return railComponents;
        }

        //駅以外、事実上橋脚ブロックの接続情報復元処理
        static public RailComponent[] Restore1RailComponents(Dictionary<string, string> componentStates, BlockPositionInfo blockPositionInfo, Vector3 componentPosition)
        {
            string railSaverJson = componentStates[typeof(RailSaverComponent).FullName];
            var saverData = JsonConvert.DeserializeObject<RailSaverData>(railSaverJson);
            int count = saverData.Values.Count;
            //2!=countならエラー
            if (count != 1)
            {
                Debug.LogError($"橋脚復元処理エラー。RailComponentUtility.Restore1RailComponents: Expected 1 RailComponents, but got {count}.");
                return Array.Empty<RailComponent>();
            }

            /*
            // JSON形式の保存データを取得・復元
            string railSaverJson = componentStates[typeof(RailSaverComponent).FullName];
            var saverData = JsonConvert.DeserializeObject<RailSaverData>(railSaverJson);

            int count = saverData.Values.Count;
            var railComponents = new RailComponent[count];
            var railComponentPositions = new Vector3[1];
            railComponentPositions[0] = CalculateRailComponentPosition(positionInfo, componentPosition);

            // 各RailComponentを生成
            for (int i = 0; i < count; i++)
            {
                var componentInfo = saverData.Values[i];
                railComponents[i] = new RailComponent(railComponentPositions[i], componentInfo.RailDirection.Vector3, componentInfo.MyID);
                // ベジェ曲線の強度を設定
                railComponents[i].UpdateControlPointStrength(componentInfo.BezierStrength);
            }

            // 接続情報の復元 (Front/Back)
            for (int i = 0; i < count; i++)
            {
                var componentInfo = saverData.Values[i];
                var currentComponent = railComponents[i];

                // FrontNodeへの接続情報を復元
                foreach (var destinationConnection in componentInfo.ConnectMyFrontTo)
                {
                    EstablishConnection(currentComponent, destinationConnection, true);
                }
                // BackNodeへの接続情報を復元
                foreach (var destinationConnection in componentInfo.ConnectMyBackTo)
                {
                    EstablishConnection(currentComponent, destinationConnection, false);
                }
            }
            */

            var railComponentPositions = new Vector3[1];
            railComponentPositions[0] = componentPosition;
            var railComponents = RestoreMain(componentStates, blockPositionInfo, railComponentPositions);
            return railComponents;

        }

        static private RailComponent[] RestoreMain(Dictionary<string, string> componentStates, BlockPositionInfo positionInfo, Vector3[] railComponentPositions)
        {
            // JSON形式の保存データを取得・復元
            string railSaverJson = componentStates[typeof(RailSaverComponent).FullName];
            var saverData = JsonConvert.DeserializeObject<RailSaverData>(railSaverJson);

            int count = saverData.Values.Count;
            var railComponents = new RailComponent[count];
            for (int i = 0; i < railComponentPositions.Length; i++)
            {
                railComponentPositions[i] = CalculateRailComponentPosition(positionInfo, railComponentPositions[i]);
            }

            // 各RailComponentを生成
            for (int i = 0; i < count; i++)
            {
                var componentInfo = saverData.Values[i];
                railComponents[i] = new RailComponent(railComponentPositions[i], componentInfo.RailDirection.Vector3, componentInfo.MyID);
                // ベジェ曲線の強度を設定
                railComponents[i].UpdateControlPointStrength(componentInfo.BezierStrength);
            }

            // 接続情報の復元 (Front/Back)
            for (int i = 0; i < count; i++)
            {
                var componentInfo = saverData.Values[i];
                var currentComponent = railComponents[i];

                // FrontNodeへの接続情報を復元
                foreach (var destinationConnection in componentInfo.ConnectMyFrontTo)
                {
                    EstablishConnection(currentComponent, destinationConnection, true);
                }
                // BackNodeへの接続情報を復元
                foreach (var destinationConnection in componentInfo.ConnectMyBackTo)
                {
                    EstablishConnection(currentComponent, destinationConnection, false);
                }
            }
            return railComponents;
        }


        static public Vector3 CalculateRailComponentPosition(BlockPositionInfo positionInfo, Vector3 componentPosition)
        {
            Vector3 CoordinateConvert(BlockDirection blockDirection,Vector3 pos)
            {
                var rotation = blockDirection.GetRotation();
                var rotationMatrix = Matrix4x4.Rotate(rotation);
                // 行列は float4 × float4 の形なので pos を拡張して計算
                var transformed = rotationMatrix.MultiplyPoint3x4(pos);
                return transformed;
            }
            var blockDirection = positionInfo.BlockDirection;
            Vector3 baseOriginPosition = blockDirection.GetBlockBaseOriginPos(positionInfo);
            return CoordinateConvert(blockDirection, componentPosition) + baseOriginPosition;
        }

        // 自分の駅or貨物駅ブロック内のRailComponentから、別ブロックのRailComponentへの接続を確立する
        // 自分から自分への接続はWorldBlockDatastore.GetBlockが失敗するため、ここでは扱わない
        static public void EstablishConnection(RailComponent sourceComponent, ConnectionDestination destinationConnection, bool isFrontSideOfComponent)
        {
            var destinationComponentId = destinationConnection.railComponentID;
            var useFrontSideOfTarget = destinationConnection.IsFront;

            var destinationPosition = destinationComponentId.Position;
            var componentIndex = destinationComponentId.ID;

            // 対象ブロックをワールドから取得
            var targetBlock = ServerContext.WorldBlockDatastore.GetBlock(destinationPosition);
            if (targetBlock == null) return;

            // 対象ブロックがRailSaverComponentを持っているか確認
            if (!targetBlock.TryGetComponent<RailSaverComponent>(out var targetRailSaver))
                return;

            // RailComponents配列から対象のRailComponentを取得
            if (componentIndex < 0 || componentIndex >= targetRailSaver.RailComponents.Length)
                return;

            var targetComponent = targetRailSaver.RailComponents[componentIndex];

            // 接続を実施 (既に接続済みの場合、距離が上書きされるだけ)
            sourceComponent.ConnectRailComponent(targetComponent, isFrontSideOfComponent, useFrontSideOfTarget);
        }

































        /// <summary>
        /// 指定数のRailComponentを作成し、必要に応じて自動的に接続します。
        /// 今のところstation,cargoなど1つのブロックに2つのRailComponentを持つものだけを想定しています。
        /// </summary>
        public static RailComponent[] Create2RailComponents(BlockPositionInfo positionInfo, UnityEngine.Vector3 entryPosition, UnityEngine.Vector3 exitPosition)
        {
            var positions = new UnityEngine.Vector3[2];
            positions[0] = RailComponentUtility.CalculateRailComponentPosition(positionInfo, entryPosition);
            positions[1] = RailComponentUtility.CalculateRailComponentPosition(positionInfo, exitPosition);
            var components = new RailComponent[2];
            for (int i = 0; i < 2; i++)
            {
                var componentId = new RailComponentID(positionInfo.OriginalPos, i);
                components[i] = new RailComponent(positions[i], positionInfo.BlockDirection, componentId);
            }
            // stationの前と後ろにそれぞれrailComponentがある、自動で接続する
            components[0].ConnectRailComponent(components[1], true, true);

            // 駅ブロック隣接時に自動接続
            // もしpositions[0]がRailPositionToConnectionDestinationにみつかってかつpairのうちどちらか1個が存在するならそこに接続し残りを埋める、0個なら新規登録、2個の場合は考えない
            while (true)
            {
                if (RailGraphDatastore.RailPositionToConnectionDestination.TryGetValue(positions[0], out var pair))
                {
                    if ((pair.Item1 != null) & (pair.Item2 != null))
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
                    var targetComponent = ConnectionDestinationToRailComponent(destinationConnection);
                    if (targetComponent == null) break;
                    //相手から自分に接続を考える(どっちでもいいが)
                    targetComponent.ConnectRailComponent(components[0], useFrontSideOfTarget, true);

                    var newdata = new ConnectionDestination(components[0].ComponentID, false);
                    if (pair.Item1 == null)
                    {
                        pair.Item1 = newdata;
                    }
                    else
                    {
                        pair.Item2 = newdata;
                    }
                    RailGraphDatastore.RailPositionToConnectionDestination[positions[0]] = pair;
                    break;
                }
                else
                {
                    // 新規登録
                    var newdata = new ConnectionDestination(components[0].ComponentID, false);
                    var newpair = (newdata, ConnectionDestination.Default);
                    RailGraphDatastore.RailPositionToConnectionDestination[positions[0]] = newpair;
                    break;
                }
            }

            // もしpositions[1]がRailPositionToConnectionDestinationにみつかってかつpairのうちどちらか1個が存在するならそこに接続し残りを埋める、0個なら新規登録、2個の場合は考えない
            while (true)
            {
                if (RailGraphDatastore.RailPositionToConnectionDestination.TryGetValue(positions[1], out var pair))
                {
                    if ((pair.Item1 != null) & (pair.Item2 != null))
                    {
                        UnityEngine.Debug.Assert(false, "RailComponentFactory.Create2RailComponents: Found multiple connection destinations for a single rail position.");
                        break;
                    }

                    if ((pair.Item1 == null) & (pair.Item2 == null))
                    {
                        //RailGraphDatastore.RailPositionToConnectionDestinationのpositions[1]キーを削除
                        RailGraphDatastore.RailPositionToConnectionDestination.Remove(positions[1]);
                        continue;
                    }

                    var destinationConnection = (pair.Item1 != null) ? pair.Item1 : pair.Item2;
                    var useFrontSideOfTarget = destinationConnection.IsFront;
                    var targetComponent = ConnectionDestinationToRailComponent(destinationConnection);
                    if (targetComponent == null) break;
                    //相手から自分に接続を考える(どっちでもいいが)
                    targetComponent.ConnectRailComponent(components[1], useFrontSideOfTarget, false);

                    var newdata = new ConnectionDestination(components[1].ComponentID, true);
                    if (pair.Item1 == null)
                    {
                        pair.Item1 = newdata;
                    }
                    else
                    {
                        pair.Item2 = newdata;
                    }
                    RailGraphDatastore.RailPositionToConnectionDestination[positions[1]] = pair;
                    break;
                }
                else
                {
                    // 新規登録
                    var newdata = new ConnectionDestination(components[1].ComponentID, true);
                    var newpair = (newdata, ConnectionDestination.Default);
                    RailGraphDatastore.RailPositionToConnectionDestination[positions[1]] = newpair;
                    break;
                }
            }

            return components;
        }


        // DestinationConnectionからRailComponentを復元する、ワールドブロックデータを使うversion
        static private RailComponent ConnectionDestinationToRailComponent(ConnectionDestination destinationConnection)
        {
            var destinationComponentId = destinationConnection.railComponentID;

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












    }
}