using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Client.Ui.State.Notification;
using Client.Ui.State.Notification.Content;
using Core.Master;
using NUnit.Framework;
using static Client.Tests.Ui.State.Notification.NotificationTestContents;

namespace Client.Tests.Ui.State.Notification
{
    /// <summary>
    /// 通知の行の一覧と獲得の合流を確かめる（L0。Web の notificationStore.test.ts。保険タイマーの 1 件は捨てる＝D-2）
    /// Verifies the notification rows and earned-row merging (L0; Web notificationStore.test.ts; the fallback-timer test is dropped, D-2)
    /// </summary>
    public class NotificationModelTest
    {
        private NotificationModel _model;

        private IReadOnlyList<NotificationRow> Rows => _model.Current.Value.Rows;

        [SetUp]
        public void CreateModel()
        {
            _model = new NotificationModel();
        }

        /// <web-test file="src/features/notification/notificationStore.test.ts">itemIdを保持する</web-test>
        [Test]
        public void 拒否の行もアイコンのアイテムを保持する()
        {
            _model.AddNotification(Denied("denied.craftMaterialShortage", NoParams, ItemIcon(42)));
            Assert.That(Rows[0].Content.Icon, Is.TypeOf<ItemNotificationIcon>().With.Property(nameof(ItemNotificationIcon.ItemId)).EqualTo(new ItemId(42)));
        }

        /// <web-test file="src/features/notification/notificationStore.test.ts">退場用の状態を持たない</web-test>
        [Test]
        public void 行は退場用の状態を持たない()
        {
            var names = typeof(NotificationRow).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(property => property.Name).OrderBy(name => name);
            CollectionAssert.AreEqual(new[] { "Content", "Id", "LifetimeEpoch" }, names);
        }

        /// <web-test file="src/features/notification/notificationStore.test.ts">表示中の同一アイテムの獲得は1行に加算される</web-test>
        [Test]
        public void 表示中の同一アイテムの獲得は1行に加算される()
        {
            _model.AddNotification(Earned(7, 5, "itemEarned.mined"));
            _model.AddNotification(Earned(7, 3, "itemEarned.mined"));
            Assert.AreEqual(1, Rows.Count);
            Assert.AreEqual(8, EarnedAt(0).Count);
        }

        /// <web-test file="src/features/notification/notificationStore.test.ts">加算時はidを据え置きlifetimeEpochで生存尺が回り直す</web-test>
        [Test]
        public void 加算時はidを据え置きLifetimeEpochで生存尺が回り直す()
        {
            // 生存尺の回り直しは epoch の進みで View に伝わる（時間は View が持つ。再生の頭出しは NotificationHostViewTest）
            // A lifetime restart reaches the View as an advanced epoch (the View owns time; the replay is in NotificationHostViewTest)
            _model.AddNotification(Earned(7, 5, "itemEarned.mined"));
            var firstId = Rows[0].Id;
            _model.AddNotification(Earned(7, 3, "itemEarned.mined"));
            Assert.AreEqual(firstId, Rows[0].Id);
            Assert.AreEqual(1, Rows[0].LifetimeEpoch);
            _model.AddNotification(Earned(7, 1, "itemEarned.mined"));
            Assert.AreEqual(firstId, Rows[0].Id);
            Assert.AreEqual(2, Rows[0].LifetimeEpoch);
        }

        /// <web-test file="src/features/notification/notificationStore.test.ts">加算しても表示順は入れ替わらない</web-test>
        [Test]
        public void 加算しても表示順は入れ替わらない()
        {
            _model.AddNotification(Earned(7, 5, "itemEarned.mined"));
            _model.AddNotification(Earned(9, 1, "itemEarned.mined"));
            _model.AddNotification(Earned(7, 3, "itemEarned.mined"));
            CollectionAssert.AreEqual(new[] { new ItemId(7), new ItemId(9) }, new[] { EarnedAt(0).ItemId, EarnedAt(1).ItemId });
            Assert.AreEqual(8, EarnedAt(0).Count);
        }

        /// <web-test file="src/features/notification/notificationStore.test.ts">別アイテムの獲得は別行になる</web-test>
        [Test]
        public void 別アイテムの獲得は別行になる()
        {
            _model.AddNotification(Earned(7, 5, "itemEarned.mined"));
            _model.AddNotification(Earned(9, 3, "itemEarned.mined"));
            Assert.AreEqual(2, Rows.Count);
        }

        /// <web-test file="src/features/notification/notificationStore.test.ts">messageIdが異なる同一アイテムの獲得は合流しない</web-test>
        [Test]
        public void MessageIdが異なる同一アイテムの獲得は合流しない()
        {
            _model.AddNotification(Earned(7, 5, "itemEarned.mined"));
            _model.AddNotification(Earned(7, 3, "itemEarned.crafted"));
            Assert.AreEqual(2, Rows.Count);
        }

        /// <web-test file="src/features/notification/notificationStore.test.ts">獲得以外のカテゴリは同じ内容でも集約されない</web-test>
        [Test]
        public void 獲得以外のカテゴリは同じ内容でも集約されない()
        {
            _model.AddNotification(Achievement("achievement.unlockedItem", NoParams, ItemIcon(7)));
            _model.AddNotification(Achievement("achievement.unlockedItem", NoParams, ItemIcon(7)));
            Assert.AreEqual(2, Rows.Count);
        }

        /// <web-test file="src/features/notification/notificationAnimationDesign.test.ts">同時表示数の上限を持たない</web-test>
        [Test]
        public void 同時表示数の上限を持たない()
        {
            for (var i = 0; i < 50; i++) _model.AddNotification(Earned(i + 1, 1, "itemEarned.mined"));
            Assert.AreEqual(50, Rows.Count);
        }

        [Test]
        public void 除去すると指定の行だけが消え同じidの再除去は何もしない()
        {
            _model.AddNotification(Earned(7, 1, "itemEarned.mined"));
            _model.AddNotification(Earned(9, 1, "itemEarned.mined"));
            var firstId = Rows[0].Id;
            _model.RemoveNotification(firstId);
            _model.RemoveNotification(firstId);
            Assert.AreEqual(1, Rows.Count);
            Assert.AreEqual(new ItemId(9), EarnedAt(0).ItemId);
        }

        private EarnedNotificationContent EarnedAt(int index)
        {
            if (Rows[index].Content is EarnedNotificationContent earned) return earned;
            Assert.Fail($"row {index} is {Rows[index].Content.GetType().Name}, not an earn");
            return null;
        }
    }
}
