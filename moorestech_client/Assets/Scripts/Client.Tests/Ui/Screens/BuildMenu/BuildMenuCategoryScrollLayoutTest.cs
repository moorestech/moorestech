using Client.Ui.Screens.BuildMenu.CategoryScroll;
using Client.Ui.State.BuildMenu.Session;
using NUnit.Framework;
using static Client.Tests.Ui.Screens.BuildMenu.FakeBuildMenuScrollViewport;

namespace Client.Tests.Ui.Screens.BuildMenu
{
    public class BuildMenuCategoryScrollLayoutTest
    {
        private BuildMenuSessionStore _session;
        private BuildMenuCategoryScrollTracker _tracker;

        [SetUp]
        public void SetUp()
        {
            _session = new BuildMenuSessionStore();
            _tracker = new BuildMenuCategoryScrollTracker(_session);
        }

        /// <web-test file="src/features/buildMenu/hooks/useBuildMenuCategoryScroll.test.ts">spacerHeightは視口高から末尾群高を引いた値になる(0クランプ込み)</web-test>
        [Test]
        public void スペーサは視口高から末尾群の高さを引いた値で0未満にならない()
        {
            var viewport = new FakeBuildMenuScrollViewport(600f, 700f, 0f);
            _tracker.SetVisibleCategories(new[] { Z, A });
            _tracker.AttachViewport(viewport);
            viewport.SetGroupHeight(A, 200f);
            _tracker.SetVisibleCategories(new[] { Y, A });
            Assert.AreEqual(400f, _tracker.SpacerHeight.Value);

            viewport.SetGroupHeight(A, 900f);
            _tracker.SetVisibleCategories(new[] { X, A });
            Assert.AreEqual(0f, _tracker.SpacerHeight.Value);
        }

        /// <web-test file="src/features/buildMenu/hooks/useBuildMenuCategoryScroll.test.ts">上方カテゴリのエントリが減って見出し位置がずれると、ハイライトがResizeObserver経由で追従する(D1案C回帰)</web-test>
        [Test]
        public void 上のカテゴリが縮んで見出しがずれるとハイライトが寸法の通知で追従する()
        {
            _session.SetScrollTop(350f);
            var viewport = new FakeBuildMenuScrollViewport(600f, 1200f, 350f);
            viewport.SetHeadingTop(A, 0f);
            viewport.SetHeadingTop(B, 400f);
            viewport.SetGroupHeight(A, 400f);
            viewport.SetGroupHeight(B, 200f);
            _tracker.SetVisibleCategories(new[] { A, B });
            _tracker.AttachViewport(viewport);
            _tracker.OnLayoutChanged();
            Assert.AreEqual(A, _tracker.ActiveCategory.Value);

            // A が縮んで B の見出しが 300 へ上がる。表示群・視口高・末尾群は変わらない
            // A shrinks and B's heading rises to 300; the visible set, viewport height and last group stay unchanged
            viewport.SetHeadingTop(B, 300f);
            viewport.SetGroupHeight(A, 300f);
            _tracker.OnLayoutChanged();
            Assert.AreEqual(B, _tracker.ActiveCategory.Value);
        }

        /// <web-test file="src/features/buildMenu/hooks/useBuildMenuCategoryScroll.test.ts">視口や末尾群のリサイズにspacerHeightがResizeObserver経由で追従する(カテゴリ集合は変わらない)</web-test>
        [Test]
        public void 視口や末尾群の寸法の変化にスペーサが追従する()
        {
            var viewport = new FakeBuildMenuScrollViewport(600f, 1200f, 0f);
            viewport.SetGroupHeight(A, 200f);
            _tracker.SetVisibleCategories(new[] { A });
            _tracker.AttachViewport(viewport);
            Assert.AreEqual(400f, _tracker.SpacerHeight.Value);

            viewport.SetClientHeight(900f);
            viewport.ApplySpacer(_tracker.SpacerHeight.Value);
            _tracker.OnLayoutChanged();
            Assert.AreEqual(700f, _tracker.SpacerHeight.Value);

            viewport.SetGroupHeight(A, 800f);
            viewport.ApplySpacer(_tracker.SpacerHeight.Value);
            _tracker.OnLayoutChanged();
            Assert.AreEqual(100f, _tracker.SpacerHeight.Value);
        }

        /// <web-test file="src/features/buildMenu/hooks/useBuildMenuCategoryScroll.test.ts">開いた直後、scrollイベントを一度も発火せずに初期ハイライトが視口位置から決まる</web-test>
        [Test]
        public void 開いた直後はスクロール無しで視口の位置から初期ハイライトが決まる()
        {
            var viewport = new FakeBuildMenuScrollViewport(600f, 1200f, 400f);
            viewport.SetHeadingTop(A, 0f);
            viewport.SetHeadingTop(B, 400f);
            _tracker.SetVisibleCategories(new[] { A });
            _tracker.AttachViewport(viewport);
            _tracker.SetVisibleCategories(new[] { A, B });
            Assert.AreEqual(B, _tracker.ActiveCategory.Value);
        }

