using Client.Game.InGame.Block;
using Client.Game.InGame.Interact.Selection;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Interact.Reach
{
    /// <summary>
    ///     開いた対象がまだインタラクト距離内にあるかの判定を検証
    ///     Verifies whether an opened target is still within the interact distance
    /// </summary>
    public class InteractReachQueryTest : InteractTargetSelectorTestFixture
    {
        [Test]
        public void ブロックはインタラクト距離内なら届き外なら届かない()
        {
            var block = CreateOpenableBlockTarget(new Vector3(1.5f, 0f, 0f));
            var query = new InteractReachQuery();

            Assert.IsTrue(query.IsWithinReach(block, Vector3.zero));
            Assert.IsFalse(query.IsWithinReach(block, new Vector3(-1.5f, 0f, 0f)));
        }

        [Test]
        public void インタラクト距離の境界の内側は届き外側は届かない()
        {
            var inside = CreateOpenableBlockTarget(new Vector3(InteractTargetSelector.InteractDistance - 0.1f, 0f, 0f));
            var outside = CreateOpenableBlockTarget(new Vector3(0f, 0f, InteractTargetSelector.InteractDistance + 0.1f));
            var query = new InteractReachQuery();

            Assert.IsTrue(query.IsWithinReach(inside, Vector3.zero));
            Assert.IsFalse(query.IsWithinReach(outside, Vector3.zero));
        }

        [Test]
        public void 列車が発車して離れると届かなくなる()
        {
            var car = CreateTrainCarTarget(new Vector3(1.5f, 0f, 0f));
            var query = new InteractReachQuery();
            Assert.IsTrue(query.IsWithinReach(car, Vector3.zero));

            // 自機は動かさず対象側だけを動かす
            // Only the target moves while the player stays put
            car.transform.position = new Vector3(5f, 0f, 0f);
            Physics.SyncTransforms();

            Assert.IsFalse(query.IsWithinReach(car, Vector3.zero));
        }

        [Test]
        public void 近くに別の対象があっても開いた対象が遠ければ届かない()
        {
            var openedBlock = CreateOpenableBlockTarget(new Vector3(5f, 0f, 0f));
            CreateOpenableBlockTarget(new Vector3(1f, 0f, 0f));
            var query = new InteractReachQuery();

            Assert.IsFalse(query.IsWithinReach(openedBlock, Vector3.zero));
        }

        [Test]
        public void 距離内でも対話不能になった対象には届かない()
        {
            var block = CreateOpenableBlockTarget(new Vector3(1f, 0f, 0f));
            var query = new InteractReachQuery();
            Assert.IsTrue(query.IsWithinReach(block, Vector3.zero));

            // 撤去済みの墓標になると距離内でも候補から外れる
            // Once tombstoned as removed, it leaves the candidates even within distance
            block.GetComponent<BlockGameObject>().MarkUnsearchable();

            Assert.IsFalse(query.IsWithinReach(block, Vector3.zero));
        }

        [Test]
        public void 破棄された対象には届かない()
        {
            var block = CreateOpenableBlockTarget(new Vector3(1f, 0f, 0f));
            var query = new InteractReachQuery();

            // TearDownが破棄済みを二重に破棄しないよう、後始末の対象から外してから壊す
            // Drop it from the cleanup list first so TearDown never destroys it twice
            TargetObjects.Remove(block.gameObject);
            Object.DestroyImmediate(block.gameObject);
            Physics.SyncTransforms();

            Assert.IsFalse(query.IsWithinReach(block, Vector3.zero));
        }
    }
}
