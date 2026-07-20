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
            // live頂点からBFS入力を作る
            // Walk live vertices once, building the BFS array and ID lookup together
            var remaining = new IElectricWireConnector[registeredConnectors.Count];
            var idToIndex = new Dictionary<BlockInstanceId, int>(registeredConnectors.Count);
            var index = 0;
            foreach (var connector in registeredConnectors)
            {
                remaining[index] = connector;
                idToIndex.Add(connector.BlockInstanceId, index);
                index++;
            }

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

            void RegisterRoles(EnergySegment targetSegment, IElectricWireConnector targetConnector)
            {
                if (targetConnector.EnergyRole is IElectricConsumer consumer) targetSegment.AddEnergyConsumer(consumer);
                if (targetConnector.EnergyRole is IElectricGenerator generator) targetSegment.AddGenerator(generator);
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
