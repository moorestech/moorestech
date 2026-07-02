using Core.Master;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
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
    }
}
