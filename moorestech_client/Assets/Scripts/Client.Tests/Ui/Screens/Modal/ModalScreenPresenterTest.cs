using Client.Input;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Input;
using Client.Ui.Foundation.Modal;
using Client.Ui.Foundation.Stage.ScaleReading;
using Client.Ui.Screens.Modal;
using Client.Ui.State.Modal;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Modal
{
    /// <summary>
    /// 保留の要求を宿主へ出し、確定・幕の取消を Respond へ返して消す。倍率 1.5 でも実画面 px（L1。Web modal.spec.ts・設計書 A-41）
    /// Shows the pending request in the host, returns confirm and backdrop cancel to Respond and hides; real-screen px at scale 1.5 (L1; Web modal.spec.ts, design A-41)
    /// </summary>
    public class ModalScreenPresenterTest : ScreenTestHost
    {
        private const float StageScale = 1.5f;
        private const string ShownClass = "ui-modal-host--shown";

        private VisualElement _layer;
        private ModalRequestService _service;

        [SetUp]
        public void MountPresenter()
        {
            _layer = new VisualElement { name = "modal-test-layer" };
            _layer.style.position = Position.Absolute;
            _layer.style.left = 0;
            _layer.style.top = 0;
            _layer.style.right = 0;
            _layer.style.bottom = 0;
            rootVisualElement.Add(_layer);

            // 入力の規則は本番と同じく層の根に付ける（前例 GameTextFieldTest）
            // Attach the input rules at the layer root as in production (precedent GameTextFieldTest)
            GameUiInputRules.Attach(_layer);
            var host = new UiModalHost(new StageScaleSource(new ReactiveProperty<float>(StageScale)));
            host.Mount(_layer);
            _service = new ModalRequestService();
            new ModalScreenPresenter(host, _service, new ModalCommands(_service), Toaster).Initialize();
            simulate.FrameUpdate();
        }

        [TearDown]
        public void ResetInputLatches()
        {
            UiInputExclusivity.SetGameUiTextInputFocused(false);
            UiInputExclusivity.ClearEscapeLatchForTest();
        }

        /// <web-test file="e2e/tests/modal.spec.ts">OK クリックで confirm を送りモーダルが消える</web-test>
        [Test]
        public void OKを押すと確定を返してモーダルが消える()
        {
            var result = _service.RequestModal("確認", "これは確認ダイアログです", "OK", ModalVariant.Confirm);
            simulate.FrameUpdate();
            Assert.IsTrue(Shown());

            simulate.Click(Find<FormButton>(_layer, "modal-button").worldBound.center);
            simulate.FrameUpdate();
            Assert.AreEqual(UniTaskStatus.Succeeded, result.Status);
            Assert.AreEqual(ModalResult.Confirm, result.GetAwaiter().GetResult());
            Assert.IsNull(_service.Pending);
            Assert.IsFalse(Shown());
        }

        /// <web-test file="e2e/tests/modal.spec.ts">背景クリックで cancel を送る</web-test>
        [Test]
        public void 幕の左上を押すと取消を返す()
        {
            var (_, response) = _service.RequestInputModal("ブループリント名", "ブループリントの名前を入力してください", "保存");
            simulate.FrameUpdate();
            simulate.Click(new Vector2(_layer.worldBound.xMin + 5f, _layer.worldBound.yMin + 5f));
            simulate.FrameUpdate();
            Assert.AreEqual(UniTaskStatus.Succeeded, response.Status);
            var (result, text) = response.GetAwaiter().GetResult();
            Assert.AreEqual(ModalResult.Cancel, result);
            Assert.IsNull(text);
            Assert.IsFalse(Shown());
        }

        [Test]
        public void 倍率が掛かっても枠は実画面440pxで中央に出る()
        {
            // 設計書 A-41: モーダルは Portal で倍率を掛けない。テストのパネルは倍率 1 なので舞台の倍率を掛けて実画面 px を見る
            // Design A-41: the modal is a Portal without scaling; the test panel has scale 1, so multiply by the stage scale for real-screen px
            _service.RequestModal("確認", "これは確認ダイアログです", "OK", ModalVariant.Confirm).Forget();
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            var frame = Find<VisualElement>(_layer, "modal");
            Assert.AreEqual(440f, frame.worldBound.width * StageScale, 0.5f);
            Assert.AreEqual(_layer.worldBound.center.x, frame.worldBound.center.x, 0.5f);
            Assert.AreEqual(_layer.worldBound.center.y, frame.worldBound.center.y, 0.5f);
        }

        [Test]
        public void 新しい要求は前の入力を持ち越さない()
        {
            // Web は要求 ID を key にして入力をリセットする。UITK は要求ごとに View を作り直す（計画 D-13）
            // The Web resets input by keying on the request id; UITK rebuilds the view per request (plan D-13)
            _service.RequestInputModal("ブループリント名", "ブループリントの名前を入力してください", "保存");
            simulate.FrameUpdate();
            Find<TextField>(_layer, "modal-input").value = "前の名前";
            _service.RequestInputModal("ブループリント名", "ブループリントの名前を入力してください", "保存");
            simulate.FrameUpdate();
            Assert.AreEqual("", Find<TextField>(_layer, "modal-input").value);
            Assert.IsFalse(Find<FormButton>(_layer, "modal-button").enabledSelf);
        }

        [Test]
        public void 入力欄のESCは1回で取り消しワールド側へESCを渡さない()
        {
            // Web の Mantine と同じく ESC 1 回で閉じる。GameUiInputRules がフォーカスを外して消費し、その外れで View が取り消す（計画 D-11）
            // One ESC closes, as Mantine on the Web; GameUiInputRules drops focus and consumes it, and the view cancels on that blur (plan D-11)
            var (_, response) = _service.RequestInputModal("ブループリント名", "ブループリントの名前を入力してください", "保存");
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            Find<TextField>(_layer, "modal-input").value = "途中の名前";

            simulate.KeyDown(KeyCode.Escape);
            simulate.FrameUpdate();
            Assert.AreEqual(UniTaskStatus.Succeeded, response.Status);
            Assert.AreEqual(ModalResult.Cancel, response.GetAwaiter().GetResult().result);
            Assert.IsFalse(Shown());
            Assert.IsTrue(UiInputExclusivity.IsEscapeConsumedThisFrame);
        }

        [Test]
        public void 入力欄の取消ナビゲーションも1回で取り消す()
        {
            // 録画の注入入力の ESC は KeyDown にならず NavigationCancel だけで届く（PanelEventHandler）。実キーと同じく 1 回で閉じる
            // An ESC injected by recordings arrives only as NavigationCancel, not KeyDown (PanelEventHandler); it closes in one press like a real key
            var (_, response) = _service.RequestInputModal("ブループリント名", "ブループリントの名前を入力してください", "保存");
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            using (var evt = NavigationCancelEvent.GetPooled())
            {
                evt.target = _layer.focusController.focusedElement;
                _layer.panel.visualTree.SendEvent(evt);
            }
            simulate.FrameUpdate();
            Assert.AreEqual(UniTaskStatus.Succeeded, response.Status);
            Assert.AreEqual(ModalResult.Cancel, response.GetAwaiter().GetResult().result);
            Assert.IsFalse(Shown());
            Assert.IsTrue(UiInputExclusivity.IsEscapeConsumedThisFrame);
        }

        [Test]
        public void 入力欄の外を押してフォーカスが外れても閉じない()
        {
            var (_, response) = _service.RequestInputModal("ブループリント名", "ブループリントの名前を入力してください", "保存");
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            simulate.Click(Find<Label>(_layer, "modal-message").worldBound.center);
            simulate.FrameUpdate();
            Assert.AreEqual(UniTaskStatus.Pending, response.Status);
            Assert.IsTrue(Shown());
        }

        [Test]
        public void 入力欄にフォーカスがあるまま外から取り消されてもゲームのキー入力を止めたままにしない()
        {
            // 右短押し・要求の差し替えはブリッジの CancelPendingRequest で閉じ、入力欄のフォーカスが外れる前に View が外れる
            // Right short-press and request replacement close through the bridge's CancelPendingRequest, detaching the view before the field loses focus
            _service.RequestInputModal("ブループリント名", "ブループリントの名前を入力してください", "保存");
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            Assert.IsTrue(UiInputExclusivity.IsTextInputFocused);

            _service.CancelPendingRequest();
            simulate.FrameUpdate();
            Assert.IsFalse(Shown());
            Assert.IsFalse(UiInputExclusivity.IsTextInputFocused);
        }

        private bool Shown()
        {
            // C3-b の UiModalHostTest.ScopeShown と同じ式
            // Same expression as C3-b UiModalHostTest.ScopeShown
            return _layer.Q("modal-scope").ClassListContains(ShownClass);
        }
    }
}
