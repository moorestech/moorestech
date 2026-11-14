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
    /// sRGB 合成の RT を作り直せないときは投げずに前の RT のまま LogError を 1 回出し、同じ大きさは試し直さず、大きさが変われば作り直すこと（L1。S5-gate bug-pass W1）
    /// When the sRGB composite RT cannot be recreated it keeps the previous RT and logs one error without throwing, skips the same size, and recreates once the size changes (L1, S5-gate bug-pass W1)
    /// </summary>
    public class UiSrgbPanelTargetRetryTest
    {
        private const string PanelSettingsPath = "Assets/UI Toolkit/GameUi/GameUiPanelSettings.asset";
        private const int FirstSize = 64;
        private const int CreatableSize = 128;
        private const int UncreatableSize = 100000;
        private static readonly Regex EditModeDestroyError = new("Destroy may not be called from edit mode");

        private GameObject _documentObject;
        private UIDocument _document;
        private UiSrgbPanelTarget _target;
        private RenderTexture _constructedTexture;
        private RenderTexture _first;
        private RenderTexture _recreated;

        [SetUp]
        public void SetUp()
        {
            GraphicsDeviceRequirement.IgnoreWhenNoGraphicsDevice();

            // 本番の PanelSettings を当てた UIDocument で作り、寸法の決まった 1 枚目を作らせる
            // Build on a UIDocument with the production PanelSettings and make a first RT of a known size
            _documentObject = new GameObject("UiSrgbPanelTargetRetryTest");
            _document = _documentObject.AddComponent<UIDocument>();
            _document.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            _target = new UiSrgbPanelTarget(_document);

            // 作るときにできた RT が 1 枚目と大きさが違えば、捨てるときの EditMode の Destroy のエラーを受ける
            // If construction made an RT of another size, accept the EditMode Destroy error for dropping it
            UiSrgbCompositeSource.TryGetTexture(out _constructedTexture);
            if (_constructedTexture != null && (_constructedTexture.width != FirstSize || _constructedTexture.height != FirstSize)) LogAssert.Expect(LogType.Error, EditModeDestroyError);
            _target.SyncToSize(FirstSize, FirstSize);
            Assert.IsTrue(UiSrgbCompositeSource.TryGetTexture(out _first), "first RT");
            Assert.AreEqual(FirstSize, _first.width, "first RT width");
        }

        [TearDown]
        public void TearDown()
        {
            if (_target == null) return;

            // EditMode の Destroy は消さないので、Dispose の後に DestroyImmediate で片づける（失敗した候補は参照が無く残る）
            // Destroy does not destroy in EditMode, so clean up with DestroyImmediate after Dispose (failed candidates have no reference and remain)
            var clonedSettings = _document.panelSettings;
            LogAssert.Expect(LogType.Error, EditModeDestroyError);
            LogAssert.Expect(LogType.Error, EditModeDestroyError);
            _target.Dispose();
            DestroyWhenPresent(_recreated);
            DestroyWhenPresent(_first);
            DestroyWhenPresent(_constructedTexture);
            Object.DestroyImmediate(clonedSettings);
            Object.DestroyImmediate(_documentObject);
            _target = null;
        }

        [Test]
        public void 作れない大きさでは投げずに失敗を返し前のRTを使い続ける()
        {
            ExpectFailedCreation(UncreatableSize);
            var result = _target.SyncToSize(UncreatableSize, UncreatableSize);

            Assert.AreEqual(UiSrgbPanelResizeResult.CreationFailed, result);
            AssertCurrentTexture(_first);
            Assert.IsTrue(_first.IsCreated(), "first RT still created");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void 作れなかった大きさをもう一度渡すと試さずログも出さない()
        {
            ExpectFailedCreation(UncreatableSize);
            _target.SyncToSize(UncreatableSize, UncreatableSize);

            var result = _target.SyncToSize(UncreatableSize, UncreatableSize);

            Assert.AreEqual(UiSrgbPanelResizeResult.SkippedFailedSize, result);
            AssertCurrentTexture(_first);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void 作れなかった後に作れる大きさを渡すと作り直して差し替える()
        {
            ExpectFailedCreation(UncreatableSize);
            _target.SyncToSize(UncreatableSize, UncreatableSize);

            LogAssert.Expect(LogType.Error, EditModeDestroyError);
            var result = _target.SyncToSize(CreatableSize, CreatableSize);

            Assert.AreEqual(UiSrgbPanelResizeResult.Recreated, result);
            Assert.IsTrue(UiSrgbCompositeSource.TryGetTexture(out _recreated), "recreated RT");
            Assert.AreNotSame(_first, _recreated);
            Assert.AreEqual(CreatableSize, _recreated.width, "recreated RT width");
            AssertCurrentTexture(_recreated);
        }

        [Test]
        public void 別の大きさを挟んでから作れなかった大きさへ戻ると再び試す()
        {
            ExpectFailedCreation(UncreatableSize);
            _target.SyncToSize(UncreatableSize, UncreatableSize);
            Assert.AreEqual(UiSrgbPanelResizeResult.Unchanged, _target.SyncToSize(FirstSize, FirstSize));

            ExpectFailedCreation(UncreatableSize);
            var result = _target.SyncToSize(UncreatableSize, UncreatableSize);

            Assert.AreEqual(UiSrgbPanelResizeResult.CreationFailed, result);
            AssertCurrentTexture(_first);
        }

        // 合成元と PanelSettings の描き先の両方が期待の RT を指す
        // Both the composite source and the PanelSettings target point at the expected RT
        private void AssertCurrentTexture(RenderTexture expected)
        {
            Assert.IsTrue(UiSrgbCompositeSource.TryGetTexture(out var current), "composite RT");
            Assert.AreSame(expected, current, "composite source");
            Assert.AreSame(expected, _document.panelSettings.targetTexture, "panel target texture");
        }

        // Unity の Create 失敗、捨てる候補の EditMode の Destroy、本体の LogError の 3 本を受ける
        // Accept Unity's Create failure, the EditMode Destroy for the dropped candidate and our LogError
        private static void ExpectFailedCreation(int size)
        {
            LogAssert.Expect(LogType.Error, new Regex(@"RenderTexture\.Create failed"));
            LogAssert.Expect(LogType.Error, EditModeDestroyError);
            LogAssert.Expect(LogType.Error, new Regex($@"\[UiSrgbPanelTarget\] failed to create the {size}x{size} panel RT"));
        }

        private static void DestroyWhenPresent(RenderTexture texture)
        {
            if (texture != null) Object.DestroyImmediate(texture);
        }
    }
}
