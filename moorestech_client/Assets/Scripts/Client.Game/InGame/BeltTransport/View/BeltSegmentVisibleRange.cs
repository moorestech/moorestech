using Game.Block.Blocks.BeltConveyor.Sync.State;
using UnityEngine;

namespace Client.Game.InGame.BeltTransport.View
{
    // segment 単位の具現化範囲。複製の組み立てごとに各 segment の中心と半径をマス座標から 1 回求め、tick ごとに基準点との距離で内外を決める
    // Per-segment materialization range: on each replica assembly the center and radius of every segment are derived once from its cells, and each tick the distance to the viewpoint decides in or out
    // 入る側と出る側の距離に差を付け、境界で毎 tick 出入りしないようにする。距離はマップオブジェクトの表示制御と同じ値(ADR 0085)
    // Enter and leave distances differ so a segment on the border does not flicker every tick; the values match the map object visibility control (ADR 0085)
    public sealed class BeltSegmentVisibleRange
    {
        public const float EnterDistance = 340f;
        public const float LeaveDistance = 350f;

        // マス中心からの余裕。坂のマスは中心が 0.5 高く、アイテムの表示物は一辺 1 m 未満
        // Margin beyond the cell centers: slope cells sit 0.5 higher and an item object is under 1 m per side
        private const float CellMargin = 1f;

        private readonly Vector3[] _centers;
        private readonly float[] _radii;
        private readonly bool[] _inside;

        public BeltSegmentVisibleRange(BeltSegmentShape[] shapes)
        {
            _centers = new Vector3[shapes.Length];
            _radii = new float[shapes.Length];
            _inside = new bool[shapes.Length];
            for (var segmentIndex = 0; segmentIndex < shapes.Length; segmentIndex++)
            {
                var cells = shapes[segmentIndex].Cells;
                if (cells.Length == 0) continue;
                var sum = Vector3.zero;
                foreach (var cell in cells) sum += CellCenter(cell.Position);
                var center = sum / cells.Length;
                var radius = 0f;
                foreach (var cell in cells) radius = Mathf.Max(radius, Vector3.Distance(center, CellCenter(cell.Position)));
                _centers[segmentIndex] = center;
                _radii[segmentIndex] = radius + CellMargin;
            }
        }

        // 範囲内なら true。前回内側だった segment は出る側の距離まで内側のまま
        // True when inside; a segment that was inside stays inside until the leave distance
        public bool Contains(int segmentIndex, Vector3 viewpoint)
        {
            var distance = Vector3.Distance(viewpoint, _centers[segmentIndex]) - _radii[segmentIndex];
            var threshold = _inside[segmentIndex] ? LeaveDistance : EnterDistance;
            _inside[segmentIndex] = distance < threshold;
            return _inside[segmentIndex];
        }

        private static Vector3 CellCenter(Vector3Int cellPosition)
        {
            return new Vector3(cellPosition.x + 0.5f, cellPosition.y + 0.5f, cellPosition.z + 0.5f);
        }
    }
}
