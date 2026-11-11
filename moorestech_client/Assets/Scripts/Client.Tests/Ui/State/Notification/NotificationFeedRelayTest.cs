using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.UI.Notification;
using Client.Tests.Inventory;
using Client.Ui.State.Notification;
using Client.Ui.State.Notification.Content;
using Client.Ui.State.Notification.Params;
using Core.Master;
using NUnit.Framework;
using Server.Event.Notification;

namespace Client.Tests.Ui.State.Notification
{
    /// <summary>
    /// Feed の通知が種別の型へ変換されて Model へ積まれ、除去告知が二重に出ないことを確かめる（L0。Web の claimSaveMigrationNotice の 2 件）
    /// Verifies feed notifications become per-kind contents in the model and the prune notice never shows twice (L0; Web claimSaveMigrationNotice x2)
    /// </summary>
    public class NotificationFeedRelayTest
    {
        private ClientLocalNotificationSource _localSource;
        private NotificationFeed _feed;
        private NotificationModel _model;
        private NotificationFeedRelay _relay;

        private IReadOnlyList<NotificationRow> Rows => _model.Current.Value.Rows;

        [SetUp]
        public void CreateFeed()
        {
            _localSource = new ClientLocalNotificationSource();
            _feed = new NotificationFeed(new CapturingVanillaApiEvent(), _localSource);
            _feed.Initialize();
            _model = new NotificationModel();
            _relay = new NotificationFeedRelay(_feed, _model);
        }

        [TearDown]
        public void DisposeRelay()
        {
            _relay.Dispose();
        }

        /// <web-test file="src/features/notification/notificationStore.test.ts">snapshotで再着した同じseqの除去告知は1回だけ通す</web-test>
        [Test]
        public void 購読前に来た除去告知は初期データから1回だけ出る()
        {
            _localSource.Notify(NotificationMessagePack.CreateSaveMigrationPruned(1, 2, 3));
            _relay.Initialize();
            _localSource.Notify(NotificationMessagePack.CreateOperationDenied("denied.craftResultFull", new Dictionary<string, string>()));

            Assert.AreEqual(2, Rows.Count);
            Assert.IsInstanceOf<SaveMigrationNotificationContent>(Rows[0].Content);
            Assert.AreEqual("saveMigration.missingMasterPruned", Rows[0].Content.MessageId);
            Assert.IsInstanceOf<OperationDeniedNotificationContent>(Rows[1].Content);
        }

        /// <web-test file="src/features/notification/notificationStore.test.ts">サーバー再接続で届いた新しいseqの告知は再び通す</web-test>
        [Test]
        public void 購読後に届いた新しい除去告知は再び出る()
        {
            _localSource.Notify(NotificationMessagePack.CreateSaveMigrationPruned(1, 2, 3));
            _relay.Initialize();
            _localSource.Notify(NotificationMessagePack.CreateSaveMigrationPruned(4, 5, 6));

            Assert.AreEqual(2, Rows.Count);
            var researchCount = Rows[1].Content.MessageParams.OfType<TextNotificationParam>().Single(param => param.Name == "researchCount");
            Assert.AreEqual("6", researchCount.Value);
        }

        [Test]
        public void 獲得は数とアイテムを持つ獲得の型へ変換される()
        {
            _relay.Initialize();
            _localSource.Notify(NotificationMessagePack.CreateItemEarned(new ItemId(2), 5));

            Assert.That(Rows[0].Content, Is.TypeOf<EarnedNotificationContent>()
                .With.Property(nameof(EarnedNotificationContent.ItemId)).EqualTo(new ItemId(2))
                .And.Property(nameof(EarnedNotificationContent.Count)).EqualTo(5)
                .And.Property(nameof(NotificationContent.MessageId)).EqualTo("itemEarned.mined"));
        }

        [Test]
        public void 空のアイテムの実績はアイコン無しの型でアイテム付きの実績はアイテムのアイコンを持つ()
        {
            _relay.Initialize();
            _localSource.Notify(NotificationMessagePack.CreateAchievement("achievement.unlockedBlock", new Dictionary<string, string>()));
            _localSource.Notify(NotificationMessagePack.CreateAchievementWithItem("achievement.unlockedItem", new Dictionary<string, string>(), new ItemId(2)));

            Assert.IsInstanceOf<AchievementNotificationContent>(Rows[0].Content);
            Assert.IsInstanceOf<NoNotificationIcon>(Rows[0].Content.Icon);
            Assert.That(Rows[1].Content.Icon, Is.TypeOf<ItemNotificationIcon>().With.Property(nameof(ItemNotificationIcon.ItemId)).EqualTo(new ItemId(2)));
        }
    }
}
