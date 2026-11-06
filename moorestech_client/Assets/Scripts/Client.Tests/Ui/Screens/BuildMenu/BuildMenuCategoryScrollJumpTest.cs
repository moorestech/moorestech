using Client.Ui.Screens.BuildMenu.CategoryScroll;
using Client.Ui.State.BuildMenu.Session;
using NUnit.Framework;
using static Client.Tests.Ui.Screens.BuildMenu.FakeBuildMenuScrollViewport;

namespace Client.Tests.Ui.Screens.BuildMenu
{
    public class BuildMenuCategoryScrollJumpTest
    {
        private BuildMenuCategoryScrollTracker _tracker;

        [SetUp]
        public void SetUp()
        {
            _tracker = new BuildMenuCategoryScrollTracker(new BuildMenuSessionStore());
        }

        /// <web-test file="src/features/buildMenu/hooks/useBuildMenuCategoryScroll.test.ts">jumpTo後、目標未到達のscrollではハイライトが動かない</web-test>
        [Test]
        public void ジャンプ後に目標未到達のスクロールではハイライトが動かない()
        {
            var viewport = AttachTwoHeadings(new FakeBuildMenuScrollViewport(600f, 1200f, 0f));
            _tracker.JumpTo(B);
            CollectionAssert.AreEqual(new[] { 400f }, viewport.SmoothScrollRequests);
            Assert.AreEqual(B, _tracker.ActiveCategory.Value);

            // 距離 200 では固定のまま
            // Still pinned at distance 200
            _tracker.OnScrolled(200f);
            Assert.AreEqual(B, _tracker.ActiveCategory.Value);
        }

        /// <web-test file="src/features/buildMenu/hooks/useBuildMenuCategoryScroll.test.ts">目標到達scrollで固定が解除されscroll-spyへ戻る</web-test>
        [Test]
        public void 目標に届くと固定が解けてスクロール追従へ戻る()
        {
            AttachTwoHeadings(new FakeBuildMenuScrollViewport(600f, 1200f, 0f));
            _tracker.JumpTo(B);
            _tracker.OnScrolled(400f);
            Assert.AreEqual(B, _tracker.ActiveCategory.Value);
            _tracker.OnScrolled(0f);
            Assert.AreEqual(A, _tracker.ActiveCategory.Value);
        }

        /// <web-test file="src/features/buildMenu/hooks/useBuildMenuCategoryScroll.test.ts">既に目標位置ならジャンプ固定が即座に解除される</web-test>
        [Test]
        public void すでに目標の位置ならジャンプの固定はすぐ解ける()
        {
            var viewport = AttachTwoHeadings(new FakeBuildMenuScrollViewport(600f, 1200f, 400f));
            _tracker.JumpTo(B);
            Assert.AreEqual(0, viewport.SmoothScrollRequests.Count);
            _tracker.OnScrolled(0f);
            Assert.AreEqual(A, _tracker.ActiveCategory.Value);
        }

        /// <web-test file="src/features/buildMenu/hooks/useBuildMenuCategoryScroll.test.ts">ジャンプ中にviewportへwheelを送ると、目標未到達でも固定が解除されscroll-spyへ戻る(D2案A)</web-test>
        [Test]
        public void ジャンプ中にホイールを回すと未到達でも固定が解ける()
        {
            var viewport = AttachTwoHeadings(new FakeBuildMenuScrollViewport(600f, 1200f, 0f));
            _tracker.JumpTo(B);
            viewport.SetScrollTop(50f);
            _tracker.OnUserIntervened();
            Assert.AreEqual(A, _tracker.ActiveCategory.Value);

            // 解除の後はスクロール追従がそのまま続く
            // After release, scroll-spy tracking resumes normally
            _tracker.OnScrolled(400f);
            Assert.AreEqual(B, _tracker.ActiveCategory.Value);
        }

        /// <web-test file="src/features/buildMenu/hooks/useBuildMenuCategoryScroll.test.ts">visibleCategoryGuidsが同内容の別配列で再レンダーされてもジャンプ固定が維持される</web-test>
        [Test]
        public void 同じ並びの表示群を渡し直してもジャンプの固定は保たれる()
        {
            AttachTwoHeadings(new FakeBuildMenuScrollViewport(600f, 1200f, 0f));
            _tracker.JumpTo(B);
            _tracker.SetVisibleCategories(new[] { A, B });
            Assert.AreEqual(B, _tracker.ActiveCategory.Value);
        }

        /// <web-test file="src/features/buildMenu/hooks/useBuildMenuCategoryScroll.test.ts">末尾付近でクランプが効いても到達後は目標カテゴリがハイライトされる</web-test>
        [Test]
        public void 末尾でクランプされても到達後は目標のカテゴリが光る()
        {
            // 内容(500)が視口(600)より低いので最大スクロール位置はマイナスへクランプされる
            // Content (500) is shorter than the viewport (600), so the max scroll clamps below zero
            var viewport = AttachTwoHeadings(new FakeBuildMenuScrollViewport(600f, 500f, 0f));
            _tracker.JumpTo(B);
            CollectionAssert.AreEqual(new[] { -100f }, viewport.SmoothScrollRequests);
            _tracker.OnScrolled(-100f);
            Assert.AreEqual(B, _tracker.ActiveCategory.Value);
        }

        /// <web-test file="src/features/buildMenu/hooks/useBuildMenuCategoryScroll.test.ts">表示群が入れ替わった後のwheel解除でも最新の見出し集合で現在地を測り直す</web-test>
        [Test]
        public void 表示群が入れ替わった後のホイール解除も最新の見出しで測り直す()
        {
            var viewport = new FakeBuildMenuScrollViewport(600f, 1200f, 0f);
            viewport.SetHeadingTop(A, 0f);
            _tracker.SetVisibleCategories(new[] { A });
            _tracker.AttachViewport(viewport);
            _tracker.SetVisibleCategories(new[] { A, B });
            viewport.SetHeadingTop(B, 400f);

            _tracker.JumpTo(B);
            viewport.SetScrollTop(400f);
            _tracker.OnUserIntervened();
            Assert.AreEqual(B, _tracker.ActiveCategory.Value);
        }

        private FakeBuildMenuScrollViewport AttachTwoHeadings(FakeBuildMenuScrollViewport viewport)
        {
            viewport.SetHeadingTop(A, 0f);
            viewport.SetHeadingTop(B, 400f);
            _tracker.SetVisibleCategories(new[] { A, B });
            _tracker.AttachViewport(viewport);
            return viewport;
        }
    }
}
