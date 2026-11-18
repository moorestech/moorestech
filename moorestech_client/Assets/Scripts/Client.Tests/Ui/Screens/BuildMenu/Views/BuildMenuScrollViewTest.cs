using System;
using System.Linq;
using Client.Tests.Ui.Screens.BuildMenu.Support;
using NUnit.Framework;
using UnityEngine.UIElements;
using static Client.Tests.Ui.Screens.BuildMenu.Support.BuildMenuSamples;

namespace Client.Tests.Ui.Screens.BuildMenu.Views
{
    public class BuildMenuScrollViewTest : BuildMenuScreenTestHost
    {
        /// <web-test file="e2e/tests/regression/buildMenu.spec.ts">全カテゴリが1本スクロールに並び、サイドバー押下で見出しへジャンプしハイライトが追従する</web-test>
        [Test]
        public void サイドバーを押すと見出しへ移りハイライトが手のスクロールにも追従する()
        {
            OpenMenu(Create());
            Assert.IsTrue(Scroll.contentContainer.Contains(Find<VisualElement>(View, $"build-menu-category-heading-{Transport:D}")));
            Assert.IsTrue(Scroll.contentContainer.Contains(Entry("block", Rail)));

            simulate.Click(CategoryButton(Transport));
            Settle();

            Assert.AreEqual(HeadingTop(Transport), Scroll.scrollOffset.y, 1f);
            Assert.IsTrue(IsSelected(View, $"build-menu-category-{Transport:D}"));
            Assert.IsFalse(IsSelected(View, $"build-menu-category-{Logistics:D}"));

            // 手のスクロールでもハイライトが追従する
            // The highlight follows manual scrolling too
            Scroll.SetScrollOffset(0f);
            Settle();
            Assert.IsTrue(IsSelected(View, $"build-menu-category-{Logistics:D}"));
        }

        /// <web-test file="e2e/tests/regression/buildMenu.spec.ts">末尾カテゴリへジャンプしても見出しが視口上端に来る</web-test>
        [Test]
        public void 末尾カテゴリへジャンプしても見出しが視口の上端に来る()
        {
            OpenMenu(Create());
            var last = CategoryButtons().Last();
            var lastGuid = Guid.Parse(last.name.Replace("build-menu-category-", ""));

            simulate.Click(last);
            Settle();

            // スペーサ反映後の GeometryChangedEvent で引き直さないと手前で止まる（L-1）
            // Without re-targeting on the post-spacer GeometryChangedEvent the jump stops short (L-1)
            Assert.Greater(Tracker.SpacerHeight.Value, 0f);
            Assert.AreEqual(HeadingTop(lastGuid), Scroll.scrollOffset.y, 1f);
            Assert.IsTrue(IsSelected(View, last.name));
        }

        [Test]
        public void ジャンプ中に押下すると固定を解いて途中で止まる()
        {
            OpenMenu(Create());
            var target = HeadingTop(Transport);
            simulate.Click(CategoryButton(Transport));
            simulate.FrameUpdateMs(0);
            simulate.FrameUpdateMs(100);

            // ホイール・押下・キーは同じ口なので押下で代表させる（D-11）。target の無い PointerDownEvent は届かないので、見出し（エントリでない所）を押す（前例 UiDragDropTest）
            // Wheel, press and key share one handler, so a press stands for all three (D-11); a target-less PointerDownEvent never arrives, so press a heading, not an entry (precedent UiDragDropTest)
            var heading = Find<VisualElement>(View, $"build-menu-category-heading-{Transport:D}").worldBound.center;
            simulate.MouseDown(heading, MouseButton.LeftMouse);
            simulate.MouseUp(heading, MouseButton.LeftMouse);
            Settle();

            Assert.Greater(Scroll.scrollOffset.y, 0f);
            Assert.Less(Scroll.scrollOffset.y, target - 1f);
        }

        [Test]
        public void 全体の高さが同じまま群の高さの配分が変わってもハイライトを引き直す()
        {
            OpenMenu(Create());
            Scroll.SetScrollOffset(HeadingTop(Transport) + 1f);
            Settle();
            Assert.IsTrue(IsSelected(View, $"build-menu-category-{Transport:D}"));
            var contentHeight = Scroll.contentContainer.layout.height;

            // 先頭の群を伸ばし 2 つ目の群を同じだけ縮める。末尾群とスペーサは変わらず、中身の高さの通知は来ない（D-9）
            // Grow the first group and shrink the second by the same amount; the last group and spacer stay, so no content-height notification comes (D-9)
            const float shift = 40f;
            var first = Find<VisualElement>(View, $"build-menu-category-{Logistics:D}-group");
            var second = Find<VisualElement>(View, $"build-menu-category-{Transport:D}-group");
            Assert.Greater(second.layout.height, shift * 2f);
            first.style.height = first.layout.height + shift;
            second.style.height = second.layout.height - shift;
            second.style.overflow = Overflow.Hidden;
            Settle();

            // 見出しが下へずれたので、同じ位置では先頭のカテゴリが選ばれる
            // The heading moved down, so the same offset now selects the first category
            Assert.AreEqual(contentHeight, Scroll.contentContainer.layout.height, 0.5f);
            Assert.IsTrue(IsSelected(View, $"build-menu-category-{Logistics:D}"));
        }
    }
}
