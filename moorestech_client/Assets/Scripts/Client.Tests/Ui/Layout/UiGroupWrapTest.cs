using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Layout;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Layout
{
    /// <summary>
    /// UiGroup が Mantine Group の CSS gap と同じに、折り返しても行頭へ gap を付けないことを実パネルで確かめる
    /// Verifies on a real panel that UiGroup matches Mantine Group CSS gap: no gap at row starts even when wrapped
    /// </summary>
    public class UiGroupWrapTest : UiTestHost
    {
        [Test]
        public void Groupが折り返すと行頭にgapを付けず行間にgapを空ける()
        {
            // 幅 100 に 40 幅の箱 3 つ（gap 10）。1 行目に 2 つ、3 つ目は 2 行目の行頭に落ちる
            // Three 40-wide boxes (gap 10) in 100px: two on row one, the third wraps to the start of row two
            var group = new UiGroup(UiGap.Xs);
            group.style.width = 100;
            var boxes = new[] { Box(), Box(), Box() };
            foreach (var box in boxes) group.AddItem(box);
            rootVisualElement.Add(group);
            simulate.FrameUpdate();

            var left = group.worldBound.x;
            Assert.AreEqual(left, boxes[0].worldBound.x, 0.01f);
            Assert.AreEqual(10f, boxes[1].worldBound.x - boxes[0].worldBound.xMax, 0.01f);
            Assert.AreEqual(left, boxes[2].worldBound.x, 0.01f);
            Assert.AreEqual(group.worldBound.y, boxes[0].worldBound.y, 0.01f);
            Assert.AreEqual(10f, boxes[2].worldBound.y - boxes[0].worldBound.yMax, 0.01f);
            Assert.AreEqual(boxes[2].worldBound.yMax, group.worldBound.yMax, 0.01f);
        }

        [Test]
        public void Groupは1行のときルートの寸法が子の合計と列間gapに一致する()
        {
            var group = new UiGroup(UiGap.Xs);
            var first = Box();
            var second = Box();
            group.AddItem(first);
            group.AddItem(second);
            rootVisualElement.Add(group);
            simulate.FrameUpdate();

            Assert.AreEqual(20f, group.worldBound.height, 0.01f);
            Assert.AreEqual(second.worldBound.xMax, group.worldBound.x + 40f + 10f + 40f, 0.01f);
            Assert.AreEqual(group.worldBound.x, first.worldBound.x, 0.01f);
            Assert.AreEqual(group.worldBound.y, first.worldBound.y, 0.01f);
        }

        [Test]
        public void GroupのSetItemShownで先頭を隠すと次の子が行頭に来て戻すとgapが復活する()
        {
            var group = new UiGroup(UiGap.Md);
            var first = Box();
            var second = Box();
            group.AddItem(first);
            group.AddItem(second);
            rootVisualElement.Add(group);
            simulate.FrameUpdate();

            group.SetItemShown(first, false);
            simulate.FrameUpdate();
            Assert.AreEqual(group.worldBound.x, second.worldBound.x, 0.01f);

            group.SetItemShown(first, true);
            simulate.FrameUpdate();
            Assert.AreEqual(StyleKeyword.Null, first.style.display.keyword);
            Assert.AreEqual(16f, second.worldBound.x - first.worldBound.xMax, 0.01f);
        }

        private static VisualElement Box()
        {
            var box = new VisualElement();
            box.style.width = 40;
            box.style.height = 20;
            return box;
        }
    }
}
