using Client.Tests.Ui.Screens;
using Client.Ui.Foundation.Layout;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.Parts.Rows;
using Client.Ui.State.BlockInventory.Logic.Detail;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Parts
{
    public class BlockInventoryRowsTest : ScreenTestHost
    {
        /// <web-test file="src/features/blockInventory/details/rows/PowerRateText.tsx">率を四捨五入の百分率で出し、強調が許されて率が1未満なら不足にする</web-test>
        [Test]
        public void 電力率は四捨五入の百分率で出し強調が許されれば不足にする()
        {
            // 文を入れてから載せる（文無しのまま載ると LogError）
            // Render before mounting (mounting without text logs an error)
            var view = new PowerRateText("power");
            view.Render(1, 8, true);
            Mount(view);
            var label = view.Q<Label>("power");
            // 12.5% は JS の Math.round と同じく 13 へ上がる
            // 12.5% rounds up to 13 like JS Math.round
            Assert.AreEqual("Power 13% (1/8)", label.text);
            Assert.IsTrue(label.ClassListContains(LackHighlightText.InsufficientClassName));
        }

        /// <web-test file="src/features/blockInventory/details/rows/PowerRateText.tsx">強調が許されなければ率が低くても不足にしない</web-test>
        [Test]
        public void 強調が許されなければ率が低くても不足にしない()
        {
            var view = new PowerRateText("power");
            view.Render(1, 2, false);
            Mount(view);
            Assert.IsFalse(view.Q<Label>("power").ClassListContains(LackHighlightText.InsufficientClassName));
        }

        /// <web-test file="src/features/blockInventory/details/rows/MachineStateRow.tsx">停止中は電力率を隠す</web-test>
        [Test]
        public void 停止中は電力率を隠しエラーを出さない()
        {
            var stack = new UiStack(UiGap.Sm);
            var row = new MachineStateRow("state", "power");
            row.MountInto(stack);
            row.Render(MachineProcessDisplayState.Halted, 0, 4, true);
            Mount(stack);
            // 隠すのは電力率の箱ごと（中のラベルの display は変わらない）
            // The whole power-rate box is hidden (the inner label's display does not change)
            var powerRate = stack.Q<PowerRateText>();
            Assert.IsFalse(IsShown(powerRate));
            row.Render(MachineProcessDisplayState.Processing, 2, 4, true);
            Assert.IsTrue(IsShown(powerRate));
            LogAssert.NoUnexpectedReceived();
        }

        /// <web-test file="src/features/blockInventory/details/rows/PerMinuteRateRow.tsx">分間レートを小数1桁で出す</web-test>
        [Test]
        public void 分間レートは小数一桁で出す()
        {
            var row = new PerMinuteRateRow(new VisualElement());
            row.Render(12.34);
            Mount(row);
            Assert.AreEqual("12.3/min", row.Q<Label>().text);
        }
    }
}
