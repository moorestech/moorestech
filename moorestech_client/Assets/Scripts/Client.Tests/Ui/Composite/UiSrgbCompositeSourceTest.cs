using System.Reflection;
using Client.Ui.Foundation.Composite;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Client.Tests.Ui.Composite
{
    /// <summary>
    /// 重ねるカメラは、画面へ出す base のうち depth 最大のものとそのスタックの overlay だけであること（L1。A-180）
    /// Only the screen base camera with the largest depth and the overlays of its stack composite the UI (L1, A-180)
    /// </summary>
    public class UiSrgbCompositeSourceTest
    {
        private GameObject _root;
        private RenderTexture _panel;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("UiSrgbCompositeSourceTest");
            _panel = new RenderTexture(4, 4, 0);
            UiSrgbCompositeSource.SetTexture(_panel);
            // 同じフレーム内の選定キャッシュを捨てる
            // Drop the per-frame choice cache
            typeof(UiSrgbCompositeSource).GetField("_chosenFrame", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, -1);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            UiSrgbCompositeSource.ClearTexture(_panel);
            Object.DestroyImmediate(_panel);
        }

        [Test]
        public void depth最大の画面向けbaseとそのスタックのoverlayだけが重ねる()
        {
            var lowBase = NewCamera("low", 0, CameraRenderType.Base);
            var highBase = NewCamera("high", 5, CameraRenderType.Base);
            var overlay = NewCamera("overlay", 0, CameraRenderType.Overlay);
            var rtBase = NewCamera("rt", 9, CameraRenderType.Base);
            rtBase.targetTexture = _panel;
            highBase.GetUniversalAdditionalCameraData().cameraStack.Add(overlay);

            Assert.IsTrue(UiSrgbCompositeSource.IsCompositingCamera(highBase), "high base");
            Assert.IsFalse(UiSrgbCompositeSource.IsCompositingCamera(lowBase), "low base");
            Assert.IsFalse(UiSrgbCompositeSource.IsCompositingCamera(rtBase), "RT camera");
            Assert.IsTrue(UiSrgbCompositeSource.IsCompositingCamera(overlay), "overlay of the stack");
        }

        private Camera NewCamera(string cameraName, float depth, CameraRenderType renderType)
        {
            var child = new GameObject(cameraName);
            child.transform.SetParent(_root.transform);
            var camera = child.AddComponent<Camera>();
            camera.depth = depth;
            child.AddComponent<UniversalAdditionalCameraData>().renderType = renderType;
            return camera;
        }
    }
}
