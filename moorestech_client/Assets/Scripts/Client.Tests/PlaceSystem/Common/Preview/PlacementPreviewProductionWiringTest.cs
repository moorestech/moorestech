#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using Client.Common;
using Client.Game.InGame.Block;
using Client.Tests.EditModeInPlayingTest.Util;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.PlaceSystem.Common.Preview
{
    public class PlacementPreviewProductionWiringTest
    {
        private PlacementPreviewPhysicsFixture _fixture;

        [TearDown]
        public void TearDown()
        {
            if (_fixture != null) _fixture.Dispose();
            _fixture = null;
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);
        }

        [UnityTest]
        public IEnumerator ProductionPoolConfiguresCollidersAndReusesOneContactSource()
        {
            EditModeInPlayingTestUtil.EnterPlayModeUtil();
            yield return new EnterPlayMode(expectDomainReload: true);
            _fixture = new PlacementPreviewPhysicsFixture();
            _fixture.Initialize();
            var preview = _fixture.SetPreview(Vector3Int.zero, true);

            // テスト側で設定を代行せずCreatorとcontrollerの両配線を守る
            // Protect both Creator and controller wiring without configuring in the test.
            foreach (var collider in preview.GetComponentsInChildren<Collider>(true))
            {
                Assert.IsTrue(collider.isTrigger);
                Assert.AreEqual(LayerConst.BlockLayer, collider.gameObject.layer);
            }
            var detector = preview.GetComponentsInChildren<GroundCollisionDetector>(true).Single();
            Assert.IsTrue(detector.GetComponent<Rigidbody>().isKinematic);
            Assert.IsNull(preview.GetComponent<Rigidbody>());

            // 非表示から再表示しても同一個体と接触源を再利用する
            // Reuse the same preview and contact source after hiding and showing.
            _fixture.Controller.SetActive(false);
            _fixture.Controller.SetActive(true);
            Assert.AreSame(preview, _fixture.SetPreview(Vector3Int.zero, true));
            Assert.AreSame(detector, preview.GetComponentsInChildren<GroundCollisionDetector>(true).Single());
            _fixture.Dispose();
            _fixture = null;
            yield return new ExitPlayMode();
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);
        }

        [UnityTest]
        public IEnumerator ProductionControllerReportsFirstContactStayAndClear()
        {
            EditModeInPlayingTestUtil.EnterPlayModeUtil();
            yield return new EnterPlayMode(expectDomainReload: true);
            _fixture = new PlacementPreviewPhysicsFixture();
            _fixture.Initialize();
            var preview = _fixture.SetPreview(Vector3Int.zero, true);
            var detector = preview.GetComponentsInChildren<GroundCollisionDetector>(true).Single();
            var contact = detector.GetComponent<BoxCollider>();
            var ground = _fixture.CreateGround();

            // 同時StayでEnterの欠落が隠れないよう入口も個別に検査する
            // Check Enter separately so concurrent Stay cannot hide its absence.
            detector.SendMessage("OnTriggerEnter", ground);
            Assert.IsTrue(detector.IsCollision);
            Assert.IsTrue(preview.IsCollisionGround);
            CollectionAssert.AreEqual(new[] { false }, _fixture.Controller.DetectGroundOverlaps());

            // 実物理の初接触と継続をそれぞれ一ステップ後に読む
            // Read first physical contact and persistence after individual steps.
            _fixture.Step();
            CollectionAssert.AreEqual(new[] { false }, _fixture.Controller.DetectGroundOverlaps());
            ground.transform.position = contact.bounds.center;
            _fixture.Step();
            Assert.IsTrue(detector.IsCollision);
            Assert.IsTrue(preview.IsCollisionGround);
            CollectionAssert.AreEqual(new[] { true }, _fixture.Controller.DetectGroundOverlaps());

            // 接触継続・離脱の結果伝達を検査
            // Check propagation of persistent and cleared contact.
            _fixture.Step();
            CollectionAssert.AreEqual(new[] { true }, _fixture.Controller.DetectGroundOverlaps());
            ground.transform.position = Vector3.one * 100f;
            _fixture.Step();
            Assert.IsFalse(detector.IsCollision);
            Assert.IsFalse(preview.IsCollisionGround);
            CollectionAssert.AreEqual(new[] { false }, _fixture.Controller.DetectGroundOverlaps());
            _fixture.Dispose();
            _fixture = null;
            yield return new ExitPlayMode();
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);
        }

        [UnityTest]
        public IEnumerator CurrentPoseQueryDoesNotWaitForTheNextPhysicsStep()
        {
            EditModeInPlayingTestUtil.EnterPlayModeUtil();
            yield return new EnterPlayMode(expectDomainReload: true);
            _fixture = new PlacementPreviewPhysicsFixture();
            _fixture.Initialize();
            var preview = _fixture.SetPreview(Vector3Int.zero, true);
            var ground = _fixture.CreateGround();
            _fixture.Step();
            Assert.IsFalse(preview.IsCollisionGround);

            // 同じプール個体を地形へ動かし、物理更新前に同期値を読む
            // Move the same pooled instance into ground and query before physics advances.
            var moved = _fixture.SetPreview(new Vector3Int(4, 0, 0), true);
            Physics.SyncTransforms();
            ground.transform.position = moved.GetComponentsInChildren<GroundCollisionDetector>(true)
                .Single().GetComponent<BoxCollider>().bounds.center;
            Assert.AreSame(preview, moved);
            Assert.IsFalse(preview.IsCollisionGround);
            CollectionAssert.AreEqual(new[] { true }, _fixture.Controller.DetectGroundOverlaps());

            // 逆向き移動も同じフレームで旧通知を使わない
            // Reverse movement likewise must not reuse the prior notification.
            _fixture.Step();
            Assert.IsTrue(preview.IsCollisionGround);
            _fixture.SetPreview(Vector3Int.zero, true);
            Assert.IsTrue(preview.IsCollisionGround);
            CollectionAssert.AreEqual(new[] { false }, _fixture.Controller.DetectGroundOverlaps());
            _fixture.Dispose();
            _fixture = null;
            yield return new ExitPlayMode();
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);
        }

        [UnityTest]
        public IEnumerator NormalModeNeverCreatesContactSourceAndReuseTogglesOwnedSource()
        {
            EditModeInPlayingTestUtil.EnterPlayModeUtil();
            yield return new EnterPlayMode(expectDomainReload: true);
            _fixture = new PlacementPreviewPhysicsFixture();
            _fixture.Initialize();
            var preview = _fixture.SetPreview(Vector3Int.zero, false);
            Assert.IsEmpty(preview.GetComponentsInChildren<GroundCollisionDetector>(true));
            Assert.IsEmpty(preview.GetComponentsInChildren<Rigidbody>(true));
            var visual = preview.GetComponent<BoxCollider>();
            Assert.IsTrue(visual.enabled);

            _fixture.SetPreview(Vector3Int.zero, true);
            var owned = preview.GetComponentsInChildren<GroundCollisionDetector>(true).Single();
            Assert.IsTrue(owned.gameObject.activeSelf);
            _fixture.SetPreview(Vector3Int.zero, false);
            Assert.IsFalse(owned.gameObject.activeSelf);
            Assert.IsTrue(visual.enabled);
            Assert.IsFalse(owned.GetComponent<Rigidbody>().gameObject.activeInHierarchy);
            _fixture.SetPreview(Vector3Int.zero, true);
            Assert.AreSame(owned, preview.GetComponentsInChildren<GroundCollisionDetector>(true).Single());
            Assert.IsTrue(owned.gameObject.activeSelf);
            _fixture.Dispose();
            _fixture = null;
            yield return new ExitPlayMode();
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);
        }
    }
}
#endif
