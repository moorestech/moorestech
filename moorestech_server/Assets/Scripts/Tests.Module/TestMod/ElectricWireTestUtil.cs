using System;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Tests.Module.TestMod
{
    /// <summary>
    /// テストで2つのブロックをワイヤー接続するユーティリティ。範囲スキャンの代わりに明示接続を張る
    /// Test utility to wire two blocks together; replaces range-scan auto-connect with explicit wiring
    /// </summary>
    public static class ElectricWireTestUtil
    {
        // 両端のIElectricWireConnectorを解決し、コスト0で双方向接続してから連結成分を再計算する
        // Resolve the connectors on both ends, add a cost-0 connection both ways, then recompute components
        public static void Connect(Vector3Int posA, Vector3Int posB)
        {
            var connectorA = ResolveConnector(posA);
            var connectorB = ResolveConnector(posB);

            var cost = new ElectricWireConnectionCost(ItemMaster.EmptyItemId, 0);
            connectorA.TryAddWireConnection(connectorB.BlockInstanceId, cost);
            connectorB.TryAddWireConnection(connectorA.BlockInstanceId, cost);

            ServerContext.GetService<IElectricWireNetworkDatastore>().RebuildAround(connectorA, connectorB);
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

            // 保留トポロジを本番入口経由で反映し、セグメントを確定させる
            // Apply the pending topology through the production entry so the segment settles
            new ElectricTickUpdater(ServerContext.GetService<ElectricWireNetworkDatastore>()).Update();

            var consumerId = world.GetBlock(consumerPos).BlockInstanceId;
            ServerContext.GetService<IElectricWireNetworkDatastore>().TryGetEnergySegment(consumerId, out var segment);
            var generator = new TestElectricGenerator(new ElectricPower(generatePower), new BlockInstanceId(_nextTestGeneratorId++));
            segment.AddGenerator(generator);
            return generator;
        }

        private static int _nextTestGeneratorId = 900000;
    }
}
