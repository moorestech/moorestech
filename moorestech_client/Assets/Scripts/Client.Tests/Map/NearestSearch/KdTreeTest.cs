using System.Collections.Generic;
using Client.Game.InGame.Map.NearestSearch;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Map.NearestSearch
{
    /// <summary>
    ///     k-d treeの最寄り結果が線形全走査と一致することを検証
    ///     Verifies k-d tree nearest results match a linear full scan
    /// </summary>
    public class KdTreeTest
    {
        [Test]
        public void ランダム点群で線形走査と最寄りが一致する()
        {
            // 決定論のため固定シード。5km四方×高低差を模した分布
            // Fixed seed for determinism; spread mimics the 5km world with height variation
            var random = new System.Random(20260823);
            var targets = new List<NearestSearchTestTarget>();
            for (var i = 0; i < 2000; i++) targets.Add(new NearestSearchTestTarget(RandomPoint(random)));
            var tree = new KdTree<NearestSearchTestTarget>(targets);

            for (var i = 0; i < 500; i++)
            {
                var query = RandomPoint(random);
                var expected = LinearNearest(targets, query);
                var actual = tree.SearchNearest(query);
                Assert.AreSame(expected, actual, $"query={query}");
            }
        }

        [Test]
        public void 同距離タイでも距離は線形走査と一致する()
        {
            // 原点対称の8点。どれが返っても距離は同じ
            // Eight origin-symmetric points; whichever returns, the distance is equal
            var targets = new List<NearestSearchTestTarget>();
            for (var x = -1; x <= 1; x += 2)
            for (var y = -1; y <= 1; y += 2)
            for (var z = -1; z <= 1; z += 2)
                targets.Add(new NearestSearchTestTarget(new Vector3(x, y, z)));
            var tree = new KdTree<NearestSearchTestTarget>(targets);

            var query = Vector3.zero;
            var expectedDistance = (LinearNearest(targets, query).Position - query).sqrMagnitude;
            var actual = tree.SearchNearest(query);
            Assert.IsNotNull(actual);
            Assert.AreEqual(expectedDistance, (actual.Position - query).sqrMagnitude, 1e-6f);
        }

        [Test]
        public void 一件だけの木はその一件を返す()
        {
            var only = new NearestSearchTestTarget(new Vector3(10f, 0f, -5f));
            var tree = new KdTree<NearestSearchTestTarget>(new[] { only });
            Assert.AreSame(only, tree.SearchNearest(new Vector3(-999f, 50f, 999f)));
        }

        [Test]
        public void 空の木はnullを返す()
        {
            var tree = new KdTree<NearestSearchTestTarget>(new List<NearestSearchTestTarget>());
            Assert.AreEqual(0, tree.Count);
            Assert.IsNull(tree.SearchNearest(Vector3.zero));
        }

        [Test]
        public void 分割面の反対側にある真の最寄りを取りこぼさない()
        {
            // x軸中央値で分割されるよう配置し、クエリは分割面のすぐ左・最寄りは面のすぐ右に置く
            // Arrange so the root splits on x; query sits just left of the plane, the true nearest just right of it
            var targets = new List<NearestSearchTestTarget>
            {
                new(new Vector3(-100f, 0f, 0f)),
                new(new Vector3(-50f, 0f, 0f)),
                new(new Vector3(0f, 0f, 0f)),
                new(new Vector3(0.5f, 0f, 0f)),
                new(new Vector3(100f, 0f, 0f)),
            };
            var tree = new KdTree<NearestSearchTestTarget>(targets);
            var query = new Vector3(0.3f, 0f, 0f);
            Assert.AreSame(LinearNearest(targets, query), tree.SearchNearest(query));
        }

        [Test]
        public void 同一座標が多数あっても構築と探索が成立する()
        {
            // 露頭は同一AABB中心に複数鉱脈が重なりうる。同座標の連続で再帰が破綻しないこと
            // Outcrops can stack on one AABB center; runs of identical coordinates must not break recursion
            var targets = new List<NearestSearchTestTarget>();
            for (var i = 0; i < 300; i++) targets.Add(new NearestSearchTestTarget(new Vector3(7f, 7f, 7f)));
            targets.Add(new NearestSearchTestTarget(new Vector3(8f, 7f, 7f)));
            var tree = new KdTree<NearestSearchTestTarget>(targets);
            Assert.AreSame(targets[300], tree.SearchNearest(new Vector3(9f, 7f, 7f)));
        }

        private static Vector3 RandomPoint(System.Random random)
        {
            return new Vector3(
                (float)(random.NextDouble() * 5000.0 - 2500.0),
                (float)(random.NextDouble() * 200.0),
                (float)(random.NextDouble() * 5000.0 - 2500.0));
        }

        private static NearestSearchTestTarget LinearNearest(List<NearestSearchTestTarget> targets, Vector3 query)
        {
            NearestSearchTestTarget nearest = null;
            var nearestSqr = float.MaxValue;
            foreach (var target in targets)
            {
                var sqr = (target.Position - query).sqrMagnitude;
                if (nearestSqr <= sqr) continue;
                nearest = target;
                nearestSqr = sqr;
            }

            return nearest;
        }
    }
}
