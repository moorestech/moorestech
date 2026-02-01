using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Train.RailGraph;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Game.Train.SaveLoad;
using UnityEngine;

namespace Game.Block.Factory.BlockTemplate.Utility
{
    public static class RailComponentUtility
    {
        /// <summary>
        /// 復元メイン
        /// </summary>
        //駅のように2つのRailComponentを持つブロックの接続情報復元処理
        static public RailComponent[] Restore2RailComponents(Dictionary<string, string> componentStates, BlockPositionInfo blockPositionInfo, Vector3 entryPosition, Vector3 exitPosition, IRailGraphDatastore railGraphDatastore)
        {
            //ここもそのうちまとめたい、またはけすか　TODO
            string railSaverJson = componentStates[typeof(RailSaverComponent).FullName];
            var saverData = JsonConvert.DeserializeObject<RailSaverData>(railSaverJson);
            int count = saverData.Values.Count;
            //2!=countならエラー
            if (count != 2)
            {
                Debug.LogError($"駅復元処理エラー。RailComponentUtility.Restore2RailComponents: Expected 2 RailComponents, but got {count}.");
                return Array.Empty<RailComponent>();
            }

            var railComponentPositions = new Vector3[2];
            railComponentPositions[0] = entryPosition;
            railComponentPositions[1] = exitPosition;
            var railComponents = RestoreMain(componentStates, blockPositionInfo, railComponentPositions, railGraphDatastore);//ここで②が復元できているはず(または隣接ブロックがまだか)
            // ①復元
            return railComponents;
        }

        //駅以外、事実上橋脚ブロックの接続情報復元処理
        static public RailComponent[] Restore1RailComponents(Dictionary<string, string> componentStates, BlockPositionInfo blockPositionInfo, Vector3 componentPosition, IRailGraphDatastore railGraphDatastore)
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

            var railComponentPositions = new Vector3[1];
            railComponentPositions[0] = componentPosition;
            var railComponents = RestoreMain(componentStates, blockPositionInfo, railComponentPositions, railGraphDatastore);
            return railComponents;
        }

        static private RailComponent[] RestoreMain(Dictionary<string, string> componentStates, BlockPositionInfo positionInfo, Vector3[] railComponentPositions, IRailGraphDatastore railGraphDatastore)
        {
            // JSON形式の保存データを取得・復元
            string railSaverJson = componentStates[typeof(RailSaverComponent).FullName];
            var saverData = JsonConvert.DeserializeObject<RailSaverData>(railSaverJson);
            int count = saverData.Values.Count;
            var railComponents = new RailComponent[count];
            //count!=railComponentPositions.Lengthならエラー
            if (count != railComponentPositions.Length) 
            {
                Debug.LogError($"レール復元時、RailComponent数が想定通りでない_saverData.Values.Count={count}_railComponentPositions.Length={railComponentPositions.Length}");
                return railComponents;
            }

            for (int i = 0; i < railComponentPositions.Length; i++)
                railComponentPositions[i] = CalculateRailComponentPosition(positionInfo, railComponentPositions[i]);

            for (int i = 0; i < count; i++)// 各RailComponentを生成
            {
                var componentInfo = saverData.Values[i];
                railComponents[i] = new RailComponent(railGraphDatastore, railComponentPositions[i], componentInfo.RailDirection.Vector3, componentInfo.MyID);
                railComponents[i].UpdateControlPointStrength(componentInfo.BezierStrength);// ベジェ曲線の強度を設定
            }

            return railComponents;
        }

