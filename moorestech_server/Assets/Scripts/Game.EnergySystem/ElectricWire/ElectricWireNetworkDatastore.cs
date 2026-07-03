using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;

namespace Game.EnergySystem
{
    /// <summary>
    /// ワイヤーグラフの連結成分としてEnergySegmentを管理する。GearNetworkDatastoreと同方式
    /// Manage EnergySegments as connected components of the wire graph, mirroring GearNetworkDatastore
    /// </summary>
    public class ElectricWireNetworkDatastore : IElectricWireNetworkDatastore
    {
        private readonly Dictionary<BlockInstanceId, EnergySegment> _connectorToSegment = new();
        private readonly Dictionary<EnergySegment, HashSet<IElectricWireConnector>> _segmentMembers = new();

        public int SegmentCount => _segmentMembers.Count;

        public void AddConnector(IElectricWireConnector connector)
        {
            if (_connectorToSegment.ContainsKey(connector.BlockInstanceId)) return;

            // 接続先が所属するセグメントを重複なく集める
            // Collect owning segments of connected partners without duplicates
            var connectedSegments = new HashSet<EnergySegment>();
            foreach (var connection in connector.WireConnections.Values)
                if (_connectorToSegment.TryGetValue(connection.Connector.BlockInstanceId, out var s))
                    connectedSegments.Add(s);

            switch (connectedSegments.Count)
            {
                case 0: CreateSegment(); break;
                case 1: JoinSegment(); break;
                default: MergeSegments(); break;
            }

            #region Internal

            void CreateSegment()
            {
                RegisterMember(new EnergySegment(), connector);
            }

            void JoinSegment()
            {
                RegisterMember(connectedSegments.First(), connector);
            }

            void MergeSegments()
            {
                // Union-by-size: メンバー数最大のセグメントへ他の全メンバーを移し替え、空セグメントはDestroyする
                // Union-by-size: fold all members into the largest segment and destroy the emptied ones
                EnergySegment largest = null;
                var largestSize = 0;
                foreach (var s in connectedSegments)
                {
                    var size = _segmentMembers[s].Count;
                    if (largest == null || largestSize < size)
                    {
                        largestSize = size;
                        largest = s;
                    }
                }

                // 非最大セグメントの全メンバーを最大セグメントへ移し、参照マップも張り替える
                // Move every member from the non-largest segments into the largest one and repoint the owner map
                foreach (var s in connectedSegments)
                {
                    if (s == largest) continue;
                    // ToList: RegisterMemberが_segmentMembersへ触るため元集合のスナップショットを走査する
                    // ToList: iterate a snapshot since RegisterMember touches _segmentMembers
                    foreach (var member in _segmentMembers[s].ToList()) RegisterMember(largest, member);
                    _segmentMembers.Remove(s);
                    s.Destroy();
                }

                RegisterMember(largest, connector);
            }

            #endregion
        }

        public void RemoveConnector(IElectricWireConnector connector)
        {
            if (!_connectorToSegment.TryGetValue(connector.BlockInstanceId, out var segment)) return;

            // 所属セグメントから役割とメンバーを除去
            // Strip roles and membership from the owning segment
            RemoveRoles();
            var members = _segmentMembers[segment];
            members.Remove(connector);
            _connectorToSegment.Remove(connector.BlockInstanceId);

            // メンバー0なら丸ごとDestroyしてマップから外す。GameUpdater購読リークを防ぐ
            // No members left: destroy the segment entirely and drop it from the maps to avoid a GameUpdater subscription leak
            if (members.Count == 0)
            {
                _segmentMembers.Remove(segment);
                segment.Destroy();
                return;
            }

            var components = ElectricWireSegmentSplitService.FindComponents(members);

            // 分断なし → 既存セグメントをそのまま維持
            // No split: keep the existing segment as-is
            if (components.Count == 1) return;

            // 複数成分へ分断 → 既存セグメントを破棄し、成分ごとに新セグメントを生成
            // Split into multiple components: discard the old segment and create one per component
            _segmentMembers.Remove(segment);
            segment.Destroy();
            foreach (var component in components)
            {
                var newSegment = new EnergySegment();
                var newMembers = new HashSet<IElectricWireConnector>();
                foreach (var member in component)
                {
                    AddRoles(newSegment, member);
                    newMembers.Add(member);
                    _connectorToSegment[member.BlockInstanceId] = newSegment;
                }
                _segmentMembers.Add(newSegment, newMembers);
            }

            #region Internal

            // 所属セグメントから各エネルギー役割を取り除く
            // Strip each energy role from the owning segment
            void RemoveRoles()
            {
                if (connector.WireConsumer != null) segment.RemoveEnergyConsumer(connector.WireConsumer);
                if (connector.WireGenerator != null) segment.RemoveGenerator(connector.WireGenerator);
                if (connector.WireTransformer != null) segment.RemoveEnergyTransformer(connector.WireTransformer);
            }

            #endregion
        }

        public void RebuildAround(params IElectricWireConnector[] connectors)
        {
            // ワイヤーの追加・削除後に両端点を除去→再追加して連結成分を再計算する
            // Recompute components after wire edits by removing then re-adding both endpoints
            foreach (var c in connectors) RemoveConnector(c);
            foreach (var c in connectors) AddConnector(c);
        }

        public bool TryGetEnergySegment(BlockInstanceId blockInstanceId, out EnergySegment segment)
        {
            return _connectorToSegment.TryGetValue(blockInstanceId, out segment);
        }

        public IReadOnlyList<EnergySegment> GetSegments()
        {
            return _segmentMembers.Keys.ToList();
        }

        private void RegisterMember(EnergySegment segment, IElectricWireConnector connector)
        {
            if (!_segmentMembers.TryGetValue(segment, out var members))
            {
                members = new HashSet<IElectricWireConnector>();
                _segmentMembers.Add(segment, members);
            }
            members.Add(connector);
            _connectorToSegment[connector.BlockInstanceId] = segment;
            AddRoles(segment, connector);
        }

        private static void AddRoles(EnergySegment segment, IElectricWireConnector connector)
        {
            if (connector.WireConsumer != null) segment.AddEnergyConsumer(connector.WireConsumer);
            if (connector.WireGenerator != null) segment.AddGenerator(connector.WireGenerator);
            if (connector.WireTransformer != null) segment.AddEnergyTransformer(connector.WireTransformer);
        }
    }
}
