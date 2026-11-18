using System.Linq;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Slots;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Slots.Fluid
{
    /// <summary>
    /// FluidSlotRow の並びと矢印を確かめる（L1）
    /// Verifies FluidSlotRow lays cells out at xs gap, toggles the progress arrow at the end, and hides an empty row (L1)
    /// </summary>
    public class FluidSlotRowTest : UiTestHost
    {
        [TearDown]
        public void NoUnexpectedLogs()
        {
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void マスが0なら列ごと出さない()
        {
            var row = Mount();
            row.SetFluids(new FluidSlotContent[0]);
            simulate.FrameUpdate();

            Assert.AreEqual(DisplayStyle.None, row.resolvedStyle.display);
            row.SetFluids(new[] { FluidSlotContent.Empty });
            simulate.FrameUpdate();
            Assert.AreEqual(DisplayStyle.Flex, row.resolvedStyle.display);
        }

        [Test]
        public void マスはxsの間隔で横に並び矢印は最後のマスの右に縦中央で出る()
        {
            var row = Mount();
            row.SetFluids(new[] { Filled(), FluidSlotContent.Empty });
            row.SetProgress(0.5f);
            simulate.FrameUpdate();

            var cells = row.Query<FluidSlot>().ToList();
            var arrow = row.Q("progress-arrow-bar");
            var gap = ThemeTokenFile.ResolvePixels("--mantine-spacing-xs");
            Assert.AreEqual(2, cells.Count);
            Assert.IsTrue(cells.All(cell => cell.name == "fluid-slot"));
            Assert.AreEqual(cells[0].worldBound.xMax + gap, cells[1].worldBound.xMin, 0.5f);
            Assert.AreEqual(cells[1].worldBound.xMax + gap, arrow.worldBound.xMin, 0.5f);
            Assert.AreEqual(cells[1].worldBound.center.y, arrow.worldBound.center.y, 0.5f);
        }

        [Test]
        public void 数が減っても残るマスは同じ要素を使い回し矢印は末尾のまま()
        {
            var row = Mount();
            row.SetFluids(new[] { Filled(), Filled(), Filled() });
            row.SetProgress(0.2f);
            var first = row.Q<FluidSlot>();
            row.SetFluids(new[] { FluidSlotContent.Empty });
            row.SetFluids(new[] { FluidSlotContent.Empty, Filled() });
            simulate.FrameUpdate();

            var cells = row.Query<FluidSlot>().ToList();
            Assert.AreEqual(2, cells.Count);
            Assert.AreSame(first, cells[0]);
            var arrow = row.Q("progress-arrow-bar");
            Assert.AreEqual(arrow.parent.childCount - 1, arrow.parent.IndexOf(arrow));
        }

        [Test]
        public void 進捗を消すと矢印は出ず間隔も残さない()
        {
            var row = Mount();
            row.SetFluids(new[] { Filled() });
            row.SetProgress(0.5f);
            simulate.FrameUpdate();
            var cell = row.Q<FluidSlot>();
            var cellRightWhileShown = cell.worldBound.xMax;
            row.ClearProgress();
            simulate.FrameUpdate();

            // 間隔は矢印自身の左余白で、表示した後に隠しても値は残るが display: none で配置から外れる。行は親の幅に伸びるので行の幅では見ない
            // The gap is the arrow's own left margin; it stays set after hiding but display: none drops it from layout. The row stretches, so its width is not checked
            var arrow = row.Q("progress-arrow-bar");
            Assert.AreEqual(DisplayStyle.None, arrow.resolvedStyle.display);
            Assert.AreEqual(0f, arrow.layout.width, 0.5f);
            Assert.AreEqual(cellRightWhileShown, cell.worldBound.xMax, 0.5f);
        }

        private FluidSlotRow Mount()
        {
            var row = new FluidSlotRow("tank-fluids");
            rootVisualElement.Add(row);
            Assert.AreEqual("tank-fluids", row.name);
            return row;
        }

        private static FluidSlotContent Filled()
        {
            return FluidSlotContent.Filled(SlotTestIcons.WithTexture("10", "Water"), 500, 1000, Color.blue);
        }
    }
}
