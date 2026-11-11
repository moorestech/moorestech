using Client.Ui.Screens.BuildMenu.CategoryScroll;
using Client.Ui.State.BuildMenu.Session;
using NUnit.Framework;
using static Client.Tests.Ui.Screens.BuildMenu.FakeBuildMenuScrollViewport;

namespace Client.Tests.Ui.Screens.BuildMenu
{
    public class BuildMenuCategoryScrollAttachTest
    {
        private BuildMenuSessionStore _session;
        private BuildMenuCategoryScrollTracker _tracker;

        [SetUp]
        public void SetUp()
        {
            _session = new BuildMenuSessionStore();
            _tracker = new BuildMenuCategoryScrollTracker(_session);
        }

        [Test]
        public void 視口を外した後の遅延通知は例外を投げず保存位置も変えない()
        {
            var viewport = new FakeBuildMenuScrollViewport(600f, 1200f, 0f);
            viewport.SetHeadingTop(A, 0f);
            viewport.SetHeadingTop(B, 400f);
            _session.SetScrollTop(250f);
            _tracker.SetVisibleCategories(new[] { A, B });
            _tracker.AttachViewport(viewport);
            _tracker.JumpTo(B);
            _tracker.AttachViewport(null);

            // 外した後に届く 3 つの口は何もしない
            // The three entry points do nothing after the viewport is detached
            Assert.DoesNotThrow(() =>
            {
                _tracker.PersistScrollPosition();
                _tracker.OnUserIntervened();
                _tracker.OnScrolled(123f);
            });
            // 復元前に外したので保存位置 250 はそのまま残る
            // Detached before the restore, so the saved 250 is kept as is
            Assert.AreEqual(250f, _session.Current.ScrollTop);
        }

        [Test]
        public void 同じ視口を渡し直しても保存位置は潰れず末尾群の配置後に復元される()
        {
            _session.SetScrollTop(250f);
            var viewport = new FakeBuildMenuScrollViewport(600f, 700f, 0f);
            _tracker.SetVisibleCategories(new[] { Z, A });
            _tracker.AttachViewport(viewport);
            _tracker.AttachViewport(viewport);
            Assert.AreEqual(250f, _session.Current.ScrollTop);

            // 反映通知で保存位置へ戻る
            // The saved position is restored on the spacer report
            viewport.SetGroupHeight(A, 200f);
            _tracker.SetVisibleCategories(new[] { Y, A });
            viewport.ApplySpacer(_tracker.SpacerHeight.Value);
            _tracker.OnLayoutChanged();
            Assert.AreEqual(250f, viewport.ScrollTop);
        }

        [Test]
        public void スペーサの反映前に通知が重なっても保存位置の復元は反映後まで確定しない()
        {
            _session.SetScrollTop(250f);
            var viewport = new FakeBuildMenuScrollViewport(600f, 700f, 0f);
            _tracker.SetVisibleCategories(new[] { Z, A });
            _tracker.AttachViewport(viewport);
            viewport.SetGroupHeight(A, 200f);
            _tracker.SetVisibleCategories(new[] { Y, A });

            // 要求だけで未反映のまま再通知しても、古い scrollHeight で復元しない
            // Re-notifying before the spacer is applied must not restore on a stale scrollHeight
            _tracker.OnLayoutChanged();
            _tracker.OnLayoutChanged();
            Assert.AreEqual(0f, viewport.ScrollTop);
            Assert.AreEqual(250f, _session.Current.ScrollTop);

            viewport.ApplySpacer(_tracker.SpacerHeight.Value);
            _tracker.OnLayoutChanged();
            Assert.AreEqual(250f, viewport.ScrollTop);
        }

        [Test]
        public void スペーサの実寸が許容誤差内なら反映済みとして保存位置を復元する()
        {
            _session.SetScrollTop(250f);
            var viewport = new FakeBuildMenuScrollViewport(600f, 700f, 0f);
            _tracker.SetVisibleCategories(new[] { Z, A });
            _tracker.AttachViewport(viewport);
            viewport.SetGroupHeight(A, 200f);
            _tracker.SetVisibleCategories(new[] { Y, A });

            // 要求 400 に対し実寸 399.6（丸め誤差）でも Settled になる
            // A measured 399.6 against a requested 400 (rounding) still settles
            viewport.ApplySpacer(_tracker.SpacerHeight.Value - 0.4f);
            _tracker.OnLayoutChanged();
            Assert.AreEqual(250f, viewport.ScrollTop);
        }
    }
}
