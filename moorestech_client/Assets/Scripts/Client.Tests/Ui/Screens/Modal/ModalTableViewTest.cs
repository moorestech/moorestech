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
            var view = Mount(new ModalView(ModalRequestSamples.Table(), new RecordingModalCommands(), Observable.Never<Unit>()));
            simulate.FrameUpdate();

            var table = Find<VisualElement>(view, "modal-table");
            Assert.AreEqual(2, table.Query<VisualElement>(className: "modal-view__table-row").ToList().Count);
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
        public void 行の無い要求は表を作らない()
        {
            var view = Mount(new ModalView(ModalRequestSamples.Confirm(), new RecordingModalCommands(), Observable.Never<Unit>()));
            simulate.FrameUpdate();
            Assert.IsNull(view.Q<VisualElement>("modal-table"));
        }

        [Test]
        public void 表のモーダルのボタンは確認として返る()
        {
            var commands = new RecordingModalCommands();
            var view = Mount(new ModalView(ModalRequestSamples.Table(), commands, Observable.Never<Unit>()));
            simulate.FrameUpdate();
            simulate.Click(Find<VisualElement>(view, "modal-button").worldBound.center);

            Assert.AreEqual(1, commands.Responses.Count);
            Assert.AreEqual("t1", commands.Responses[0].RequestId);
            Assert.AreEqual(ModalResult.Confirm, commands.Responses[0].Result);
        }
    }
}
