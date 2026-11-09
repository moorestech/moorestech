using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Modal;
using Client.Ui.Foundation.Stage.ScaleReading;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Modal
{
    /// <summary>
    /// モーダル宿主の配置と幕を確かめる（L1）
    /// Verifies modal host placement and scrim (L1)
    /// </summary>
    public class UiModalHostTest : UiTestHost
    {
        private const float StageScale = 1.5f;
        private const string ShownClass = "ui-modal-host--shown";
        private UiModalHost _host;
        private VisualElement _layer;

        [SetUp]
        public void MountHost()
        {
            _layer = new VisualElement { name = "modal-test-layer" };
            _layer.style.position = Position.Absolute;
            _layer.style.left = 0;
            _layer.style.top = 0;
            _layer.style.right = 0;
            _layer.style.bottom = 0;
            rootVisualElement.Add(_layer);
            _host = new UiModalHost(new StageScaleSource(new ReactiveProperty<float>(StageScale)));
            _host.Mount(_layer);
            simulate.FrameUpdate();
        }

        [Test]
        public void 隠れている間は幕も中身も無く下へクリックが届く()
        {
            Assert.IsFalse(ScopeShown());
            Assert.AreEqual(DisplayStyle.None, _layer.Q("modal-scope").resolvedStyle.display);
            Assert.AreNotEqual("modal-backdrop", rootVisualElement.panel.Pick(rootVisualElement.worldBound.center)?.name);
        }

        [Test]
        public void 中身は実画面の寸法のまま中央に置かれ幕は画面いっぱい()
        {
            var content = Box("modal-content-a", 440, 232);
            _host.Show(content);
            simulate.FrameUpdate();

            // 実画面 px ＝ パネル px × 倍率（テストのパネルの倍率は 1 なので舞台の倍率を掛けて見る）
            // Real-screen px = panel px x scale (the test panel has scale 1, so multiply by the stage scale)
            Assert.IsTrue(ScopeShown());
            Assert.AreEqual(DisplayStyle.Flex, _layer.Q("modal-scope").resolvedStyle.display);
            Assert.AreEqual(440f, content.worldBound.width * StageScale, 0.1f);
            Assert.AreEqual(232f, content.worldBound.height * StageScale, 0.1f);
            Assert.AreEqual(_layer.worldBound.center.x, content.worldBound.center.x, 0.5f);
            Assert.AreEqual(_layer.worldBound.center.y, content.worldBound.center.y, 0.5f);
            var backdrop = _layer.Q("modal-backdrop");
            Assert.AreEqual(_layer.worldBound.width, backdrop.worldBound.width, 0.5f);
            Assert.AreEqual(_layer.worldBound.height, backdrop.worldBound.height, 0.5f);
            Assert.AreEqual(Color.black.r, backdrop.resolvedStyle.backgroundColor.r, 0.001f);
            Assert.AreEqual(0.8605f, backdrop.resolvedStyle.backgroundColor.a, 0.01f, "--modal-backdrop-face");
        }

        [Test]
        public void 幕の押下は知らせるだけで閉じず次の表示は前の中身を差し替える()
        {
            var pressed = 0;
            using var subscription = _host.OnBackdropClicked.Subscribe(_ => pressed++);
            var first = Box("modal-content-a", 200, 100);
            _host.Show(first);
            simulate.FrameUpdate();

            // 中身の押下は幕の知らせにならず、幕の押下は 1 回だけ知らせる
            // A press on the contents is not a backdrop press; a press on the backdrop reports once
            simulate.Click(first.worldBound.center);
            simulate.Click(new Vector2(_layer.worldBound.xMin + 4f, _layer.worldBound.yMin + 4f));
            Assert.AreEqual(1, pressed);
            Assert.IsTrue(ScopeShown(), "a backdrop press does not close");

            var second = Box("modal-content-b", 200, 100);
            _host.Show(second);
            simulate.FrameUpdate();
            Assert.IsNull(first.parent, "the previous content is removed");
            Assert.IsNotNull(_layer.Q("modal-content-b"));

            _host.Hide();
            simulate.FrameUpdate();
            Assert.IsFalse(ScopeShown());
            Assert.IsNull(second.parent);
            Assert.AreEqual(DisplayStyle.None, _layer.Q("modal-scope").resolvedStyle.display);
        }

        // 表示中かは宿主の状態クラスで見る（宿主は表示の有無を公開しない）
        // Read shown-ness from the host's state class (the host exposes no shown flag)
        private bool ScopeShown()
        {
            return _layer.Q("modal-scope").ClassListContains(ShownClass);
        }

        private static VisualElement Box(string boxName, float width, float height)
        {
            var box = new VisualElement { name = boxName };
            box.style.width = width;
            box.style.height = height;
            return box;
        }
    }
}
