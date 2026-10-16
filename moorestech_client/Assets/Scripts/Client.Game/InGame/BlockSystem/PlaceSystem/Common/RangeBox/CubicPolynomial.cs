using System;
using System.Collections.Generic;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.RangeBox
{
    // 微分根で単調区間へ分け、サンプル間隔に依存せず境界根を求める
    // Split at derivative roots and solve boundary roots independently of sampling intervals
    internal readonly struct CubicPolynomial
    {
        private readonly double _a, _b, _c, _d;
        internal CubicPolynomial(double p0, double p1, double p2, double p3)
        {
            _a = -p0 + 3 * p1 - 3 * p2 + p3;
            _b = 3 * p0 - 6 * p1 + 3 * p2;
            _c = -3 * p0 + 3 * p1;
            _d = p0;
        }

        internal double Evaluate(double t) => ((_a * t + _b) * t + _c) * t + _d;

        internal void AddBoundaryRoots(double boundary, List<double> roots)
        {
            var cuts = new List<double> { 0, 1 };
            AddDerivativeRoots(cuts);
            cuts.Sort();
            for (var i = 0; i < cuts.Count; i++)
            {
                var left = cuts[i];
                var leftValue = Evaluate(left) - boundary;
                if (leftValue == 0) roots.Add(left);
                if (i + 1 == cuts.Count) continue;
                var right = cuts[i + 1];
                var rightValue = Evaluate(right) - boundary;
                if (leftValue == 0 || rightValue == 0 || Math.Sign(leftValue) == Math.Sign(rightValue)) continue;

                // 単調区間の符号反転を二分する。doubleで区別不能になれば終了する
                // Bisect the sign change on a monotone interval until doubles cannot split it
                for (var iteration = 0; iteration < 64; iteration++)
                {
                    var middle = (left + right) / 2;
                    if (middle == left || middle == right) break;
                    var middleValue = Evaluate(middle) - boundary;
                    if (middleValue == 0) { left = right = middle; break; }
                    if (Math.Sign(middleValue) == Math.Sign(leftValue)) { left = middle; leftValue = middleValue; }
                    else right = middle;
                }
                roots.Add((left + right) / 2);
            }
        }

        private void AddDerivativeRoots(List<double> cuts)
        {
            var a = 3 * _a;
            var b = 2 * _b;
            if (a == 0)
            {
                if (b != 0) Add(-_c / b);
                return;
            }
            var discriminant = b * b - 4 * a * _c;
            if (discriminant < 0) return;
            var root = Math.Sqrt(discriminant);
            Add((-b - root) / (2 * a));
            Add((-b + root) / (2 * a));

            #region Internal
            void Add(double value)
            {
                if (value > 0 && value < 1) cuts.Add(value);
            }
            #endregion
        }
    }
}