        //回転計算による浮動小数点数誤差に注意。railcomponent.positionはVector3形式であるが、現時点でこの値自体の誤差は許容している。もしrailcomponent.positionを新規に使う場合すでに誤差が含まれていることを考慮すること
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
            var baseOriginPosition = (Vector3)blockDirection.GetBlockModelOriginPos(positionInfo.OriginalPos, positionInfo.BlockSize);
            return CoordinateConvert(blockDirection, componentPosition) + baseOriginPosition;
        }

        /// <summary>
        /// 復元とnewでのconnectionMap関連処理
        /// </summary>

        // 指定数のRailComponentを作成し、必要に応じて自動的に接続します。
        // 今のところstation,cargoなど1つのブロックに2つのRailComponentを持つものだけを想定しています。
        public static RailComponent[] Create2RailComponents(BlockPositionInfo positionInfo, Vector3 entryPosition, Vector3 exitPosition, IRailGraphDatastore railGraphDatastore)
        {
            var positions = new Vector3[2];
            positions[0] = CalculateRailComponentPosition(positionInfo, entryPosition);
            positions[1] = CalculateRailComponentPosition(positionInfo, exitPosition);
            var components = new RailComponent[2];
            for (int i = 0; i < 2; i++)
            {
                var componentId = new RailComponentID(positionInfo.OriginalPos, i);
                components[i] = new RailComponent(railGraphDatastore, positions[i], positionInfo.BlockDirection, componentId);
            }
            // stationの前と後ろにそれぞれrailComponentがある、自動で接続する
            components[0].ConnectRailComponent(components[1], true, true);
            return components;
        }

        // 駅ブロック設置時、他の駅ブロックと隣接時にrailcomponentを自動接続する処理。あとconnectionMapへ登録する
        // When placing a station block, auto-connect rail components to adjacent stations and register to the connection map
        // 接続判定について詳細
        // Details about the connection judgement
        // 旧：ブロック座標を1マスずつスライドして同じ駅種のブロックにヒットするまでみていた(north,eastはブロックサイズ分シフトすればよかったがsouth,westは他人ブロックサイズが不明なので1マスずつスライドする必要があった)
        // Old: slide block coordinates one tile at a time until hitting the same station type (north/east could shift by block size, south/west required step-by-step because other block size was unknown)
        // 新：マスタ定義のentryPositionとexitPositionを有効活用する方針とした
        // New: use master-defined entryPosition and exitPosition effectively
        // たとえば駅1のentryPositionを駅2のexitPosition座標が一致していれば隣接接続してほしい
        // For example, if station1 entryPosition matches station2 exitPosition, they should connect as adjacent
        // これをRailGraphDataStoreのconnectionMapに辞書登録
        // Register this mapping into RailGraphDataStore connectionMap
        // 座標とConnectionDestinationを対応登録。ConnectionDestinationは事実上、駅のRailComponentのentry exitのみならずfront backの情報ももつ
        // Register coordinate to ConnectionDestination mapping; ConnectionDestination carries entry/exit as well as front/back info
        // このConnectionDestinationから接続させたい2つのRailComponentと向きが求まる
        // From ConnectionDestination, determine the two RailComponents to connect and their orientation
        // なお座標はVector3だと浮動小数点数誤差(回転計算)で一致しなくなることがあったので2倍&四捨五入でVector3Int化した
        // Vector3 positions may not match due to rotation precision, so multiply by 2 and round to Vector3Int
        // 座標→ConnectionDestinationの対応関係については、2つのConnectionDestinationをもつようにDictionary<Vector3Int, (ConnectionDestination first, ConnectionDestination second)> としている。3つ以上の重なりは考慮しない
        // For coordinate->ConnectionDestination mapping, use Dictionary<Vector3Int, (ConnectionDestination first, ConnectionDestination second)> and ignore overlaps of 3 or more
        // 駅のRailComponentをconnectionMapへ登録し、必要な場合のみ隣接接続を行う
        // Register station RailComponents into the connection map and optionally connect neighbors
        static public void RegisterAndConnetStationBlocks(RailComponent[] components, IRailGraphDatastore railGraphDatastore) // componentsが2つ限定ver
        {
            RegisterStationBlocksInternal(components, railGraphDatastore, true);
        }

        static public void RegisterStationBlocks(RailComponent[] components, IRailGraphDatastore railGraphDatastore) // componentsが2つ限定ver
        {
            RegisterStationBlocksInternal(components, railGraphDatastore, false);
        }

        static private void RegisterStationBlocksInternal(RailComponent[] components, IRailGraphDatastore railGraphDatastore, bool connectNeighbors)
        {
            var connectionMap = railGraphDatastore.GetRailPositionToConnectionDestination();
            var roundedPosition0 = RoundRailPosition(components[0].Position);
            var roundedPosition1 = RoundRailPosition(components[1].Position);
            RegisterStationBlock(components, connectionMap, roundedPosition0, 0, connectNeighbors);
            RegisterStationBlock(components, connectionMap, roundedPosition1, 1, connectNeighbors);
        }

        static private Vector3Int RoundRailPosition(Vector3 position)
        {
            var roundedPosition = new Vector3Int();
            roundedPosition.x = Mathf.RoundToInt(position.x * 2f);
            roundedPosition.y = Mathf.RoundToInt(position.y * 2f);
            roundedPosition.z = Mathf.RoundToInt(position.z * 2f);
            return roundedPosition;
        }

        static private void RegisterStationBlock(RailComponent[] components, Dictionary<Vector3Int, (ConnectionDestination first, ConnectionDestination second)> connectionMap, Vector3Int roundedPosition, int componentIndex, bool connectNeighbors)
        {
            while (true)
            {
                if (connectionMap.TryGetValue(roundedPosition, out var pair))
                {
                    if ((!pair.first.IsDefault()) && (!pair.second.IsDefault()))
                    {
                        Debug.Assert(false, "RailComponentFactory.RegisterStationBlocksInternal: Found multiple connection destinations for a single rail position.");
                        break;
                    }
                    if ((pair.first.IsDefault()) && (pair.second.IsDefault()))
                    {
                        connectionMap.Remove(roundedPosition);
                        continue;
                    }

                    if (connectNeighbors)
                    {
                        var destinationConnection = (!pair.first.IsDefault()) ? pair.first : pair.second;
                        var useFrontSideOfTarget = destinationConnection.IsFront;
                        var targetComponent = ConnectionDestinationToRailComponent(destinationConnection);
                        if (targetComponent == null) break;
                        var isFrontOfSelf = componentIndex == 0;
                        targetComponent.ConnectRailComponent(components[componentIndex], useFrontSideOfTarget, isFrontOfSelf);
                    }

                    var isFront = componentIndex == 1;
                    var newdata = new ConnectionDestination(components[componentIndex].ComponentID, isFront);
                    if (pair.first.IsDefault())
                    {
                        pair.first = newdata;
                    }
                    else
                    {
                        pair.second = newdata;
                    }
                    connectionMap[roundedPosition] = pair;
                    break;
                }
                else
                {
                    var isFront = componentIndex == 1;
                    var newdata = new ConnectionDestination(components[componentIndex].ComponentID, isFront);
                    var newpair = (newdata, ConnectionDestination.Default);
                    connectionMap[roundedPosition] = newpair;
                    break;
                }
            }
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


