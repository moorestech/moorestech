using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Stage;
using Client.Ui.Foundation.Stage.ScaleReading;
using NUnit.Framework;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Stage
{
    /// <summary>
    /// 倍率を打ち消す箱が中身を実画面 px で置き、見かけは親いっぱいになることを確かめる（L1。設計書 A-41）
    /// Verifies the scale-cancelling box lays contents out in real-screen px while looking parent-sized (L1; design A-41)
    /// </summary>
    public class UiScreenPixelScopeTest : UiTestHost
    {
        // テストのパネルは倍率 1 なので、舞台の倍率は StageScaleSource へ外から差し込み、実画面 px は「パネル px × 倍率」で見る
        // The test panel has scale 1, so the stage scale is injected into StageScaleSource and real-screen px are read as panel px x scale
        private ReactiveProperty<float> _stageScale;
        private UiScreenPixelScope _scope;
        private VisualElement _content;

        [SetUp]
        public void MountScope()
        {
            _stageScale = new ReactiveProperty<float>(1f);
            _scope = new UiScreenPixelScope("screen-pixel-scope-test", new StageScaleSource(_stageScale));
            _content = new VisualElement { name = "screen-pixel-scope-content" };
            _content.style.width = 320;
            _content.style.height = 40;
            _scope.Add(_content);
            rootVisualElement.Add(_scope);
            simulate.FrameUpdate();
        }

        [TestCase(1f)]
        [TestCase(1.5f)]
        [TestCase(2f)]
        public void 中身の幅320はどの倍率でも実画面の320pxで箱の見かけは親いっぱい(float stageScale)
        {
            _stageScale.Value = stageScale;
            simulate.FrameUpdate();

            Assert.AreEqual(320f, _content.worldBound.width * stageScale, 0.05f, "content width in real-screen px");
            Assert.AreEqual(rootVisualElement.worldBound.x, _scope.worldBound.x, 0.05f);
            Assert.AreEqual(rootVisualElement.worldBound.y, _scope.worldBound.y, 0.05f);
            Assert.AreEqual(rootVisualElement.worldBound.width, _scope.worldBound.width, 0.05f);
            Assert.AreEqual(rootVisualElement.worldBound.height, _scope.worldBound.height, 0.05f);
        }

        [Test]
        public void 倍率が戻ると寸法も戻り箱そのものはクリックを受けない()
        {
            _stageScale.Value = 1.5f;
            simulate.FrameUpdate();
            _stageScale.Value = 1f;
            simulate.FrameUpdate();
            Assert.AreEqual(320f, _content.worldBound.width, 0.05f);

            // 中身の外の点は箱を素通りする（箱の後ろの層へ届く）
            // A point outside the contents passes through the box to whatever lies behind it
            var outside = new UnityEngine.Vector2(_content.worldBound.xMax + 50f, _content.worldBound.yMax + 50f);
            var picked = rootVisualElement.panel.Pick(outside);
            Assert.IsFalse(picked == _scope, "the scope itself must not be picked");
            Assert.AreEqual(_content, rootVisualElement.panel.Pick(_content.worldBound.center));
        }
    }
}
