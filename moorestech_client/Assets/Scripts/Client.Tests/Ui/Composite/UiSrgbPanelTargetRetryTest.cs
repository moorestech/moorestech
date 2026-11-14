using System.Text.RegularExpressions;
using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Composite;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Composite
{
    /// <summary>
    /// sRGB 合成の RT を作り直せないときは前の RT のまま投げ、次の呼びでも作り直しを試みること（L1。S5-gate bug-pass W1）
    /// When the sRGB composite RT cannot be recreated, the previous RT stays and it throws, and the next call retries (L1, S5-gate bug-pass W1)
    /// </summary>
    public class UiSrgbPanelTargetRetryTest
    {
        private const string PanelSettingsPath = "Assets/UI Toolkit/GameUi/GameUiPanelSettings.asset";
        private const int FirstSize = 64;
        private const int UncreatableSize = 100000;
        private static readonly Regex EditModeDestroyError = new("Destroy may not be called from edit mode");

        private GameObject _documentObject;
        private UiSrgbPanelTarget _target;
        private RenderTexture _first;

        [SetUp]
        public void SetUp()
        {
            GraphicsDeviceRequirement.IgnoreWhenNoGraphicsDevice();

            // 本番の PanelSettings を当てた UIDocument で作り、寸法の決まった 1 枚目を作らせる
            // Build on a UIDocument with the production PanelSettings and make a first RT of a known size
            _documentObject = new GameObject("UiSrgbPanelTargetRetryTest");
            var document = _documentObject.AddComponent<UIDocument>();
            document.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            _target = new UiSrgbPanelTarget(document);
            ExpectEditModeDestroyWhenPreviousExists();
            _target.SyncToSize(FirstSize, FirstSize);
            Assert.IsTrue(UiSrgbCompositeSource.TryGetTexture(out _first), "first RT");
            Assert.AreEqual(FirstSize, _first.width, "first RT width");
        }

        [TearDown]
        public void TearDown()
        {
            if (_target == null) return;

            // EditMode の Destroy は消さないので、Dispose の後に DestroyImmediate で片づける
            // Destroy does not destroy in EditMode, so clean up with DestroyImmediate after Dispose
            var clonedSettings = _documentObject.GetComponent<UIDocument>().panelSettings;
            LogAssert.Expect(LogType.Error, EditModeDestroyError);
            LogAssert.Expect(LogType.Error, EditModeDestroyError);
            _target.Dispose();
            Object.DestroyImmediate(_first);
            Object.DestroyImmediate(clonedSettings);
            Object.DestroyImmediate(_documentObject);
            _target = null;
        }

        [Test]
        public void 作れない大きさでは例外を投げ前のRTを合成に使い続ける()
        {
            ExpectFailedCreation();
            Assert.Throws<UiSrgbPanelRenderTextureUnavailableException>(() => _target.SyncToSize(UncreatableSize, UncreatableSize));

            Assert.IsTrue(UiSrgbCompositeSource.TryGetTexture(out var current), "composite RT after failure");
            Assert.AreSame(_first, current);
            Assert.IsTrue(_first.IsCreated(), "first RT still created");
        }

        [Test]
        public void 作れなかった大きさをもう一度渡すと寸法一致で黙らず再び作り直しを試みる()
        {
            ExpectFailedCreation();
            Assert.Throws<UiSrgbPanelRenderTextureUnavailableException>(() => _target.SyncToSize(UncreatableSize, UncreatableSize));
            ExpectFailedCreation();
            Assert.Throws<UiSrgbPanelRenderTextureUnavailableException>(() => _target.SyncToSize(UncreatableSize, UncreatableSize));

            Assert.IsTrue(UiSrgbCompositeSource.TryGetTexture(out var current), "composite RT after retries");
            Assert.AreSame(_first, current);
        }

        // 作れない RT の Unity のエラーと、捨てる RT の EditMode の Destroy のエラーを受ける
        // Accept Unity's error for the uncreatable RT and the EditMode Destroy error for the dropped RT
        private static void ExpectFailedCreation()
        {
            LogAssert.Expect(LogType.Error, new Regex("RenderTexture|[Tt]exture"));
            LogAssert.Expect(LogType.Error, EditModeDestroyError);
        }

        // 作るときに画面の大きさで RT ができていれば、それを捨てる Destroy のエラーを受ける
        // If construction made an RT at the screen size, accept the Destroy error for dropping it
        private static void ExpectEditModeDestroyWhenPreviousExists()
        {
            if (Screen.width > 0 && Screen.height > 0 && (Screen.width != FirstSize || Screen.height != FirstSize)) LogAssert.Expect(LogType.Error, EditModeDestroyError);
        }
    }
}
