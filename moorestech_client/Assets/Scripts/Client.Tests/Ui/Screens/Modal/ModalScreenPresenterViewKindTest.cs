using System.Threading;
using Client.Tests.Ui.Screens.GrabOverlay;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Modal;
using Client.Ui.Foundation.Stage.ScaleReading;
using Client.Ui.Screens.Modal;
using Client.Ui.State.Modal;
using NUnit.Framework;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Modal
{
    /// <summary>
    /// 要求の種類で View と載せ方（舞台の倍率か実画面 px か）を振り分ける（L1）
    /// Picks the view and how it is mounted (stage scale or real-screen px) by request kind (L1)
    /// </summary>
    public class ModalScreenPresenterViewKindTest : ScreenTestHost
    {
        private const float StageScale = 1.5f;

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

            var host = new UiModalHost(new StageScaleSource(new ReactiveProperty<float>(StageScale)));
            host.Mount(_layer);
            _service = new ModalRequestService();
            new ModalScreenPresenter(host, _service, new ModalCommands(_service, Toaster), new UiOsCursorRequestStack(new FakeUiOsCursorApplier(), new FakeUiScreenPointer(1f))).Initialize();
            simulate.FrameUpdate();
        }

        [Test]
        public void 行のある要求は表のモーダルを舞台の倍率で出す()
        {
            var rows = new[] { new ModalTableRow("部屋クラスを B に", "失敗 −13.1%") };
            _service.RequestTableModal("失敗率の改善方法", "今: 失敗 50.5%", rows, "閉じる", CancellationToken.None);
            simulate.FrameUpdate();
            Assert.IsTrue(Find<VisualElement>(_layer, "modal").ClassListContains("game-panel--craft"));
            Assert.IsNotNull(Find<Label>(_layer, "modal-table-row-0-label"));

            // 舞台の画面から開く表は、実画面 px の宿主の中でも開いた画面と同じ舞台の倍率で出す
            // A table opened from a stage screen uses that screen's stage scale even inside the real-screen-px host
            Assert.AreEqual(StageScale, _layer.Q(className: "modal-table-view").style.scale.value.value.x, 0.001f);
        }

        [Test]
        public void 行の無い要求は表を作らず実画面pxのダイアログで出す()
        {
            _service.RequestModal("確認", "これは確認ダイアログです", "OK", ModalVariant.Confirm, CancellationToken.None);
            simulate.FrameUpdate();
            Assert.IsNull(_layer.Q<VisualElement>("modal-table"));

            // 確認・入力のダイアログには舞台の倍率を掛けない（実画面 px のまま）
            // Confirm and input dialogs take no stage scale (they stay in real-screen px)
            Assert.AreEqual(StyleKeyword.Null, _layer.Q(className: "modal-dialog-view").style.scale.keyword);
        }
    }
}
