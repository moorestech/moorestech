using Client.Game.InGame.Tutorial;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.WebUi
{
    public class WorldPinScreenProjectionTest
    {
        private Camera _camera;

        [SetUp]
        public void SetUp()
        {
            // 原点から+Z方向を向く素のカメラで射影の座標系変換を検証する
            // A plain camera at the origin facing +Z verifies the projection's axis conversions
            _camera = new GameObject("TestCamera").AddComponent<Camera>();
            _camera.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_camera.gameObject);
        }

        [Test]
        public void FrontTargetIsOnScreenWithCssTopLeftOrigin()
        {
            var world = _camera.transform.position + _camera.transform.forward * 10f;
            var projection = WorldPinScreenProjection.Project(_camera, world);

            var viewport = _camera.WorldToViewportPoint(world);
            Assert.IsTrue(projection.OnScreen);
            Assert.AreEqual(viewport.x, projection.ScreenX, 0.0001f);
            // CSS座標系は左上原点のためYを反転していること
            // Y must be flipped because CSS uses a top-left origin
            Assert.AreEqual(1f - viewport.y, projection.ScreenY, 0.0001f);
        }

        [Test]
        public void UpperRightTargetHasMatchingCssDirection()
        {
            var world = _camera.transform.position + (_camera.transform.forward + _camera.transform.right + _camera.transform.up) * 10f;
            var projection = WorldPinScreenProjection.Project(_camera, world);

            // 右上方向のターゲットはCSS座標系で「右(+X)・上(-Y)」を指すこと
            // An upper-right target must point right (+X) and up (-Y) in CSS axes
            Assert.Greater(projection.DirectionX, 0f);
            Assert.Less(projection.DirectionY, 0f);
        }

        [Test]
        public void BehindTargetIsOffScreen()
        {
            var world = _camera.transform.position - _camera.transform.forward * 10f;
            var projection = WorldPinScreenProjection.Project(_camera, world);

            Assert.IsFalse(projection.OnScreen);
        }

        [Test]
        public void ForwardAxisDegenerateDirectionFallsBackToScreenDown()
        {
            // カメラ前後軸上のターゲットは方向ゼロに縮退するため画面下方向へフォールバックすること
            // Targets on the camera's forward axis degenerate to zero direction and must fall back to screen-down
            var world = _camera.transform.position - _camera.transform.forward * 10f;
            var projection = WorldPinScreenProjection.Project(_camera, world);

            Assert.AreEqual(0f, projection.DirectionX, 0.0001f);
            Assert.AreEqual(1f, projection.DirectionY, 0.0001f);
        }
    }
}
