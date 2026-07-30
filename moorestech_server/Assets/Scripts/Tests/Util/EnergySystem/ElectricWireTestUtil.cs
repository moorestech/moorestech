using System;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.World.Interface.DataStore;
using Tests.Module;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.Util
{
    /// <summary>
    /// テストで2つのブロックをワイヤー接続するユーティリティ。範囲スキャンの代わりに明示接続を張る
    /// Test utility to wire two blocks together; replaces range-scan auto-connect with explicit wiring
    /// </summary>
    public static class ElectricWireTestUtil
    {
        // 両端のIElectricWireConnectorを解決し、コスト0で双方向接続する（dirty化は接続メソッド内で行われる）
        // Resolve the connectors on both ends and add a cost-0 connection both ways; the mutation itself marks the topology dirty
        public static void Connect(Vector3Int posA, Vector3Int posB)
        {
            var connectorA = ResolveConnector(posA);
            var connectorB = ResolveConnector(posB);

            var cost = ElectricWireConnectionCost.Empty;
            connectorA.TryAddWireConnection(connectorB.BlockInstanceId, cost);
            connectorB.TryAddWireConnection(connectorA.BlockInstanceId, cost);
        }

        private static IElectricWireConnector ResolveConnector(Vector3Int pos)
        {
            var block = ServerContext.WorldBlockDatastore.GetBlock(pos);
            return block.GetComponent<IElectricWireConnector>();
        }

        // 対象ブロックを電柱経由で給電セグメントへ入れ、出力可変のテスト発電機を登録して返す
        // Put the block into a powered segment through a pole and register a settable test generator, returning it
        public static TestElectricGenerator WirePower(Vector3Int consumerPos, Vector3Int polePos, float generatePower)
        {
            var world = ServerContext.WorldBlockDatastore;
            if (!world.Exists(polePos))
                world.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, polePos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            Connect(consumerPos, polePos);

            // dirtyな登録グラフを再構築してセグメントを確定させる
            // Rebuild the dirty live graph so the segment is applied immediately
            ServerContext.GetService<ElectricWireNetworkDatastore>().RebuildIfDirty();

            var consumerId = world.GetBlock(consumerPos).BlockInstanceId;
            ServerContext.GetService<IElectricWireNetworkLookup>().TryGetEnergySegment(consumerId, out var segment);
            var generator = new TestElectricGenerator(new ElectricPower(generatePower), new BlockInstanceId(_nextTestGeneratorId++));
            ElectricNetworkReflectionTestUtil.AddGenerator(segment, generator);
            return generator;
        }

        private static int _nextTestGeneratorId = 900000;
    }
}
