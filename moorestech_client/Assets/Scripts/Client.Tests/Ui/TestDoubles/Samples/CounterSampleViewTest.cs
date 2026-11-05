using Client.Tests.Ui.TestHost;
using NUnit.Framework;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.TestDoubles.Samples
{
    /// <summary>
    /// 雛形の使い方の最小例。偽モデルの Set で表示が変わり、ボタンの押下が記録係に積まれることを確かめる（L1）
    /// Minimal example of the templates: Set on the fake model changes the display and a button press lands on the recorder (L1)
    /// </summary>
    public class CounterSampleViewTest : UiTestHost
    {
        [Test]
        public void FakeModelDrivesLabelAndButtonPressIsRecorded()
        {
            var model = new FakeCounterModel(3);
            var commands = new RecordingCounterCommands();
            var view = new CounterSampleView(model.Current, commands);
            rootVisualElement.Add(view);
            simulate.FrameUpdate();
            Assert.AreEqual("3", view.CountLabel.text);

            // setUiState に当たる差し込みで表示が変わる
            // The push that stands for setUiState changes the display
            model.Set(7);
            simulate.FrameUpdate();
            Assert.AreEqual("7", view.CountLabel.text);

            // payloadsOf に当たる Issued に押下が積まれる
            // The press lands on Issued, which stands for payloadsOf
            simulate.Click(view.IncrementButton);
            CollectionAssert.AreEqual(new[] { CounterCommand.Increment }, commands.Issued);
        }

        // 例のためだけの View。数を Label に出し、Button でコマンドを呼ぶ
        // View for the example only: shows the count on a Label and calls the command from a Button
        private sealed class CounterSampleView : VisualElement
        {
            public readonly Label CountLabel = new() { name = "counter-sample-count" };
            public readonly Button IncrementButton;

            public CounterSampleView(IReadOnlyReactiveProperty<int> count, ICounterCommands commands)
            {
                IncrementButton = new Button(commands.Increment) { name = "counter-sample-increment", text = "+" };
                Add(CountLabel);
                Add(IncrementButton);
                count.Subscribe(value => CountLabel.text = value.ToString());
            }
        }
    }
}
