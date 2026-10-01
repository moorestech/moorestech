using Client.Game.InGame.Block;
using Client.Game.InGame.Interact.Selection;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Interact.Reach
{
    /// <summary>
    ///     開いた対象へ届くかと、届かない理由の判別を検証
    ///     Verifies whether an opened target is reachable and which reason it is not
    /// </summary>
    public class InteractReachQueryTest : InteractTargetSelectorTestFixture
    {
        private const float BoundaryMargin = 0.05f;

        [Test]
        public void ブロックはインタラクト距離内なら届き外なら届かない()
        {
            var block = CreateOpenableBlockTarget(new Vector3(1.5f, 0f, 0f));
            var query = new InteractReachQuery();

            Assert.AreEqual(InteractReachResult.Reachable, query.QueryReach(block, Vector3.zero));
            Assert.AreEqual(InteractReachResult.OutOfRange, query.QueryReach(block, new Vector3(-1.5f, 0f, 0f)));
        }

        [Test]
        public void 対象の面からインタラクト距離の内側は届き外側は届かない()
        {
            var block = CreateOpenableBlockTarget(Vector3.zero);
            var query = new InteractReachQuery();

            // 当たり判定の大きさに依らないよう、自機側を向いた面からの距離で境界を挟む
            // Bracket the boundary by distance from the face toward the player so the collider size never matters
            var faceX = block.GetComponentInChildren<Collider>().bounds.min.x;
            var insidePosition = new Vector3(faceX - (InteractOverlap.InteractDistance - BoundaryMargin), 0f, 0f);
            var outsidePosition = new Vector3(faceX - (InteractOverlap.InteractDistance + BoundaryMargin), 0f, 0f);

            Assert.AreEqual(InteractReachResult.Reachable, query.QueryReach(block, insidePosition));
            Assert.AreEqual(InteractReachResult.OutOfRange, query.QueryReach(block, outsidePosition));
        }

        [Test]
        public void 列車が発車して離れると届かなくなる()
        {
            var car = CreateTrainCarTarget(new Vector3(1.5f, 0f, 0f));
            var query = new InteractReachQuery();
            Assert.AreEqual(InteractReachResult.Reachable, query.QueryReach(car, Vector3.zero));

            // 自機は動かさず対象側だけを動かす
            // Only the target moves while the player stays put
            car.transform.position = new Vector3(5f, 0f, 0f);
            Physics.SyncTransforms();

            Assert.AreEqual(InteractReachResult.OutOfRange, query.QueryReach(car, Vector3.zero));
        }

        [Test]
        public void 近くに別の対象があっても開いた対象が遠ければ届かない()
        {
            var openedBlock = CreateOpenableBlockTarget(new Vector3(5f, 0f, 0f));
            CreateOpenableBlockTarget(new Vector3(1f, 0f, 0f));
            var query = new InteractReachQuery();

            Assert.AreEqual(InteractReachResult.OutOfRange, query.QueryReach(openedBlock, Vector3.zero));
        }

        [Test]
        public void 距離内でも対話不能になった対象には届かない()
        {
            var block = CreateOpenableBlockTarget(new Vector3(1f, 0f, 0f));
            var query = new InteractReachQuery();
            Assert.AreEqual(InteractReachResult.Reachable, query.QueryReach(block, Vector3.zero));

            // 撤去済みの墓標になると距離内でも候補から外れる
            // Once tombstoned as removed, it leaves the candidates even within distance
            block.GetComponent<BlockGameObject>().MarkUnsearchable();

            Assert.AreEqual(InteractReachResult.NotInteractable, query.QueryReach(block, Vector3.zero));
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

            Assert.AreEqual(InteractReachResult.TargetDestroyed, query.QueryReach(block, Vector3.zero));
        }
    }
}
