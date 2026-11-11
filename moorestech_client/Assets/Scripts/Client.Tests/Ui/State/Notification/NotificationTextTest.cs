using System.Collections.Generic;
using System.Linq;
using Client.Localization;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.State.Notification.Params;
using Client.Ui.State.Notification.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static Client.Tests.Ui.State.Notification.NotificationTestContents;

namespace Client.Tests.Ui.State.Notification
{
    /// <summary>
    /// 中身から UiText（キーと補間値）を作る規則を確かめる（L0。Web の notificationMessages.test.ts）
    /// Verifies how a content becomes a UiText key and interpolation values (L0; Web notificationMessages.test.ts)
    /// </summary>
    public class NotificationTextTest
    {
        private const string ContentGuid = "13C3D42F-BBBC-5EB4-8CD0-7B841EF53079";
        private FakeNotificationTextLookup _lookup;

        [SetUp]
        public void CreateLookup()
        {
            _lookup = new FakeNotificationTextLookup();
        }

        /// <web-test file="src/features/notification/notificationMessages.test.ts">撤去Undoの取りこぼし件数を通知文言へ渡す</web-test>
        [Test]
        public void 撤去Undoの取りこぼし件数を通知文言へ渡す()
        {
            const string id = "denied.undoRestoreSkipped";
            var text = NotificationText.Resolve(Denied(id, Wire(id, new Dictionary<string, string> { ["count"] = "2" }), NoIcon), _lookup);
            UiTextAssert.AreEqual(LocalizationKeys.Ui.Notification.UndoRestoreSkipped, new[] { "count", "2", "messageId", id }, text);
        }

        /// <web-test file="src/features/notification/notificationMessages.test.ts">messageIdとparamsを補間値へ変換する</web-test>
        [Test]
        public void MessageIdとparamsを補間値へ変換する()
        {
            var values = NotificationText.BuildInterpolationValues("known.id", new[] { new LocalizationTextParam("x", "a"), new LocalizationTextParam("y", "b") });
            CollectionAssert.AreEqual(new[] { "x=a", "y=b", "messageId=known.id" }, values.ConvertAll(value => $"{value.Name}={value.Value}"));
        }

        /// <web-test file="src/features/notification/notificationMessages.test.ts">アイテム名とcountを補間するのは獲得通知だけ</web-test>
        [Test]
        public void アイテム名とcountを補間するのは獲得通知だけ()
        {
            const string deniedId = "denied.craftResultFull";
            UiTextAssert.AreEqual(LocalizationKeys.Ui.Notification.ItemEarned, new[] { "messageId", "itemEarned.mined", "itemName", "item:7", "count", "8" },
                NotificationText.Resolve(Earned(7, 8, "itemEarned.mined"), _lookup));
            UiTextAssert.AreEqual(LocalizationKeys.Ui.Notification.CraftResultFull, new[] { "x", "a", "messageId", deniedId },
                NotificationText.Resolve(Denied(deniedId, Wire(deniedId, new Dictionary<string, string> { ["x"] = "a" }), NoIcon), _lookup));
        }

        /// <web-test file="src/features/notification/notificationMessages.test.ts">獲得通知はmessageIdに依らず獲得テンプレートを使う</web-test>
        [Test]
        public void 獲得通知はmessageIdに依らず獲得テンプレートを使う()
        {
            UiTextAssert.AreEqual(LocalizationKeys.Ui.Notification.ItemEarned, new[] { "messageId", "itemEarned.drifted", "itemName", "item:7", "count", "3" },
                NotificationText.Resolve(Earned(7, 3, "itemEarned.drifted"), _lookup));
        }

        /// <web-test file="src/features/notification/notificationMessages.test.ts">獲得messageIdがmessage系categoryで来たら未知通知へ落とす</web-test>
        [Test]
        public void 獲得messageIdがmessage系categoryで来たら未知通知へ落とす()
        {
            var text = NotificationText.Resolve(Achievement("itemEarned.mined", NoParams, NoIcon), _lookup);
            UiTextAssert.AreEqual(LocalizationKeys.Ui.Notification.UnknownMessage, new[] { "messageId", "itemEarned.mined" }, text);
        }

