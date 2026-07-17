using System;
using System.Collections.Generic;
using Game.Block.Interface;

namespace Game.EnergySystem
{
    // tick境界の電線所属mapを保持する
    // Holds connected components and ownership for the wire graph applied at one tick boundary
    internal class ElectricWireTopologyMap
    {
        private readonly Dictionary<BlockInstanceId, EnergySegment> _connectorToSegment;
        private readonly List<EnergySegment> _segments;

        private ElectricWireTopologyMap(Dictionary<BlockInstanceId, EnergySegment> connectorToSegment, List<EnergySegment> segments)
        {
            _connectorToSegment = connectorToSegment;
            _segments = segments;
        }

        public static ElectricWireTopologyMap CreateEmpty()
        {
            return new ElectricWireTopologyMap(new Dictionary<BlockInstanceId, EnergySegment>(), new List<EnergySegment>());
        }

        public static ElectricWireTopologyMap Build(ICollection<IElectricWireConnector> registeredConnectors)
        {
            // live頂点をID昇順の正準順に並べ、BFS入力を作る（生成順を登録履歴非依存にする）
            // Sort live vertices by ID into canonical order so build results never depend on registration history
            var remaining = new IElectricWireConnector[registeredConnectors.Count];
            registeredConnectors.CopyTo(remaining, 0);
            Array.Sort(remaining, (a, b) => a.BlockInstanceId.CompareTo(b.BlockInstanceId));
            var idToIndex = new Dictionary<BlockInstanceId, int>(registeredConnectors.Count);
            for (var i = 0; i < remaining.Length; i++) idToIndex.Add(remaining[i].BlockInstanceId, i);

            // 連結成分ごとに新しいsegmentを作る
            // Create a fresh segment for each connected component
            var components = ElectricWireSegmentSplitService.FindComponents(remaining, idToIndex);
            var connectorToSegment = new Dictionary<BlockInstanceId, EnergySegment>(registeredConnectors.Count);
            var segments = new List<EnergySegment>(components.Count);
            foreach (var component in components)
            {
                var segment = new EnergySegment();
                segments.Add(segment);
                foreach (var connector in component)
                {
                    RegisterRoles(segment, connector);
                    connectorToSegment.Add(connector.BlockInstanceId, segment);
                }
            }

            return new ElectricWireTopologyMap(connectorToSegment, segments);

            #region Internal

            static void RegisterRoles(EnergySegment segment, IElectricWireConnector connector)
            {
                if (connector.EnergyRole is IElectricConsumer consumer) segment.AddEnergyConsumer(consumer);
                if (connector.EnergyRole is IElectricGenerator generator) segment.AddGenerator(generator);
            }

            #endregion
        }

        public bool TryGetEnergySegment(BlockInstanceId blockInstanceId, out EnergySegment segment)
        {
            return _connectorToSegment.TryGetValue(blockInstanceId, out segment);
        }

        public IReadOnlyList<EnergySegment> GetSegments()
        {
            return _segments;
        }

        public void Destroy()
        {
            foreach (var segment in _segments) segment.Destroy();
            _connectorToSegment.Clear();
            _segments.Clear();
        }

    }
}