        /// <web-test file="src/features/buildMenu/hooks/useBuildMenuCategoryScroll.test.ts">末尾群が未マウントの間は復元も保存位置の上書きもしない</web-test>
        [Test]
        public void 末尾群が未配置の間は復元も保存位置の上書きもしない()
        {
            _session.SetScrollTop(250f);
            var viewport = new FakeBuildMenuScrollViewport(600f, 700f, 0f);
            _tracker.SetVisibleCategories(new[] { Z, A });
            _tracker.AttachViewport(viewport);
            Assert.AreEqual(0f, _tracker.SpacerHeight.Value);
            Assert.AreEqual(0f, viewport.ScrollTop);
            Assert.AreEqual(250f, _session.Current.ScrollTop);

            // 末尾群が載ってスペーサが決まり、View が反映を通知した時点で保存位置へ戻す
            // Once the last group lands and the view reports the applied spacer, restore the saved position
            viewport.SetGroupHeight(A, 200f);
            _tracker.SetVisibleCategories(new[] { Y, A });
            Assert.AreEqual(400f, _tracker.SpacerHeight.Value);
            Assert.AreEqual(0f, viewport.ScrollTop);
            Assert.AreEqual(250f, _session.Current.ScrollTop);
            viewport.ApplySpacer(_tracker.SpacerHeight.Value);
            _tracker.OnLayoutChanged();
            Assert.AreEqual(250f, viewport.ScrollTop);
            Assert.AreEqual(250f, _session.Current.ScrollTop);
        }

        /// <web-test file="src/features/buildMenu/hooks/useBuildMenuCategoryScroll.test.ts">末尾ジャンプ中にスペーサが伸びたら、反映後の高さで目標を引き直す</web-test>
        [Test]
        public void 末尾へのジャンプ中にスペーサが伸びたら反映後の高さで目標を引き直す()
        {
            var viewport = new FakeBuildMenuScrollViewport(600f, 700f, 0f);
            viewport.SetHeadingTop(A, 0f);
            viewport.SetHeadingTop(B, 300f);
            _tracker.SetVisibleCategories(new[] { A, B });
            _tracker.AttachViewport(viewport);

            // スペーサ未実測では最大スクロールが 100 しかなく、目標はクランプされる
            // Without the spacer the max scroll is only 100, so the target is clamped
            _tracker.JumpTo(B);
            Assert.AreEqual(100f, viewport.SmoothScrollRequests[viewport.SmoothScrollRequests.Count - 1]);

            // 内容高とスペーサが同時に伸びる。反映前の通知では反映前の高さ(800)で目標を引き直さない
            // Content and spacer grow together; the pre-apply notification must not re-target on the pre-apply height (800)
            var requestsBefore = viewport.SmoothScrollRequests.Count;
            viewport.SetContentHeight(800f);
            viewport.SetGroupHeight(B, 100f);
            _tracker.OnLayoutChanged();
            Assert.AreEqual(500f, _tracker.SpacerHeight.Value);
            Assert.AreEqual(requestsBefore, viewport.SmoothScrollRequests.Count);

            // View が反映して通知すると、反映後の高さ(1300)で目標を引き直す
            // After the view applies the spacer and notifies, re-target on the applied height (1300)
            viewport.ApplySpacer(_tracker.SpacerHeight.Value);
            _tracker.OnLayoutChanged();
            Assert.AreEqual(300f, viewport.SmoothScrollRequests[viewport.SmoothScrollRequests.Count - 1]);
        }

        [Test]
        public void 閉じる直前の位置を保存する()
        {
            // 末尾群が視口と同じ高さならスペーサ 0 で反映済みとなり、張った時点で復元が済む
            // A last group as tall as the viewport needs a 0 spacer, so the restore completes on attach
            var viewport = new FakeBuildMenuScrollViewport(600f, 1200f, 0f);
            viewport.SetGroupHeight(A, 600f);
            _tracker.SetVisibleCategories(new[] { A });
            _tracker.AttachViewport(viewport);
            viewport.SetScrollTop(180f);
            _tracker.PersistScrollPosition();
            Assert.AreEqual(180f, _session.Current.ScrollTop);
        }

        [Test]
        public void 復元前の位置は閉じても保存しない()
        {
            // 末尾群が未配置で復元前のまま閉じても、保存位置は残る（復元前の位置で保存位置を潰さないため）
            // Closing before the restore, while the last group is unplaced, keeps the saved position (a pre-restore position must not flatten the saved one)
            _session.SetScrollTop(250f);
            var viewport = new FakeBuildMenuScrollViewport(600f, 1200f, 0f);
            _tracker.SetVisibleCategories(new[] { A });
            _tracker.AttachViewport(viewport);
            _tracker.PersistScrollPosition();
            Assert.AreEqual(250f, _session.Current.ScrollTop);
        }
    }
}
