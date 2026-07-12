using System.Collections.Generic;
using Game.Block.Interface;

namespace Game.EnergySystem
{
    /// <summary>
    /// トポロジ変更をその場で適用せずコマンドとして保留し、tick先頭・tick末尾のflushでFIFO一括反映するデータストア。
    /// これによりtick途中でセグメントの所属や列挙内容が変化しないことを保証する。
    /// Datastore that queues topology mutations as commands instead of applying them in place,
    /// flushing them FIFO at tick head and tick end so segment membership never changes mid-tick.
    /// </summary>
    public class ElectricWireNetworkDatastore : IElectricWireNetworkDatastore
    {
        private readonly ElectricWireTopologyMap _topologyMap = new();
        private readonly List<ElectricWireTopologyCommand> _pendingCommands = new();
        private bool _isFlushing;

        public int SegmentCount => _topologyMap.SegmentCount;

        public void AddConnector(IElectricWireConnector connector)
        {
            _pendingCommands.Add(new ElectricWireTopologyCommand(ElectricWireTopologyCommandType.Add, new[] { connector }));
        }

        public void RemoveConnector(IElectricWireConnector connector)
        {
            _pendingCommands.Add(new ElectricWireTopologyCommand(ElectricWireTopologyCommandType.Remove, new[] { connector }));
        }

        public void RebuildAround(params IElectricWireConnector[] connectors)
        {
            _pendingCommands.Add(new ElectricWireTopologyCommand(ElectricWireTopologyCommandType.Rebuild, connectors));
        }

        // 保留コマンドをFIFOで一括適用する。tick先頭とtick末尾からのみ呼ばれる
        // Apply pending commands in FIFO order; called only at tick head and tick end
        public void FlushPendingCommands()
        {
            if (_isFlushing || _pendingCommands.Count == 0) return;

            _isFlushing = true;
            for (var i = 0; i < _pendingCommands.Count; i++)
            {
                ApplyCommand(_pendingCommands[i]);
            }
            _pendingCommands.Clear();
            _isFlushing = false;

            #region Internal

            void ApplyCommand(ElectricWireTopologyCommand command)
            {
                switch (command.CommandType)
                {
                    case ElectricWireTopologyCommandType.Add:
                        foreach (var connector in command.Connectors) _topologyMap.AddConnector(connector);
                        break;
                    case ElectricWireTopologyCommandType.Remove:
                        foreach (var connector in command.Connectors) _topologyMap.RemoveConnector(connector);
                        break;
                    case ElectricWireTopologyCommandType.Rebuild:
                        // 両端点を除去→再追加して連結成分を再計算する
                        // Remove then re-add both endpoints to recompute connected components
                        foreach (var connector in command.Connectors) _topologyMap.RemoveConnector(connector);
                        foreach (var connector in command.Connectors) _topologyMap.AddConnector(connector);
                        break;
                }
            }

            #endregion
        }

        public bool TryGetEnergySegment(BlockInstanceId blockInstanceId, out EnergySegment segment)
        {
            return _topologyMap.TryGetEnergySegment(blockInstanceId, out segment);
        }

        public IReadOnlyList<EnergySegment> GetSegments()
        {
            return _topologyMap.GetSegments();
        }
    }
}
