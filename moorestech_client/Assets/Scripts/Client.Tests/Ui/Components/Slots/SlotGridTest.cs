using System;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Slots;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Slots
{
    /// <summary>
    /// SlotGrid の列・行の並びと間隔を既定とつまみの上書きで確かめる（L1）
    /// Verifies SlotGrid column/row layout and gaps with defaults and knob overrides (L1)
    /// </summary>
    public class SlotGridTest : UiTestHost
    {
        private const string KnobPath = "Assets/Scripts/Client.Tests/Ui/Components/Slots/SlotKnobOverrides.uss";
        // 画素丸めのため 0.5px まで許す
        // Allow 0.5px because sizes snap to pixels
        private const float Tolerance = 0.5f;
        // 列の幅と間隔が 1 つずつ画素へ丸まって足されるので、幅の合計は 1px まで許す
        // Each track and gap snaps to a pixel before summing, so the total width allows 1px
        private const float WidthTolerance = 1f;

        [TearDown]
        public void NoUnexpectedLogs()
        {
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void 列数ごとに行を作り間隔は既定のつまみ()
        {
            var (grid, cells) = Mount(null, 5, 12);

            var size = ThemeTokenFile.ResolvePixels("--slot-size");
            var gap = ThemeTokenFile.ResolvePixels("--slot-grid-gap");
            // 先頭の子は列の幅の行、続く 3 つがマスの行
            // The first child is the track row, followed by three cell rows
            Assert.AreEqual(4, grid.childCount);
            Assert.AreEqual(gap, cells[1].worldBound.xMin - cells[0].worldBound.xMax, Tolerance);
            Assert.AreEqual(gap, cells[5].worldBound.yMin - cells[0].worldBound.yMax, Tolerance);
            Assert.AreEqual(cells[0].worldBound.xMin, cells[5].worldBound.xMin, Tolerance);
            Assert.AreEqual(5 * size + 4 * gap, grid.layout.width, WidthTolerance);
        }

        [Test]
        public void 列数を変えると前から詰め直す()
        {
            var (grid, cells) = Mount(null, 5, 12);
            grid.SetColumns(4);
            simulate.FrameUpdate();

            Assert.AreEqual(4, grid.childCount);
            Assert.AreEqual(4 * ThemeTokenFile.ResolvePixels("--slot-size") + 3 * ThemeTokenFile.ResolvePixels("--slot-grid-gap"), grid.layout.width, WidthTolerance);
            Assert.AreEqual(cells[0].worldBound.xMin, cells[4].worldBound.xMin, Tolerance);
            Assert.IsTrue(cells[4].ClassListContains("slot-grid__cell--row-start"));
            Assert.IsFalse(cells[5].ClassListContains("slot-grid__cell--row-start"));
        }

        [Test]
        public void 祖先で列と行の間隔を別々に上書きできる()
        {
            var (_, cells) = Mount("slot-knob-item-list", 5, 10);

            Assert.AreEqual(46.144f, cells[0].layout.width, Tolerance);
            Assert.AreEqual(8.656f, cells[1].worldBound.xMin - cells[0].worldBound.xMax, Tolerance);
            Assert.AreEqual(8.896f, cells[5].worldBound.yMin - cells[0].worldBound.yMax, Tolerance);
        }

        [Test]
        public void 祖先で列の間隔だけ上書きすると行の間隔も追う()
        {
            // Web の var(--slot-grid-row-gap, var(--slot-grid-gap)) と同じく、:root の既定が使う側で解けるかの実測
            // Measures whether the :root default resolves at the use site like the Web var(--slot-grid-row-gap, var(--slot-grid-gap))
            var (_, cells) = Mount("slot-knob-gap-only", 5, 10);

            Assert.AreEqual(10f, cells[1].worldBound.xMin - cells[0].worldBound.xMax, Tolerance);
            Assert.AreEqual(10f, cells[5].worldBound.yMin - cells[0].worldBound.yMax, Tolerance);
        }

        [Test]
        public void 空にすると行もマスの印も消える()
        {
            var (grid, cells) = Mount(null, 5, 3);
            grid.ClearItems();
            simulate.FrameUpdate();

            // 列の幅の行だけが残り、幅は Web の grid と同じく列数ぶん
            // Only the track row remains and the width stays the column count like the Web grid
            Assert.AreEqual(1, grid.childCount);
            Assert.IsFalse(cells[0].ClassListContains("slot-grid__cell"));
            Assert.AreEqual(5 * ThemeTokenFile.ResolvePixels("--slot-size") + 4 * ThemeTokenFile.ResolvePixels("--slot-grid-gap"), grid.layout.width, WidthTolerance);
            Assert.AreEqual(0f, grid.layout.height, Tolerance);
        }

        [Test]
        public void マスが列数より少なくても幅は列数ぶん()
        {
            var (grid, _) = Mount(null, 5, 1);

            Assert.AreEqual(5 * ThemeTokenFile.ResolvePixels("--slot-size") + 4 * ThemeTokenFile.ResolvePixels("--slot-grid-gap"), grid.layout.width, WidthTolerance);
        }

        [Test]
        public void 隙間を指しても格子自身が当たる()
        {
            // 2 枠の中心は枠の間の隙間に落ちる（Web の grid の div は隙間でも elementFromPoint に当たる）
            // The centre of two cells falls in the gap between them (the Web grid div is hit by elementFromPoint there too)
            var (grid, cells) = Mount(null, 2, 2);
            var gapCenter = new UnityEngine.Vector2((cells[0].worldBound.xMax + cells[1].worldBound.xMin) / 2f, cells[0].worldBound.center.y);

            Assert.AreSame(grid, grid.panel.Pick(gapCenter));
            Assert.AreSame(grid, grid.panel.Pick(grid.worldBound.center));
        }

        [Test]
        public void 列数1未満は止める()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SlotGrid("grid", 0));
        }

        private (SlotGrid grid, SlotFrame[] cells) Mount(string knobClass, int columns, int count)
        {
            var screen = new VisualElement();
            if (knobClass != null)
            {
                var knobs = AssetDatabase.LoadAssetAtPath<StyleSheet>(KnobPath);
                Assert.IsNotNull(knobs, KnobPath);
                screen.styleSheets.Add(knobs);
                screen.AddToClassList(knobClass);
            }
            var grid = new SlotGrid("grid", columns);
            var cells = new SlotFrame[count];
            for (var i = 0; i < count; i++)
            {
                cells[i] = new SlotFrame($"cell-{i}");
                grid.AddItem(cells[i]);
            }
            screen.Add(grid);
            rootVisualElement.Add(screen);
            simulate.FrameUpdate();
            return (grid, cells);
        }
    }
}
