using System.Collections.Generic;
using UnityEngine;

namespace Game.CleanRoom
{
    // 検出された1つのクリーンルーム。ブロックから導出される派生状態。
    // フェーズ2が純度状態（N/Status/ThresholdIndex）を本クラスに追加する（codemap §1.2）。
    // A single detected clean room; derived state computed from blocks.
    // Phase 2 adds purity state (N/Status/ThresholdIndex) to this class (codemap §1.2).
    public class CleanRoom
    {
        // Id は一時参照用。永続キーにしてはいけない。
        // Id is an ephemeral handle, NOT a persistence key.
        public int Id { get; }

        // Cells は機械等の占有セルを含む全内部セル。
        // Cells contains all interior cells incl. machine-occupied ones.
        public IReadOnlyCollection<Vector3Int> Cells => _cells;

        // V = Cells のうち空セル数。S = 空セルが境界に接する面の数。
        // V = empty-cell count within Cells. S = empty-cell faces touching boundary.
        public int Volume { get; }
        public int SurfaceArea { get; }

        private readonly HashSet<Vector3Int> _cells;

        public CleanRoom(int id, HashSet<Vector3Int> cells, int volume, int surfaceArea)
        {
            Id = id;
            _cells = cells;
            Volume = volume;
            SurfaceArea = surfaceArea;
        }

        public bool Contains(Vector3Int cell) => _cells.Contains(cell);
    }
}
