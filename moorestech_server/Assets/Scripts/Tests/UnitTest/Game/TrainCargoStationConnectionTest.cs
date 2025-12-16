using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Train.RailGraph;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.UnitTest.Game
{
    public class TrainCargoStationConnectionTest
    {
        // 貨物駅と駅のブロック隣接時限定自動レール接続処理のテスト
        // レール接続を方向別に検証する
        [TestCase(BlockDirection.East, BlockDirection.West)]
        [TestCase(BlockDirection.West, BlockDirection.East)]
        [TestCase(BlockDirection.East, BlockDirection.East)]
        [TestCase(BlockDirection.West, BlockDirection.West)]
        [TestCase(BlockDirection.North, BlockDirection.South)]
        [TestCase(BlockDirection.South, BlockDirection.North)]
        [TestCase(BlockDirection.North, BlockDirection.North)]
        [TestCase(BlockDirection.South, BlockDirection.South)]
        public void RailComponentsConnectWhenBlocksAreAdjacent(BlockDirection cargoDirection, BlockDirection stationDirection)
        {
            // テスト環境を初期化する
            // Initialize isolated train test environment
            var environment = TrainTestHelper.CreateEnvironment();

            // マスターデータから自動計算した座標を使う
            // Use coordinates auto-calculated from master data
            var (cargoPosition, stationPosition) = ResolveBlockPositions(cargoDirection, stationDirection);
            //Debug.Log($"貨物駅位置: {cargoPosition}, 駅位置: {stationPosition}");

            // 貨物駅ブロックを設置してRailSaverを取得する
            // Place cargo platform block and obtain its RailSaver component
            var (_, cargoSaver) = TrainTestHelper.PlaceBlockWithComponent<RailSaverComponent>(
                environment,
                ForUnitTestModBlockId.TestTrainCargoPlatform,
                cargoPosition,
                cargoDirection);
            Assert.IsNotNull(cargoSaver, "貨物駅のRailSaverComponentを取得できませんでした。");

            // 駅ブロックを設置してRailSaverを取得する
            // Place station block and obtain its RailSaver component
            var (_, stationSaver) = TrainTestHelper.PlaceBlockWithComponent<RailSaverComponent>(
                environment,
                ForUnitTestModBlockId.TestTrainStation,
                stationPosition,
                stationDirection);
            Assert.IsNotNull(stationSaver, "駅ブロックのRailSaverComponentを取得できませんでした。");

            // 双方のRailComponentが距離0で接続されていることを確認する
            // Ensure at least one pair of rail nodes connects with zero distance
            AssertRailComponentsAreLinked(cargoSaver!, stationSaver!);

            #region Internal

            (Vector3Int cargoPos, Vector3Int stationPos) ResolveBlockPositions(BlockDirection cargoDir, BlockDirection stationDir)
            {
                // マスターデータからブロックサイズを取得する
                // Fetch block sizes from the master data set
                var blockMaster = MasterHolder.BlockMaster;
                var cargoSize = blockMaster.GetBlockMaster(ForUnitTestModBlockId.TestTrainCargoPlatform).BlockSize;
                var stationSize = blockMaster.GetBlockMaster(ForUnitTestModBlockId.TestTrainStation).BlockSize;

                // 対象方向が水平方向かつ同一軸であることを検証する
                // Ensure both directions are horizontal and share the same axis
                Assert.IsTrue(IsHorizontal(cargoDir), $"貨物ブロック方向が水平ではありません: {cargoDir}");
                Assert.IsTrue(IsHorizontal(stationDir), $"駅ブロック方向が水平ではありません: {stationDir}");
                Assert.IsTrue(SharesAxis(cargoDir, stationDir), $"異なる軸では接続できません: {cargoDir} <-> {stationDir}");

                // 単位ベクトルとブロック長を計算する
                // Calculate unit vectors and block lengths
                var cargoForward = ConvertDirectionToVector(cargoDir);
                var stationForward = ConvertDirectionToVector(stationDir);
                var cargoLength = GetForwardLength(cargoDir, cargoSize);
                var stationLength = GetForwardLength(stationDir, stationSize);
                Assert.AreNotEqual(0, cargoLength, "貨物プラットフォーム長が0です");
                Assert.AreNotEqual(0, stationLength, "駅ブロック長が0です");
                Assert.IsTrue(cargoForward == stationForward || cargoForward == -stationForward, $"向きが平行ではありません: {cargoDir} <-> {stationDir}");

                // 貨物を原点に、駅を貨物の進行方向へ隣接させる
                // Place cargo at origin and station right next to it along the cargo direction
                var cargoPosCandidate = Vector3Int.zero;
                var stationPosCandidate = cargoPosCandidate + cargoForward * cargoLength;

                // 負座標が発生する場合は平行移動して補正する
                // Shift both positions when negative coordinates appear

                var normalized = NormalizePositions(cargoPosCandidate, stationPosCandidate);
                return (normalized.cargo, normalized.station);

                Vector3Int ConvertDirectionToVector(BlockDirection direction)
                {
                    // 水平方向のみ対応し、その他はテスト失敗とする
                    // Support horizontal axes only; fail otherwise
                    switch (direction)
                    {
                        case BlockDirection.East:
                            return Vector3Int.right;
                        case BlockDirection.West:
                            return Vector3Int.left;
                        case BlockDirection.North:
                            return Vector3Int.forward;
                        case BlockDirection.South:
                            return Vector3Int.back;
                        default:
                            Assert.Fail($"未対応の方向です: {direction}");
                            return Vector3Int.zero;
                    }
                }

                bool IsHorizontal(BlockDirection direction) => direction is BlockDirection.East or BlockDirection.West or BlockDirection.North or BlockDirection.South;

                bool SharesAxis(BlockDirection first, BlockDirection second) => IsXAxis(first) && IsXAxis(second) || IsZAxis(first) && IsZAxis(second);

                bool IsXAxis(BlockDirection direction) => direction is BlockDirection.East or BlockDirection.West;

                bool IsZAxis(BlockDirection direction) => direction is BlockDirection.North or BlockDirection.South;

                int GetForwardLength(BlockDirection direction, Vector3Int size)
                {
                    // 水平ブロックでは常にZ成分が進行方向長となる
                    // Horizontal blocks always use the Z component as forward length
                    if (IsHorizontal(direction))
                    {
                        return size.z;
                    }

                    Assert.Fail($"未対応の方向です: {direction}");
                    return 0;
                }

                (Vector3Int cargo, Vector3Int station) NormalizePositions(Vector3Int cargoPosToNormalize, Vector3Int stationPosToNormalize)
                {
                    // 各軸の最小値が負なら原点方向へシフトする
                    // Shift both coordinates so every axis remains non-negative
                    var minX = Mathf.Min(cargoPosToNormalize.x, stationPosToNormalize.x);
                    if (minX < 0)
                    {
                        var offset = -minX;
                        cargoPosToNormalize.x += offset;
                        stationPosToNormalize.x += offset;
                    }

                    var minY = Mathf.Min(cargoPosToNormalize.y, stationPosToNormalize.y);
                    if (minY < 0)
                    {
                        var offset = -minY;
                        cargoPosToNormalize.y += offset;
                        stationPosToNormalize.y += offset;
                    }

                    var minZ = Mathf.Min(cargoPosToNormalize.z, stationPosToNormalize.z);
                    if (minZ < 0)
                    {
                        var offset = -minZ;
                        cargoPosToNormalize.z += offset;
                        stationPosToNormalize.z += offset;
                    }

                    return (cargoPosToNormalize, stationPosToNormalize);
                }
            }

            void AssertRailComponentsAreLinked(RailSaverComponent firstSaver, RailSaverComponent secondSaver)
            {
                // RailNodeを列挙して接続が存在するか探索する
                // Enumerate RailNodes and search for direct connections
                var firstNodes = CollectNodes(firstSaver);
                var secondNodes = CollectNodes(secondSaver);

                // すべての組み合わせで距離を計算し、直接接続を検出する
                // Calculate distances across all node pairs to detect direct adjacency
                var isConnected = false;
                foreach (var firstNode in firstNodes)
                {
                    foreach (var secondNode in secondNodes)
                    {
                        var distance = RailGraphDatastore.GetDistanceBetweenNodes(firstNode, secondNode);
                        if (distance == 0)
                        {
                            isConnected = true;
                            break;
                        }
                    }

                    if (isConnected)
                    {
                        break;
                    }
                }

                Assert.IsTrue(isConnected, "隣接する貨物駅と駅のRailComponentが接続されていません。");

                List<RailNode> CollectNodes(RailSaverComponent saver)
                {
                    // RailSaverに含まれる全ノードを収集する
                    // Gather all nodes contained within the RailSaver
                    var nodes = new List<RailNode>(saver.RailComponents.Length * 2);
                    foreach (var component in saver.RailComponents)
                    {
                        nodes.Add(component.FrontNode);
                        nodes.Add(component.BackNode);
                    }

                    return nodes;
                }
            }

            #endregion
        }
    }
}
