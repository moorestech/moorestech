using System;
using Client.Ui.State.BuildMenu.Session;
using NUnit.Framework;

namespace Client.Tests.Ui.State.BuildMenu
{
    public class BuildMenuSessionStoreTest
    {
        /// <web-test file="src/features/buildMenu/sessionState/buildMenuSessionState.test.ts">初期状態は空検索・先頭スクロール・ホバー無し</web-test>
        [Test]
        public void 初期状態は空の検索語と先頭位置とホバー無し()
        {
            var current = new BuildMenuSessionStore().Current;
            Assert.AreEqual("", current.Query);
            Assert.AreEqual(0f, current.ScrollTop);
            Assert.IsNull(current.HoveredEntryId);
        }

        /// <web-test file="src/features/buildMenu/sessionState/buildMenuSessionState.test.ts">部分更新が累積し、他フィールドは保たれる</web-test>
        [Test]
        public void 部分の更新は積み重なり他の値は保たれる()
        {
            var store = new BuildMenuSessionStore();
            store.SetQuery("鉄");
            store.SetScrollTop(120f);
            Assert.AreEqual("鉄", store.Current.Query);
            Assert.AreEqual(120f, store.Current.ScrollTop);
            Assert.IsNull(store.Current.HoveredEntryId);
        }

        /// <web-test file="src/features/buildMenu/sessionState/buildMenuSessionState.test.ts">モジュール再読込で前テストの更新が持ち越されない</web-test>
        [Test]
        public void 新しいストアは前のストアの更新を持ち越さない()
        {
            // Web のモジュール大域はインスタンスへ置き換えたので、インスタンスごとに独立していることを確かめる
            // The Web module global became an instance, so verify each instance stands alone
            new BuildMenuSessionStore().SetQuery("鉄");
            Assert.AreEqual("", new BuildMenuSessionStore().Current.Query);
        }

        /// <web-test file="src/features/buildMenu/sessionState/buildMenuSessionState.test.ts">ホバー中エントリを保持し、null で解除できる</web-test>
        [Test]
        public void ホバー中のエントリを保持しnullで解除できる()
        {
            var store = new BuildMenuSessionStore();
            var entryId = new Guid("30000000-0000-4000-8000-000000000001");
            store.SetHoveredEntry(entryId);
            Assert.AreEqual(entryId, store.Current.HoveredEntryId);
            store.SetHoveredEntry(null);
            Assert.IsNull(store.Current.HoveredEntryId);
        }
    }
}
