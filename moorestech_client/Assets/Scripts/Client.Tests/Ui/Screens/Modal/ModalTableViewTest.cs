using Client.Tests.Ui.Screens.GrabOverlay;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.Foundation.Input.OsCursor;
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

        [Test]
        public void 幕を押すと取り消しとして返り_外した後の押下は返さない()
        {
            var commands = new RecordingModalCommands();
            var backdropClicked = new Subject<Unit>();
            var view = Mount(TableView(commands, backdropClicked));
            simulate.FrameUpdate();
            backdropClicked.OnNext(Unit.Default);

            Assert.AreEqual(1, commands.Responses.Count);
            Assert.AreEqual("t1", commands.Responses[0].RequestId);
            Assert.AreEqual(ModalResult.Cancel, commands.Responses[0].Result);

            // 画面から外れた後の幕の押下は、もう自分の要求ではないので返さない
            // Backdrop presses after detaching are no longer for this request, so nothing is returned
            view.RemoveFromHierarchy();
            backdropClicked.OnNext(Unit.Default);
            Assert.AreEqual(1, commands.Responses.Count);
        }

        private static ModalTableView TableView(RecordingModalCommands commands)
        {
            return TableView(commands, Observable.Never<Unit>());
        }

        private static ModalTableView TableView(RecordingModalCommands commands, System.IObservable<Unit> backdropClicked)
        {
            return new ModalTableView(ModalRequestSamples.Table(), commands, backdropClicked, new UiOsCursorRequestStack(new FakeUiOsCursorApplier(), new FakeUiScreenPointer(1f)));
        }
    }
}
