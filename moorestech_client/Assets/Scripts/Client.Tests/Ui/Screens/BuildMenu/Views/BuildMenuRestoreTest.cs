using System.Collections.Generic;
using System.Linq;
using Client.Tests.Ui.Screens.BuildMenu.Support;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;
using static Client.Tests.Ui.Screens.BuildMenu.Support.BuildMenuSamples;

namespace Client.Tests.Ui.Screens.BuildMenu.Views
{
    public class BuildMenuRestoreTest : BuildMenuScreenTestHost
    {
        /// <web-test file="e2e/tests/regression/buildMenuRestore.spec.ts">閉じて開き直すと検索・スクロール・詳細stickyが復元される</web-test>
        [Test]
        public void 閉じて開き直すとスクロールと詳細のstickyが戻る()
        {
            OpenMenu(Create());
            Hover(Entry("block", Rail));
            StringAssert.Contains("鉄道レール", TextOf(Detail));

            // スクロールの前にポインタを一覧の外へ逃がす（下に来た別のエントリへ詳細が移らないように。Web と同じ）
            // Park the pointer outside the list before scrolling so the detail never moves to the entry sliding under it (same as the Web)
            simulate.MouseMove(Entry("block", Rail).worldBound.center, new Vector2(1f, 1f));
            var top = HeadingTop(Transport);
            Scroll.SetScrollOffset(top);
            Settle();
            CloseMenu();
            Assert.IsFalse(IsShown(View));

            OpenMenu(Create());

            StringAssert.Contains("鉄道レール", TextOf(Detail));
            Assert.AreEqual(top, Scroll.scrollOffset.y, 0.5f);
        }

        [Test]
        public void スペーサ反映後の高さで保存位置を復元する()
        {
            // 末尾の見出しの位置はスペーサ込みでしか届かない。反映前の高さで復元すると手前へクランプされる（L-2）
            // The last heading is reachable only with the spacer; restoring on the pre-apply height clamps short (L-2)
            OpenMenu(Create());
            simulate.Click(CategoryButtons().Last());
            Settle();
            var saved = Scroll.scrollOffset.y;
            Assert.Greater(Tracker.SpacerHeight.Value, 0f);
            CloseMenu();

            OpenMenu(Create());

            Assert.AreEqual(saved, Scroll.scrollOffset.y, 0.5f);
            Assert.AreEqual(saved, Session.Current.ScrollTop, 0.5f);
        }

        [Test]
        public void 末尾の群が視口より高くても閉じて開き直すと保存位置へ戻る()
        {
            // スペーサが 0 のままの構成。閉じた後の高さ 0 を実測と取り違えると、復元を 0 で確定して保存位置まで潰す
            // A layout whose spacer stays 0; mistaking the post-hide 0 height for a measurement settles the restore at 0 and overwrites the saved position
            OpenMenu(Create());
            var lastGroup = Find<VisualElement>(View, $"build-menu-category-{CategoryButtons().Last().name.Replace("build-menu-category-", "")}-group");
            lastGroup.style.minHeight = Scroll.contentViewport.layout.height * 2f;
            Settle();
            Assert.AreEqual(0f, Tracker.SpacerHeight.Value, 0.01f);
            var saved = HeadingTop(Transport) + 10f;
            Scroll.SetScrollOffset(saved);
            Settle();
            CloseMenu();

            OpenMenu(Create());

            Assert.AreEqual(saved, Scroll.scrollOffset.y, 0.5f);
            Assert.AreEqual(saved, Session.Current.ScrollTop, 0.5f);
        }

        [Test]
        public void 開き直した視口へ要求済みのスペーサ高を当て直す()
        {
            OpenMenu(Create());
            var requested = Tracker.SpacerHeight.Value;
            Assert.Greater(requested, 0f);
            CloseMenu();

            // 閉じている間にスペーサを未適用へ戻す（要求値は変えないので購読は再通知しない）。張り替えの当て直しだけが高さを戻せる
            // Reset the spacer to unapplied while closed (the request is unchanged, so the subscription never re-fires); only the re-apply on attach can restore it
            var spacer = Find<VisualElement>(View, "build-menu-trailing-spacer");
            spacer.style.minHeight = StyleKeyword.Null;
            spacer.style.height = StyleKeyword.Null;
            simulate.FrameUpdate();
            Assert.AreEqual(requested, Tracker.SpacerHeight.Value, 0.01f);
            Assert.Less(spacer.layout.height, requested - 0.5f);

            OpenMenu(Create());

            // 新しい視口の AppliedSpacerHeight が要求値と同じ（L-4。Attach の当て直しを消すとここで赤）
            // The new viewport's applied spacer height matches the request (L-4; removing the re-apply in Attach turns this red)
            Assert.AreEqual(requested, spacer.layout.height, 0.5f);
        }

        [Test]
        public void 復元の前に流れたスクロール通知で保存位置を0へ潰さない()
        {
            // L-3 の観測: 開く間に ScrollView が流した位置を記録して結果へ出し、保存位置が保たれることを主張する（D-8）
            // L-3 observation: record the offsets the ScrollView emits while opening, print them, and assert the saved position survives (D-8)
            var emitted = new List<float>();
            Session.SetScrollTop(150f);
            using (Scroll.OnScrollOffsetChanged.Subscribe(emitted.Add))
            {
                OpenMenu(Create());
            }

            TestContext.WriteLine($"[L-3] offsets emitted while opening: {string.Join(",", emitted.Select(offset => offset.ToString("F1")))}");
            Assert.AreEqual(150f, Scroll.scrollOffset.y, 0.5f);
            Assert.AreEqual(150f, Session.Current.ScrollTop, 0.5f);
        }

        [Test]
        public void 復元の前に閉じても保存位置を上書きしない()
        {
            // 開いて最初のレイアウトが済む前に閉じる。復元前の位置で保存位置を潰さないため、保存位置は残る
            // Close before the first layout settles; a pre-restore position must not flatten the saved one, so it survives
            Session.SetScrollTop(150f);
            View.Render(Create());
            View.SetShown(true);
            CloseMenu();
            Assert.AreEqual(150f, Session.Current.ScrollTop, 0.5f);

            OpenMenu(Create());

            Assert.AreEqual(150f, Scroll.scrollOffset.y, 0.5f);
            Assert.AreEqual(150f, Session.Current.ScrollTop, 0.5f);
        }
    }
}
