using System;
using Client.Game.InGame.Map.NearestSearch;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Map.NearestSearch
{
    /// <summary>
    ///     guid別索引の分離と、墓標による除外・組み直しを検証
    ///     Verifies per-guid separation plus tombstone exclusion and rebuilds
    /// </summary>
    public class NearestTargetIndexTest
    {
        private static readonly Guid TreeGuid = new("00000000-0000-0000-0000-000000000001");
        private static readonly Guid RockGuid = new("00000000-0000-0000-0000-000000000002");

        [Test]
        public void 別guidの近い個体は返さない()
        {
            var index = new NearestTargetIndex<NearestSearchTestTarget>();
            var farTree = new NearestSearchTestTarget(new Vector3(100f, 0f, 0f));
            var nearRock = new NearestSearchTestTarget(new Vector3(1f, 0f, 0f));
            index.Register(TreeGuid, farTree);
            index.Register(RockGuid, nearRock);

            Assert.IsTrue(index.TrySearchNearest(TreeGuid, Vector3.zero, out var tree, out _));
            Assert.AreSame(farTree, tree);
            Assert.IsTrue(index.TrySearchNearest(RockGuid, Vector3.zero, out var rock, out _));
            Assert.AreSame(nearRock, rock);
        }

        [Test]
        public void 未登録guidは探索が成立しない()
        {
            var index = new NearestTargetIndex<NearestSearchTestTarget>();
            Assert.IsFalse(index.TrySearchNearest(TreeGuid, Vector3.zero, out _, out _));
        }

        [Test]
        public void 登録後に探索不能となった個体は返らない()
        {
            var index = new NearestTargetIndex<NearestSearchTestTarget>();
            var near = new NearestSearchTestTarget(new Vector3(1f, 0f, 0f));
            var far = new NearestSearchTestTarget(new Vector3(50f, 0f, 0f));
            index.Register(TreeGuid, near);
            index.Register(TreeGuid, far);
            Assert.IsTrue(index.TrySearchNearest(TreeGuid, Vector3.zero, out var first, out _));
            Assert.AreSame(near, first);

            // 墓標を通知して組み直しを促しても、通知が無くても結果は同じになること
            // Notifying the tombstone schedules a rebuild, and the result must match what a search without it would give
            near.SetSearchable(false);
            index.NotifyTargetUnsearchable(TreeGuid);
            Assert.IsTrue(index.TrySearchNearest(TreeGuid, Vector3.zero, out var second, out var secondSqrDistance));
            Assert.AreSame(far, second);
            Assert.AreEqual(2500f, secondSqrDistance, 1e-2f);

            far.SetSearchable(false);
            Assert.IsFalse(index.TrySearchNearest(TreeGuid, Vector3.zero, out _, out _));
        }

        [Test]
        public void 墓標を通知しなくても探索から除外される()
        {
            var index = new NearestTargetIndex<NearestSearchTestTarget>();
            var near = new NearestSearchTestTarget(new Vector3(1f, 0f, 0f));
            var far = new NearestSearchTestTarget(new Vector3(50f, 0f, 0f));
            index.Register(TreeGuid, near);
            index.Register(TreeGuid, far);
            Assert.IsTrue(index.TrySearchNearest(TreeGuid, Vector3.zero, out _, out _));

            near.SetSearchable(false);
            Assert.IsTrue(index.TrySearchNearest(TreeGuid, Vector3.zero, out var actual, out _));
            Assert.AreSame(far, actual);
        }

        [Test]
        public void 探索後の追加登録も次の探索で拾われる()
        {
            var index = new NearestTargetIndex<NearestSearchTestTarget>();
            var far = new NearestSearchTestTarget(new Vector3(50f, 0f, 0f));
            index.Register(TreeGuid, far);
            Assert.IsTrue(index.TrySearchNearest(TreeGuid, Vector3.zero, out var first, out _));
            Assert.AreSame(far, first);

            var near = new NearestSearchTestTarget(new Vector3(1f, 0f, 0f));
            index.Register(TreeGuid, near);
            Assert.IsTrue(index.TrySearchNearest(TreeGuid, Vector3.zero, out var second, out _));
            Assert.AreSame(near, second);
        }

        [Test]
        public void 少数追記は木と線形候補を横断して最近傍を返す()
        {
            var index = new NearestTargetIndex<NearestSearchTestTarget>();
            for (var offset = 0; offset < 20; offset++)
                index.Register(TreeGuid, new NearestSearchTestTarget(new Vector3(100f + offset, 0f, 0f)));
            Assert.IsTrue(index.TrySearchNearest(TreeGuid, Vector3.zero, out _, out _));

            var appended = new NearestSearchTestTarget(new Vector3(1f, 0f, 0f));
            index.Register(TreeGuid, appended);

            Assert.IsTrue(index.TrySearchNearest(TreeGuid, Vector3.zero, out var actual, out _));
            Assert.AreSame(appended, actual);
        }
    }
}
