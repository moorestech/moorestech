using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;

namespace Game.Block.Blocks.ElectricWire
{
    /// <summary>
    ///     電気機械が所属セグメントの確定済み電力供給率を導出するための共通窓口。
    ///     セグメント未所属、または全電線切断でトポロジ反映待ちの機械は供給率0として自然に停止する。
    ///     Shared entry for electric machines to derive the settled supply rate of their segment.
    ///     Machines with no segment, or with all wires cut and awaiting topology rebuild, get rate 0 and naturally stop.
    /// </summary>
    public static class ElectricSegmentPowerRateResolver
    {
        public static float GetPowerRate(BlockInstanceId blockInstanceId)
        {
            // セグメント未所属なら供給率0
            // No segment membership means supply rate 0
            var datastore = ServerContext.GetService<IElectricWireNetworkDatastore>();
            if (!datastore.TryGetEnergySegment(blockInstanceId, out var segment)) return 0f;

            // 全電線が切断されトポロジ反映待ちの場合も供給率0として扱う
            // All wires already cut but the topology rebuild is still pending: also treated as rate 0
            var block = ServerContext.WorldBlockDatastore.GetBlock(blockInstanceId);
            var connector = block?.GetComponent<IElectricWireConnector>();
            if (connector == null || connector.WireConnections.Count == 0) return 0f;

            return segment.Statistics.PowerRate;
        }
    }
}
