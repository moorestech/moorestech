using System.Collections.Generic;
using UnityEngine;

namespace Game.SaveLoad.Migration.Steps.V9ToV10
{
    /// <summary>
    /// 寝かせた後の向きで占めるセルの集合。縦置きを寝かせたときに既存の占有と重なるかを判定する
    /// The set of cells occupied in the laid-flat orientation; decides whether laying a vertical block flat would overlap what is already there
    /// </summary>
    internal sealed class LaidFlatOccupancy
    {
        private readonly HashSet<Vector3Int> _cells = new();

        public void Add(IEnumerable<Vector3Int> cells)
        {
            foreach (var cell in cells) _cells.Add(cell);
        }

        public bool Overlaps(IEnumerable<Vector3Int> cells)
        {
            foreach (var cell in cells)
            {
                if (_cells.Contains(cell)) return true;
            }

            return false;
        }
    }
}
