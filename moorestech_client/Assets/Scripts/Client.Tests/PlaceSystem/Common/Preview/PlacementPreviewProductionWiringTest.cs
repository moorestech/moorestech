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
            var preview = _fixture.SetPreview();

            // テスト側で設定を代行せずCreatorとcontrollerの両配線を守る
            // Protect both Creator and controller wiring without configuring in the test.
            foreach (var collider in preview.GetComponentsInChildren<Collider>(true))
            {
                Assert.IsTrue(collider.isTrigger);
                Assert.AreEqual(LayerConst.BlockLayer, collider.gameObject.layer);
            }
            var detector = preview.GetComponentsInChildren<GroundCollisionDetector>(true).Single();
            Assert.IsTrue(preview.GetComponent<Rigidbody>().isKinematic);

            // 非表示から再表示しても同一個体と接触源を再利用する
            // Reuse the same preview and contact source after hiding and showing.
            _fixture.Controller.SetActive(false);
            _fixture.Controller.SetActive(true);
            Assert.AreSame(preview, _fixture.SetPreview());
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
            var preview = _fixture.SetPreview();
            var detector = preview.GetComponentsInChildren<GroundCollisionDetector>(true).Single();
            var contact = detector.GetComponent<BoxCollider>();
            var ground = _fixture.CreateGround();

            // 同時StayでEnterの欠落が隠れないよう入口も個別に検査する
            // Check Enter separately so concurrent Stay cannot hide its absence.
            detector.SendMessage("OnTriggerEnter", ground);
            Assert.IsTrue(detector.IsCollision);
            Assert.IsTrue(preview.IsCollisionGround);
            CollectionAssert.AreEqual(new[] { true }, _fixture.Controller.DetectGroundOverlaps());

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
    }
}
#endif
