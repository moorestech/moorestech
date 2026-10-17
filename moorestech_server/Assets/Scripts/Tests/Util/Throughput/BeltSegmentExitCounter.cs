using System.Collections.Generic;
using Core.BeltTransport;
using Core.Item.Interface;
using UnityEngine;
using static Tests.Util.BeltWorldTestUtil;

namespace Tests.Util.Throughput
{
    // 監視マスを含むsegmentから出たアイテム数を tick ごとに数える。「segment のスループット100%」= 窓の間の搬出数が W/P にちょうど一致
    // Counts items leaving the segment that contains each watched cell, tick by tick; "100% segment throughput" = exits over the window equal W/P exactly
    // 搬出は「前の tick にいた個体が今いない」ことで検出する(buffer 内も segment の一部として数える)。Core に計測の口は足さない
    // An exit is detected as an instance present last tick and absent now (the buffer counts as part of the segment); no measuring hook is added to Core
    public sealed class BeltSegmentExitCounter
    {
        private readonly IReadOnlyList<Vector3Int> _cells;
        private readonly List<HashSet<ItemInstanceId>> _lastIds = new();
        private readonly List<int> _exits = new();

        public BeltSegmentExitCounter(IReadOnlyList<Vector3Int> cells)
        {
            _cells = cells;
            foreach (var cell in cells)
            {
                _lastIds.Add(IdsOn(SegmentAt(cell)));
                _exits.Add(0);
            }
        }

        public int Count => _cells.Count;

        // tick の後に呼ぶ。消えた個体の数を搬出として積む
        // Called after a tick; vanished instances are added as exits
        public void Observe()
        {
            for (var i = 0; i < _cells.Count; i++)
            {
                var now = IdsOn(SegmentAt(_cells[i]));
                foreach (var id in _lastIds[i])
                {
                    if (!now.Contains(id)) _exits[i]++;
                }
                _lastIds[i] = now;
            }
        }

        public List<int> Exits()
        {
            return new List<int>(_exits);
        }

        private static HashSet<ItemInstanceId> IdsOn(BeltConveyorSegment segment)
        {
            var ids = new HashSet<ItemInstanceId>();
            foreach (var state in segment.CaptureItems()) ids.Add(state.Item.ItemInstanceId);
            if (segment is BeltBufferedSegment buffered && buffered.Buffer.TryGetItem(out var held)) ids.Add(held.ItemInstanceId);
            return ids;
        }
    }
}
