using Client.Ui.Components.Slots;
using Client.Ui.Foundation.Text;
using Client.Ui.State.BlockInventory.Logic.Common;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Fluid
{
    /// <summary>
    /// パイプの液体の詳細（名前と「量 / 容量」の文・空の行・同じ行数の再描画で要素を残す）
    /// Pipe fluid details (name and "amount / capacity" texts, empty rows, keeping elements across same-count re-renders)
    /// </summary>
    public class FluidPipeDetailsTest : FluidScreenTestHost
    {
        private const string Slot0 = "fluid-pipe-fluid-slots-slot-0";

        /// <web-test file="src/features/blockInventory/views/__tests__/SectionStackView.test.tsx">パイプは液体だけを進捗なしで描画する</web-test>
        [Test]
        public void パイプは液体だけを進捗なしで描く()
        {
            var body = MountBody("FluidPipe");
            SetFluids(null, BlockFluidSlotState.Filled(Water, 40, 100));

            Assert.AreEqual(0, Shown<VisualElement>(body.Root, "progress-arrow-bar").Count);
            var texts = Shown<LocalizedLabel>(body.Root, null, "text--sm");
            Assert.AreEqual(new[] { "Test Water", "40 / 100" }, texts.ConvertAll(label => label.text).ToArray());

            SetFluids(null, BlockFluidSlotState.Filled(Water, 60, 100));
            Assert.AreEqual("60", AmountOf(body.Root.Q(Slot0)));
        }

        /// <web-test file="src/features/blockInventory/views/__tests__/SectionStackView.test.tsx">空のパイプは名前や容量を表示せず空スロットを残す</web-test>
        [Test]
        public void 空のパイプは文を出さず空の枠を残す()
        {
            var body = MountBody("FluidPipe");
            SetFluids(null, BlockFluidSlotState.Empty(100));

            Assert.IsNotNull(body.Root.Q(Slot0));
            Assert.AreEqual(1, Shown<FluidSlot>(body.Root, "fluid-slot").Count);
            Assert.AreEqual(0, Shown<LocalizedLabel>(body.Root, null, "text--sm").Count);
        }

        [Test]
        public void 同じ行数の再描画では行の要素が同一のまま中身だけ変わる()
        {
            var body = MountBody("FluidPipe");
            SetFluids(null, BlockFluidSlotState.Filled(Water, 40, 100));
            var row = body.Root.Q(Slot0);

            SetFluids(null, BlockFluidSlotState.Filled(Water, 60, 100));
            Assert.AreSame(row, body.Root.Q(Slot0));
            Assert.AreEqual("60 / 100", AmountText(body.Root));

            SetFluids(null, BlockFluidSlotState.Empty(100));
            Assert.AreSame(row, body.Root.Q(Slot0));
            Assert.AreEqual(0, Shown<LocalizedLabel>(body.Root, null, "text--sm").Count);

            SetFluids(null, BlockFluidSlotState.Filled(Water, 60, 100));
            Assert.AreSame(row, body.Root.Q(Slot0));
            Assert.AreEqual("60 / 100", AmountText(body.Root));

            SetFluids(null, BlockFluidSlotState.Filled(Water, 60, 100), BlockFluidSlotState.Empty(100));
            Assert.AreNotSame(row, body.Root.Q(Slot0));
        }

        private static string AmountText(VisualElement root)
        {
            var texts = Shown<LocalizedLabel>(root, null, "text--sm");
            return texts[texts.Count - 1].text;
        }

        private static string AmountOf(VisualElement slotRow)
        {
            return slotRow.Q<Label>(null, "fluid-slot__amount", "outlined-count-label--glyph").text;
        }
    }
}
