using System.Collections.Generic;
using CSparse;
using CSparse.Double.Factorization;
using CSparse.Storage;

namespace Game.Gear.Common
{
    // 歯車ネットワークの各エッジを通過するトルクを Lφ = b の疎コレスキー解で計算するソルバ
    // Solver that computes per-edge torque flow on a gear network via sparse Cholesky on Lφ = b
    public sealed class TorqueLoadSolver
    {
        private int _nodeCount;
        private int _reducedSize;
        private int[] _edgeI;
        private int[] _edgeJ;

        private SparseCholesky _chol;
        private double[] _bReduced;
        private double[] _phiReduced;
        private double[] _phiFull;

        // 段階A: 構造変更時のみ呼ぶ。ラプラシアン構築 + コレスキー分解
        // Phase A: invoked when topology changes. Build Laplacian and factorize
        public void Prepare(int nodeCount, IReadOnlyList<(int i, int j)> edges)
        {
            _nodeCount = nodeCount;
            _reducedSize = nodeCount - 1;
            BuildEdgeArrays();
            var reduced = BuildReducedLaplacian();
            _chol = SparseCholesky.Create(reduced, ColumnOrdering.MinimumDegreeAtPlusA);
            _bReduced = new double[_reducedSize];
            _phiReduced = new double[_reducedSize];
            _phiFull = new double[_nodeCount];

            #region Internal

            void BuildEdgeArrays()
            {
                // エッジ端点を配列にコピー（Solveでの参照を高速化）
                // Cache edge endpoints into flat arrays for fast Solve-time access
                _edgeI = new int[edges.Count];
                _edgeJ = new int[edges.Count];
                for (var k = 0; k < edges.Count; k++)
                {
                    _edgeI[k] = edges[k].i;
                    _edgeJ[k] = edges[k].j;
                }
            }

            CSparse.Double.SparseMatrix BuildReducedLaplacian()
            {
                // ノード0をφ=0で固定し、残り(N-1)ノードで対称正定値系を構築する
                // Anchor node 0 at φ=0 and build the symmetric positive definite reduced system
                var degree = new int[nodeCount];
                for (var k = 0; k < edges.Count; k++)
                {
                    degree[edges[k].i]++;
                    degree[edges[k].j]++;
                }

                var nnzEstimate = _reducedSize + edges.Count * 2;
                var coo = new CoordinateStorage<double>(_reducedSize, _reducedSize, nnzEstimate);

                for (var original = 1; original < nodeCount; original++)
                {
                    coo.At(original - 1, original - 1, degree[original]);
                }

                for (var k = 0; k < edges.Count; k++)
                {
                    var a = edges[k].i;
                    var b = edges[k].j;
                    if (a == 0 || b == 0) continue;
                    coo.At(a - 1, b - 1, -1.0);
                    coo.At(b - 1, a - 1, -1.0);
                }

                return (CSparse.Double.SparseMatrix)CSparse.Double.SparseMatrix.OfIndexed(coo, false);
            }

            #endregion
        }

        // 段階B: 毎ティック呼ぶ。負荷ベクトルbから電位φを解き、各エッジのトルクを返す
        // Phase B: called per tick. Solve φ from load b, output per-edge torque
        public void Solve(double[] b, double[] torqueOut)
        {
            for (var i = 0; i < _reducedSize; i++)
            {
                _bReduced[i] = b[i + 1];
            }

            _chol.Solve(_bReduced, _phiReduced);

            _phiFull[0] = 0.0;
            for (var i = 0; i < _reducedSize; i++)
            {
                _phiFull[i + 1] = _phiReduced[i];
            }

            for (var k = 0; k < _edgeI.Length; k++)
            {
                torqueOut[k] = _phiFull[_edgeI[k]] - _phiFull[_edgeJ[k]];
            }
        }

        // 各ノードについて、隣接エッジトルクの絶対値の最大値を返す（破壊判定用の局所荷重）
        // For each node, the max |torque| over adjacent edges (local load for breakage check)
        public void AggregateNodeMaxAdjacent(double[] torque, double[] nodeMaxOut)
        {
            for (var i = 0; i < nodeMaxOut.Length; i++) nodeMaxOut[i] = 0.0;
            for (var k = 0; k < _edgeI.Length; k++)
            {
                var t = torque[k];
                var a = t >= 0 ? t : -t;
                if (a > nodeMaxOut[_edgeI[k]]) nodeMaxOut[_edgeI[k]] = a;
                if (a > nodeMaxOut[_edgeJ[k]]) nodeMaxOut[_edgeJ[k]] = a;
            }
        }

    }
}
