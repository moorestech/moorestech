using System;
using System.Collections.Generic;
using Core.Master;
using Mooresmaster.Model.CleanRoomModule;
using UnityEngine;

namespace Game.CleanRoom
{
    /// <summary>
    ///     密閉検出で得られた1つの部屋。Id は揮発値であり永続キーにしてはならない
    ///     One room found by sealed-room detection. Id is volatile and must not be persisted
    /// </summary>
    public class CleanRoom
    {
        public int Id { get; }

        // 占有セルも含む全内部セル
        // All interior cells including occupied ones
        public IReadOnlyCollection<Vector3Int> Cells => _cells;

        // Volume は空セル数、SurfaceArea は空セルが境界に接する面数
        // Volume counts empty cells; SurfaceArea counts empty-cell faces touching the boundary
        public int Volume { get; }
        public int SurfaceArea { get; }

        public double ImpurityCount { get; private set; }
        public int ThresholdIndex { get; private set; }

        private readonly HashSet<Vector3Int> _cells;

        public CleanRoom(int id, HashSet<Vector3Int> cells, int volume, int surfaceArea)
        {
            Id = id;
            _cells = cells;
            Volume = volume;
            SurfaceArea = surfaceArea;

            // 生成直後はクリーン度未達（Out）から始める
            // A fresh room starts at the out-of-class sentinel index
            ImpurityCount = 0;
            ThresholdIndex = MasterHolder.CleanRoomMaster.OutThresholdIndex;
        }

        // 不純物数は負になり得ないため0でクランプする
        // Impurity count can never be negative, so clamp at zero
        public void SetImpurity(double value)
        {
            ImpurityCount = Math.Max(0, value);
        }

        public void SetThresholdIndex(int index)
        {
            ThresholdIndex = index;
        }

        // 閾値行がOutでなければその行を返す。「部屋がOutか」の判定はここだけに置く
        // Returns the threshold row unless the room is Out; the only place that decides whether a room is Out
        public bool TryGetClassRow(out ThresholdsElement row)
        {
            var master = MasterHolder.CleanRoomMaster;
            if (master.OutThresholdIndex <= ThresholdIndex)
            {
                row = default;
                return false;
            }

            row = master.Thresholds[ThresholdIndex];
            return true;
        }

        public bool Contains(Vector3Int cell)
        {
            return _cells.Contains(cell);
        }
    }
}
