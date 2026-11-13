using System.Collections.Generic;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route.Search
{
    /// <summary>
    ///     始点から空きセルをベルトの歩幅（水平1歩、坂ありなら高さ±1を伴う水平1歩）で塗りつぶし、終点のセルへ届くかだけを見る軽い到達判定
    ///     A light reachability probe that floods free cells with belt steps (one horizontal step, with slopes also one step that shifts height by one) and reports only whether the end cell is touched
    ///     坂の真上・山谷・曲がり・既存の入出力口は見ないので「届く」は必要条件。届かなければ本探索を回すまでもなく経路なし
    ///     Slope heads, peaks and valleys, turns and existing ports are ignored, so reachable is only necessary; unreachable means no route without the real search
    /// </summary>
    public sealed class BeltRouteReachability
    {
        private readonly bool[] _visited;
        private readonly int[] _queue;
        private readonly List<int> _touched = new();

        public BeltRouteReachability(int cellCount)
        {
            _visited = new bool[cellCount];
            _queue = new int[cellCount];
        }

        // 始点は既存ベルト（分岐）でも塗り始める。終点は空きセルと確かめ済みなので触れた時点で到達
        // The start is flooded even when it is an existing belt (a branch); the end is a verified free cell, so touching it means reached
        public bool CanReach(BeltRouteTransitionRule rule, BeltRouteGrid grid)
        {
            var query = rule.Query;
            if (query.StartCell == query.EndCell) return true;
            ResetTouched();

            var sizeX = grid.Size.x;
            var sizeY = grid.Size.y;
            var sizeZ = grid.Size.z;
            var layer = grid.LayerCellCount;
            var endIndex = grid.CellIndex(query.EndCell);
            var verticalReach = query.AllowSlopes ? 1 : 0;
            var head = 0;
            var tail = 0;
            Visit(grid.CellIndex(query.StartCell));
            while (head < tail)
            {
                var index = _queue[head++];
                var x = index % sizeX;
                var rest = index / sizeX;
                var z = rest % sizeZ;
                var y = rest / sizeZ;
                for (var dy = -verticalReach; dy <= verticalReach; dy++)
                {
                    var targetY = y + dy;
                    if (targetY < 0 || sizeY <= targetY) continue;
                    var level = index + dy * layer;
                    if (0 < x && Step(level - 1)) return true;
                    if (x < sizeX - 1 && Step(level + 1)) return true;
                    if (0 < z && Step(level - sizeX)) return true;
                    if (z < sizeZ - 1 && Step(level + sizeX)) return true;
                }
            }
            return false;

            #region Internal

            // 終点に触れたらtrue。空きセルなら塗って待ち行列へ
            // True on touching the end; a free cell is painted and queued
            bool Step(int next)
            {
                if (next == endIndex) return true;
                if (_visited[next] || !rule.IsCellFreeAt(next)) return false;
                Visit(next);
                return false;
            }

            void Visit(int index)
            {
                _visited[index] = true;
                _touched.Add(index);
                _queue[tail++] = index;
            }

            #endregion
        }

        private void ResetTouched()
        {
            foreach (var index in _touched) _visited[index] = false;
            _touched.Clear();
        }
    }
}
