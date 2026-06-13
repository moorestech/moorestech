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

        // ---- 純度状態（データストアが毎tick更新。再検出/ロードで引き継ぐ） ----
        // ---- Purity state, updated by the datastore each tick; carried across re-detection/load ----
        public double ImpurityCount { get; private set; }                  // N（個）
        public CleanRoomRoomStatus Status { get; private set; } = CleanRoomRoomStatus.Valid;
        public int ThresholdIndex { get; private set; } = int.MaxValue;    // 生成直後は未判定（最悪側）。データストアが Out 値で初期化する
        public double GraceRemainingSeconds { get; private set; }
        public double Concentration => Volume > 0 ? ImpurityCount / Volume : 0.0;

        public void AddImpurity(double delta)
        {
            ImpurityCount += delta;
            if (ImpurityCount < 0.0) ImpurityCount = 0.0;
        }

        public void RemoveImpurity(double removed)
        {
            ImpurityCount -= removed;
            if (ImpurityCount < 0.0) ImpurityCount = 0.0;
        }

        // ステータスと猶予を同時に設定。猶予は負にしない。
        // Set status and grace together; grace clamped to zero.
        public void SetStatus(CleanRoomRoomStatus status, double graceSeconds)
        {
            Status = status;
            GraceRemainingSeconds = graceSeconds < 0.0 ? 0.0 : graceSeconds;
        }

        public void SetThresholdIndex(int index)
        {
            ThresholdIndex = index;
        }

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
