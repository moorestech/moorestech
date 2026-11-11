using System.IO;
using System.Text.RegularExpressions;
using Client.Tests.Ui.Components;
using Client.Tests.Ui.State.Notification;
using Client.Ui.Screens.Notification;
using Client.Ui.State.Notification;
using NUnit.Framework;
using static Client.Tests.Ui.State.Notification.NotificationTestContents;

namespace Client.Tests.Ui.Screens.Notification
{
    /// <summary>
    /// 通知の行の出入りと除去の時間を確かめる（L1。Web の notificationAnimationDesign.test.ts 4 件と e2e animation.spec.ts）
    /// Verifies the timing of row enter, exit and removal (L1; 4 tests of Web notificationAnimationDesign.test.ts and e2e animation.spec.ts)
    /// </summary>
    public class NotificationHostViewTest : NotificationHostViewTestFixture
    {
        private const string UssPath = "Assets/Scripts/Client.Ui/Screens/Notification/Notification.uss";

        /// <web-test file="src/features/notification/notificationAnimationDesign.test.ts">生存尺はstoreが唯一の正で、CSSへ尺を直書きしない</web-test>
        [Test]
        public void 生存尺はModelの定数だけが持ちUSSへ尺を書かない()
        {
            Assert.IsFalse(Regex.IsMatch(File.ReadAllText(UssPath), @"7000ms|\b7s\b"));
            Model.AddNotification(Earned(7, 1, "itemEarned.mined"));
            AdvancePanel(0);
            AdvancePanel(NotificationModel.DisplayMilliseconds - 100);
            Assert.AreEqual(1, Host.childCount);
            AdvancePanel(200);
            AdvancePanel(0);
            Assert.AreEqual(0, Host.childCount);
        }

        /// <web-test file="src/features/notification/notificationAnimationDesign.test.ts">移動量と尺はトークンで管理する</web-test>
        [Test]
        public void 移動量と尺はトークンで管理する()
        {
            Assert.AreEqual("160ms", ThemeTokenFile.Resolve("--notification-enter-duration"));
            Assert.AreEqual("200ms", ThemeTokenFile.Resolve("--notification-exit-duration"));
            Assert.AreEqual("12px", ThemeTokenFile.Resolve("--notification-shift"));
            var uss = File.ReadAllText(UssPath);
            StringAssert.Contains("var(--notification-enter-duration)", uss);
            StringAssert.Contains("var(--notification-exit-duration)", uss);
            StringAssert.Contains("var(--notification-shift)", uss);
        }

        /// <web-test file="src/features/notification/notificationAnimationDesign.test.ts">除去は退場アニメの終了が主で、タイマーは尺より後ろの保険に留める</web-test>
        [Test]
        public void 除去は退場の動きの終わりだけが起こす()
        {
            Model.AddNotification(Earned(7, 1, "itemEarned.mined"));
            AdvancePanel(0);
            AdvancePanel(NotificationModel.DisplayMilliseconds - 50);
            Assert.AreEqual(1, Model.Current.Value.Rows.Count);
            AdvancePanel(100);
            AdvancePanel(0);

            // 保険タイマーは無い（D-2）ので、消したのは退場の動きの終わり
            // There is no fallback timer (D-2), so the exit motion's end did the removal
            Assert.AreEqual(0, Model.Current.Value.Rows.Count);
            Assert.AreEqual(0, Host.childCount);
        }

        /// <web-test file="src/features/notification/notificationAnimationDesign.test.ts">生存尺はホストでなく各行へ渡し、面と同じ要素の上で解決させる</web-test>
        [Test]
        public void 生存は行ごとに回り面と同じ要素が薄れる()
        {
            Model.AddNotification(Earned(7, 1, "itemEarned.mined"));
            AdvancePanel(0);
            AdvancePanel(1000);
            Model.AddNotification(Earned(9, 1, "itemEarned.mined"));
            AdvancePanel(0);
            AdvancePanel(6100);
            AdvancePanel(0);

            // 先の行だけが消え、後の行は残る。ホスト自身は薄れない
            // Only the earlier row is gone; the later row stays and the host itself never fades
            Assert.AreEqual(1, Host.childCount);
            Assert.AreEqual(1f, Host.resolvedStyle.opacity, 0.0001f);
            AdvancePanel(1000);
            AdvancePanel(0);
            Assert.AreEqual(0, Host.childCount);
        }

        /// <web-test file="e2e/tests/notification/animation.spec.ts">通知は左からのスライドとフェードで入場し生存尺の終端で退場する</web-test>
        [Test]
        public void 通知は左からのスライドとフェードで入場し生存尺の終端で退場する()
        {
            Model.AddNotification(Earned(7, 1, "itemEarned.mined"));
            AdvancePanel(0);
            AdvancePanel(80);
            var row = RowAt(0);
            Assert.That(row.resolvedStyle.opacity, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(row.resolvedStyle.translate.x, Is.LessThan(0f).And.GreaterThan(-ThemeTokenFile.ResolvePixels("--notification-shift")));

            AdvancePanel(3000);
            Assert.AreEqual(1f, row.resolvedStyle.opacity, 0.0001f);
            Assert.AreEqual(0f, row.resolvedStyle.translate.x, 0.01f);

            AdvancePanel(NotificationModel.DisplayMilliseconds - 3080 - 100);
            Assert.That(row.resolvedStyle.opacity, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(row.resolvedStyle.translate.x, Is.LessThan(0f));
            AdvancePanel(200);
            AdvancePanel(0);
            Assert.AreEqual(0, Host.childCount);
        }

        [Test]
        public void 画面USSが当たらないまま描かれると開発者にエラーを出す()
        {
            // 規則の当たらないホスト（tss の @import 漏れと同じ状態）を載せ、預かった描画が無音で消えないことを確かめる
            // Mount a host no rule matches (same as a missing tss @import) and make sure the held render never vanishes silently
            var bare = new NotificationHostView(new NotificationModel(), new FakeNotificationTextLookup(), new FakeNotificationRowIcons());
            bare.RemoveFromClassList("notification-host");
            bare.Render(NotificationListViewModel.Empty);
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, new Regex(@"^\[NotificationHostView\] custom style not resolved"));
            Mount(bare);
            AdvancePanel(0);
            AdvancePanel(0);
        }

        [Test]
        public void 合流で生存尺が回り直し動きを頭から再生する()
        {
            Model.AddNotification(Earned(7, 5, "itemEarned.mined"));
            AdvancePanel(0);
            AdvancePanel(5000);
            Model.AddNotification(Earned(7, 3, "itemEarned.mined"));
            AdvancePanel(0);
            AdvancePanel(80);
            Assert.AreEqual(1, Host.childCount);
            Assert.That(RowAt(0).resolvedStyle.opacity, Is.LessThan(1f));

            // 最初の生存尺（7000ms）を越えても、回り直した行は残る
            // Past the first lifetime (7000ms), the restarted row stays
            AdvancePanel(3000);
            Assert.AreEqual(1, Host.childCount);
            AdvancePanel(4000);
            AdvancePanel(0);
            Assert.AreEqual(0, Host.childCount);
        }
    }
}
