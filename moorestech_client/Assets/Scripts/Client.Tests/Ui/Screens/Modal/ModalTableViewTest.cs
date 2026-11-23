using Client.Tests.Ui.Screens.GrabOverlay;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Stage.ScaleReading;
using Client.Ui.Screens.Modal;
using Client.Ui.State.Modal;
using NUnit.Framework;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Modal
{
    /// <summary>
    /// モーダルの項目名と値の行（L1）
    /// Modal label-and-values rows (L1)
    /// </summary>
    public class ModalTableViewTest : ScreenTestHost
    {
        private const float StageScale = 1.5f;

        [Test]
        public void 表の行は本文とボタンの間に項目名の行と字下げした値の行で描く()
        {
            var view = Mount(TableView(new RecordingModalCommands()));
            simulate.FrameUpdate();

            var table = Find<VisualElement>(view, "modal-table");
            Assert.AreEqual(2, table.Query<VisualElement>(className: "modal-table-view__row").ToList().Count);
            StringAssert.EndsWith("部屋クラスを B に", Find<Label>(view, "modal-table-row-0-label").text);
            StringAssert.EndsWith("Lv1 +9.2% / 失敗 −13.1%", Find<Label>(view, "modal-table-row-0-values").text);
            StringAssert.EndsWith("品質モジュールLv1 を追加", Find<Label>(view, "modal-table-row-1-label").text);

            // 値の行は項目名の行の下で、字下げしている
            // The values line sits below the label line and is indented
            var label = Find<Label>(view, "modal-table-row-0-label").worldBound;
            var values = Find<Label>(view, "modal-table-row-0-values").worldBound;
            Assert.GreaterOrEqual(values.yMin, label.yMax - 0.5f);
            Assert.Greater(values.xMin, label.xMin + 0.5f);

            // 表は本文の下、ボタンの上
            // The table sits under the message and above the button
            Assert.LessOrEqual(Find<Label>(view, "modal-message").worldBound.yMax, table.worldBound.yMin + 0.5f);
            Assert.LessOrEqual(table.worldBound.yMax, Find<VisualElement>(view, "modal-button").worldBound.yMin + 0.5f);
        }

        [Test]
        public void 表のモーダルはレシピ詳細と同じCraft枠で題の罫線を付けない()
        {
            var view = Mount(TableView(new RecordingModalCommands()));
            simulate.FrameUpdate();

            var panel = Find<VisualElement>(view, "modal");
            Assert.IsTrue(panel.ClassListContains("game-panel--craft"));
            Assert.IsFalse(panel.ClassListContains("game-panel--titled"));

            // 実画面 px の宿主の中でも、インベントリと同じ舞台の倍率で出す
            // Even inside the real-screen-px host, it uses the inventory's stage scale
            Assert.AreEqual(StageScale, view.style.scale.value.value.x, 0.001f);
        }

        [Test]
        public void 表のモーダルのボタンは確認として返る()
        {
            var commands = new RecordingModalCommands();
            var view = Mount(TableView(commands));
            simulate.FrameUpdate();
            simulate.Click(Find<VisualElement>(view, "modal-button").worldBound.center);

            Assert.AreEqual(1, commands.Responses.Count);
            Assert.AreEqual("t1", commands.Responses[0].RequestId);
            Assert.AreEqual(ModalResult.Confirm, commands.Responses[0].Result);
        }

        private static ModalTableView TableView(RecordingModalCommands commands)
        {
            return new ModalTableView(ModalRequestSamples.Table(), commands, Observable.Never<Unit>(), new UiOsCursorRequestStack(new FakeUiOsCursorApplier(), new FakeUiScreenPointer(1f)), new StageScaleSource(new ReactiveProperty<float>(StageScale)));
        }
    }
}
