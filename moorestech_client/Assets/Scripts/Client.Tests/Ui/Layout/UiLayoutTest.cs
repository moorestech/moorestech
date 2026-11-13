using Client.Localization;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Layout;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Layout
{
    /// <summary>
    /// Stack/Group の間隔の確認
    /// Checks Stack/Group spacing
    /// </summary>
    public class UiLayoutTest : UiTestHost
    {
        [TestCase(UiGap.None, 0f)]
        [TestCase(UiGap.Px2, 2f)]
        [TestCase(UiGap.Px4, 4f)]
        [TestCase(UiGap.Xs, 10f)]
        [TestCase(UiGap.Sm, 12f)]
        [TestCase(UiGap.Md, 16f)]
        [TestCase(UiGap.Lg, 20f)]
        [TestCase(UiGap.Xl, 32f)]
        public void Stackは表示中の先頭以外の子の上にgapを空ける(UiGap gap, float expected)
        {
            var stack = new UiStack(gap);
            var first = Box();
            var second = Box();
            stack.AddItem(first);
            stack.AddItem(second);
            rootVisualElement.Add(stack);
            simulate.FrameUpdate();

            Assert.AreEqual(0f, first.resolvedStyle.marginTop, 0.01f);
            Assert.AreEqual(expected, second.worldBound.y - first.worldBound.yMax, 0.01f);
        }

        [Test]
        public void Groupは横に並べ中央で揃え先頭以外の左にgapを空ける()
        {
            var group = new UiGroup(UiGap.Xs);
            var first = Box();
            var second = Box();
            group.AddItem(first);
            group.AddItem(second);
            rootVisualElement.Add(group);
            simulate.FrameUpdate();

            var row = group.Q(className: "ui-group__row");
            Assert.AreEqual(FlexDirection.Row, row.resolvedStyle.flexDirection);
            Assert.AreEqual(Align.Center, row.resolvedStyle.alignItems);
            Assert.AreEqual(10f, second.worldBound.x - first.worldBound.xMax, 0.01f);
        }

        [Test]
        public void 先頭を隠すと次の子が先頭になりgapを持たない()
        {
            var stack = new UiStack(UiGap.Md);
            var first = Box();
            var second = Box();
            stack.AddItem(first);
            stack.AddItem(second);
            rootVisualElement.Add(stack);
            simulate.FrameUpdate();

            stack.SetItemShown(first, false);
            simulate.FrameUpdate();
            Assert.AreEqual(0f, second.resolvedStyle.marginTop, 0.01f);

            stack.SetItemShown(first, true);
            simulate.FrameUpdate();
            Assert.AreEqual(16f, second.resolvedStyle.marginTop, 0.01f);
        }

        [Test]
        public void 部品のmargin0に負けずにボタンの間にもgapを空ける()
        {
            // PanelActionButton は 2 クラスで margin: 0 を当てる。Stack の間隔はそれより強い必要がある
            // PanelActionButton sets margin: 0 with two classes; the Stack gap must win over it
            // LocalizedLabel が載った時点で辞書を引くので先に読み込む
            // LocalizedLabel looks up the dictionary on attach, so load it first
            Localize.Initialize();
            var stack = new UiStack(UiGap.Md);
            var first = new PanelActionButton("layout-a", new UiText(LocalizationKeys.Ui.PauseMenu.Back));
            var second = new PanelActionButton("layout-b", new UiText(LocalizationKeys.Ui.PauseMenu.Back));
            stack.AddItem(first);
            stack.AddItem(second);
            rootVisualElement.Add(stack);
            simulate.FrameUpdate();

            Assert.AreEqual(16f, second.worldBound.y - first.worldBound.yMax, 0.01f);
        }

        [Test]
        public void SetGapで段を変えると次のスタイル解決で間隔が変わる()
        {
            var stack = new UiStack(UiGap.Xs);
            var first = Box();
            var second = Box();
            stack.AddItem(first);
            stack.AddItem(second);
            rootVisualElement.Add(stack);
            simulate.FrameUpdate();

            stack.SetGap(UiGap.Xl);
            simulate.FrameUpdate();
            Assert.IsTrue(stack.ClassListContains("ui-stack--gap-xl"));
            Assert.IsFalse(stack.ClassListContains("ui-stack--gap-xs"));
            Assert.AreEqual(32f, second.resolvedStyle.marginTop, 0.01f);
        }

        [Test]
        public void ClearItemsで子と間隔の記録を空にする()
        {
            var stack = new UiStack(UiGap.Md);
            stack.AddItem(Box());
            stack.AddItem(Box());
            rootVisualElement.Add(stack);
            simulate.FrameUpdate();

            stack.ClearItems();
            var only = Box();
            stack.AddItem(only);
            simulate.FrameUpdate();
            Assert.AreEqual(1, stack.childCount);
            Assert.AreEqual(0f, only.resolvedStyle.marginTop, 0.01f);
        }

        [Test]
        public void ClearItems以外で外された子は間隔の数に入らない()
        {
            var stack = new UiStack(UiGap.Md);
            var first = Box();
            var second = Box();
            stack.AddItem(first);
            stack.AddItem(second);
            rootVisualElement.Add(stack);
            simulate.FrameUpdate();

            first.RemoveFromHierarchy();
            stack.SetItemShown(second, true);
            simulate.FrameUpdate();
            Assert.AreEqual(0f, second.resolvedStyle.marginTop, 0.01f);
        }

        [Test]
        public void 外した子を再びAddItemすると実際の先頭に余白が付かない()
        {
            var stack = new UiStack(UiGap.Md);
            var a = Box();
            var b = Box();
            var c = Box();
            stack.AddItem(a);
            stack.AddItem(b);
            stack.AddItem(c);
            rootVisualElement.Add(stack);
            simulate.FrameUpdate();

            // 実階層は B・C・A になる。先頭の B は余白なし、A は C の下に gap
            // The hierarchy becomes B, C, A; B has no space and A sits a gap below C
            a.RemoveFromHierarchy();
            stack.AddItem(a);
            simulate.FrameUpdate();
            Assert.AreEqual(0f, b.resolvedStyle.marginTop, 0.01f);
            Assert.AreEqual(16f, c.resolvedStyle.marginTop, 0.01f);
            Assert.AreEqual(16f, a.resolvedStyle.marginTop, 0.01f);
        }

        [Test]
        public void 外さずに再びAddItemしても二重に数えない()
        {
            var stack = new UiStack(UiGap.Md);
            var a = Box();
            var b = Box();
            stack.AddItem(a);
            stack.AddItem(b);
            rootVisualElement.Add(stack);
            simulate.FrameUpdate();

            stack.AddItem(a);
            simulate.FrameUpdate();
            Assert.AreEqual(0f, b.resolvedStyle.marginTop, 0.01f);
            Assert.AreEqual(16f, a.resolvedStyle.marginTop, 0.01f);
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