        /// <web-test file="src/features/notification/notificationMessages.test.ts">獲得通知以外はアイテム名を解決しない</web-test>
        [Test]
        public void 獲得通知以外はアイテム名を解決しない()
        {
            NotificationText.Resolve(Achievement("achievement.unlockedItem", NoParams, ItemIcon(7)), _lookup);
            Assert.AreEqual(0, _lookup.ItemNameCalls);
        }

        /// <web-test file="src/features/notification/notificationMessages.test.ts">マスタ欠損の除去通知は3つの件数を補間する</web-test>
        [Test]
        public void マスタ欠損の除去通知は3つの件数を補間する()
        {
            const string id = "saveMigration.missingMasterPruned";
            var wire = new Dictionary<string, string> { ["blockCount"] = "3", ["itemStackCount"] = "4", ["researchCount"] = "5" };
            UiTextAssert.AreEqual(LocalizationKeys.Ui.Notification.SaveMigrationMissingMasterPruned,
                new[] { "blockCount", "3", "itemStackCount", "4", "researchCount", "5", "messageId", id },
                NotificationText.Resolve(SaveMigration(id, Wire(id, wire)), _lookup));
        }

        /// <web-test file="src/features/notification/notificationMessages.test.ts">Guidパラメータ通知はcontentキーで表示名へ解決する</web-test>
        [Test]
        public void Guidパラメータ通知はcontentキーで表示名へ解決する()
        {
            var lower = ContentGuid.ToLowerInvariant();
            Assert.AreEqual($"resolved:challenge.{lower}.title", ResolvedValue("achievement.challengeCompleted", "challenge", ContentGuid));
            Assert.AreEqual($"resolved:research.{lower}.name", ResolvedValue("achievement.researchCompleted", "research", ContentGuid));
        }

        /// <web-test file="src/features/notification/notificationMessages.test.ts">Guidパラメータを持たない通知はparamsを素通しする</web-test>
        [Test]
        public void Guidパラメータを持たない通知はparamsを素通しする()
        {
            Assert.AreEqual("raw", ResolvedValue("denied.craftResultFull", "x", "raw"));
            Assert.AreEqual(0, _lookup.ContentTextCalls);
        }

        /// <web-test file="src/features/notification/notificationMessages.test.ts">Guidの名前が欠けた通知は警告してparamsを素通しする</web-test>
        [Test]
        public void Guidの名前が欠けた通知は警告してparamsを素通しする()
        {
            LogAssert.Expect(LogType.Warning, "[NotificationParams] achievement.researchCompleted: missing param research");
            Assert.AreEqual("g", ResolvedValue("achievement.researchCompleted", "wrong", "g"));
            Assert.AreEqual(0, _lookup.ContentTextCalls);
        }

        [Test]
        public void Guidとして読めない値は警告して文字の引数のまま出す()
        {
            const string id = "achievement.researchCompleted";
            LogAssert.Expect(LogType.Warning, $"[NotificationParams] {id}: param research is not a guid: not-a-guid");
            var messageParams = Wire(id, new Dictionary<string, string> { ["research"] = "not-a-guid" });
            Assert.IsInstanceOf<TextNotificationParam>(messageParams.Single());
            Assert.AreEqual("not-a-guid", NotificationText.ResolveParams(messageParams, _lookup).Single().Value);
        }

        // 受信の 1 引数を型付きにしてから解決し、その値を返す
        // Type one wire argument, resolve it and return its value
        private string ResolvedValue(string messageId, string name, string raw)
        {
            var messageParams = Wire(messageId, new Dictionary<string, string> { [name] = raw });
            return NotificationText.ResolveParams(messageParams, _lookup).Single(param => param.Name == name).Value;
        }
    }
}
