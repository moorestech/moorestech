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
            railComponents[0].ConnectRailComponent(railComponents[1], true, true);//
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

        // 自分の駅or貨物駅ブロック内のRailComponentから、別ブロックのRailComponentへの接続を確立する
        // 自分から自分への接続はWorldBlockDatastore.GetBlockが失敗するため、ここでは扱わない
        static public void EstablishConnection(RailComponent sourceComponent, ConnectionDestination destinationConnection, bool isFrontSideOfComponent)
        {
            var targetComponent = ConnectionDestinationToRailComponent(destinationConnection);
            if (targetComponent == null) return;
            var useFrontSideOfTarget = destinationConnection.IsFront;
            // 接続を実施 (既に接続済みの場合、距離が上書きされるだけ)
            sourceComponent.ConnectRailComponent(targetComponent, isFrontSideOfComponent, useFrontSideOfTarget);
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
        // 接続判定について詳細
        // 旧：ブロック座標を1マスずつスライドして同じ駅種のブロックにヒットするまでみていた(north,eastはブロックサイズ分シフトすればよかったがsouth,westは他人ブロックサイズが不明なので1マスずつスライドする必要があった)
        // 新：マスタ定義のentryPositionとexitPositionを有効活用する方針とした
        // たとえば駅1のentryPositionを駅2のexitPosition座標が一致していれば隣接接続してほしい
        // これをRailGraphDataStoreのconnectionMapに辞書登録
        // 座標とConnectionDestinationを対応登録。ConnectionDestinationは事実上、駅のRailComponentのentry exitのみならずfront backの情報ももつ
        // このConnectionDestinationから接続させたい2つのRailComponentと向きが求まる
        // なお座標はVector3だと浮動小数点数誤差(回転計算)で一致しなくなることがあったので2倍&四捨五入でVector3Int化した
        // 座標→ConnectionDestinationの対応関係については、2つのConnectionDestinationをもつようにDictionary<Vector3, (ConnectionDestination first, ConnectionDestination second)> としている。3つ以上の重なりは考慮しない
        static public void RegisterAndConnetStationBlocks(RailComponent[] components, IRailGraphDatastore railGraphDatastore) // componentsが2つ限定ver
        {
            var connectionMap = railGraphDatastore.GetRailPositionToConnectionDestination();
            Vector3Int[] roundedPositions = new Vector3Int[2];
            // *2 → 四捨五入 → int 化
            roundedPositions[0].x = Mathf.RoundToInt(components[0].Position.x * 2f);
            roundedPositions[0].y = Mathf.RoundToInt(components[0].Position.y * 2f);
            roundedPositions[0].z = Mathf.RoundToInt(components[0].Position.z * 2f);
            roundedPositions[1].x = Mathf.RoundToInt(components[1].Position.x * 2f);
            roundedPositions[1].y = Mathf.RoundToInt(components[1].Position.y * 2f);
            roundedPositions[1].z = Mathf.RoundToInt(components[1].Position.z * 2f);

            // もしpositions[0]がRailPositionToConnectionDestinationにみつかってかつpairのうちどちらか1個が存在するならそこに接続し残りを埋める、0個なら新規登録、2個の場合は考えない
            while (true)
            {
                if (connectionMap.TryGetValue(roundedPositions[0], out var pair))
                {
                    if ((!pair.Item1.IsDefault()) && (!pair.Item2.IsDefault()))
                    {
                        Debug.Assert(false, "RailComponentFactory.Create2RailComponents: Found multiple connection destinations for a single rail position.");
                        break;
                    }
                    if ((pair.Item1.IsDefault()) && (pair.Item2.IsDefault()))
                    {
                        //connectionMapのpositions[0]キーを削除
                        connectionMap.Remove(roundedPositions[0]);
                        continue;
                    }

                    var destinationConnection = (!pair.Item1.IsDefault()) ? pair.Item1 : pair.Item2;
                    var useFrontSideOfTarget = destinationConnection.IsFront;
                    var targetComponent = ConnectionDestinationToRailComponent(destinationConnection);
                    if (targetComponent == null) break;
                    //相手から自分に接続を考える(どっちでもいいが)
                    targetComponent.ConnectRailComponent(components[0], useFrontSideOfTarget, true);

                    var newdata = new ConnectionDestination(components[0].ComponentID, false);
                    if (pair.Item1.IsDefault())
                    {
                        pair.Item1 = newdata;
                    }
                    else
                    {
                        pair.Item2 = newdata;
                    }
                    connectionMap[roundedPositions[0]] = pair;
                    break;
                }
                else
                {
                    // 新規登録
                    var newdata = new ConnectionDestination(components[0].ComponentID, false);
                    var newpair = (newdata, ConnectionDestination.Default);
                    connectionMap[roundedPositions[0]] = newpair;
                    break;
                }
            }

            // もしpositions[1]がRailPositionToConnectionDestinationにみつかってかつpairのうちどちらか1個が存在するならそこに接続し残りを埋める、0個なら新規登録、2個の場合は考えない
            while (true)
            {
                if (connectionMap.TryGetValue(roundedPositions[1], out var pair))
                {
                    if ((!pair.Item1.IsDefault()) && (!pair.Item2.IsDefault()))
                    {
                        Debug.Assert(false, "RailComponentFactory.Create2RailComponents: Found multiple connection destinations for a single rail position.");
                        break;
                    }

                    if ((pair.Item1.IsDefault()) && (pair.Item2.IsDefault()))
                    {
                        //connectionMapのpositions[1]キーを削除
                        connectionMap.Remove(roundedPositions[1]);
                        continue;
                    }

                    var destinationConnection = (!pair.Item1.IsDefault()) ? pair.Item1 : pair.Item2;
                    var useFrontSideOfTarget = destinationConnection.IsFront;
                    var targetComponent = ConnectionDestinationToRailComponent(destinationConnection);
                    if (targetComponent == null) break;
                    //相手から自分に接続を考える(どっちでもいいが)
                    targetComponent.ConnectRailComponent(components[1], useFrontSideOfTarget, false);

                    var newdata = new ConnectionDestination(components[1].ComponentID, true);
                    if (pair.Item1.IsDefault())
                    {
                        pair.Item1 = newdata;
                    }
                    else
                    {
                        pair.Item2 = newdata;
                    }
                    connectionMap[roundedPositions[1]] = pair;
                    break;
                }
                else
                {
                    // 新規登録
                    var newdata = new ConnectionDestination(components[1].ComponentID, true);
                    var newpair = (newdata, ConnectionDestination.Default);
                    connectionMap[roundedPositions[1]] = newpair;
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


