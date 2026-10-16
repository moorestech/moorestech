using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.RangeBox
{
    // 曲線係数は形状変更時だけ作る。半開セル箱との連続交差を判定する
    // Build coefficients only when geometry changes; intersect the continuous curve with a half-open cell box
    internal sealed class BezierCellBoxIntersection
    {
        private readonly CubicPolynomial[] _axes = new CubicPolynomial[3];
        private readonly List<double> _cuts = new(20);
        private readonly List<double> _roots = new(3);

        internal BezierCellBoxIntersection(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            for (var axis = 0; axis < 3; axis++) _axes[axis] = new CubicPolynomial(p0[axis], p1[axis], p2[axis], p3[axis]);
        }

        internal bool Intersects(Vector3Int min, Vector3Int max)
        {
            _cuts.Clear();
            _cuts.Add(0);
            _cuts.Add(1);
            // 六境界面の根で区間を分け、根自身と区間内部の両方を調べる
            // Partition at all six face roots and test both boundary points and interval interiors
            for (var axis = 0; axis < 3; axis++)
            {
                if (AddFace(axis, min[axis])) return true;
                if (AddFace(axis, (double)max[axis] + 1)) return true;
            }
            if (Contains(0, -1, 0) || Contains(1, -1, 0)) return true;
            _cuts.Sort();
            for (var i = 0; i < _cuts.Count; i++)
            {
                // 根は上で境界由来座標を固定済み。丸めた根を通常点として再評価しない
                // Roots were tested with their boundary coordinate pinned; never re-evaluate rounded roots as ordinary points
                if (i + 1 < _cuts.Count && _cuts[i] < _cuts[i + 1] && Contains((_cuts[i] + _cuts[i + 1]) / 2, -1, 0)) return true;
            }
            return false;

            #region Internal
            bool AddFace(int axis, double boundary)
            {
                _roots.Clear();
                _axes[axis].AddBoundaryRoots(boundary, _roots);
                foreach (var root in _roots)
                {
                    _cuts.Add(root);
                    // 求めた面の座標を固定し、丸めで下限接触を落とさない
                    // Pin the solved coordinate to its face so rounding cannot lose lower-face contact
                    if (Contains(root, axis, boundary)) return true;
                }
                return false;
            }
            bool Contains(double t, int boundaryAxis, double boundary)
            {
                for (var axis = 0; axis < 3; axis++)
                {
                    var value = axis == boundaryAxis ? boundary : _axes[axis].Evaluate(t);
                    if (value < min[axis] || value >= (double)max[axis] + 1) return false;
                }
                return true;
            }
            #endregion
        }
    }
}
