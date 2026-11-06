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
            Assert.AreEqual(0f, _session.Current.ScrollTop);
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
    }
}
