using System.Collections.Generic;
using Client.Tests.Ui.Screens;
using Client.Ui.State.Notification.Content;
using Client.Ui.State.Notification.Text;
using Core.Master;
using NUnit.Framework;
using Server.Event.Notification;
using UnityEngine;
using UnityEngine.TestTools;
using static Client.Tests.Ui.State.Notification.NotificationTestContents;

namespace Client.Tests.Ui.State.Notification
{
    /// <summary>
    /// 補間値が欠けた通知は {name} の枠のまま見え、開発者向けのログが出ることを英語の辞書で確かめる（Web の it.each）
    /// Verifies with the English dictionary that a notification missing a value keeps its {name} token visible and logs for developers (Web it.each)
    /// </summary>
    public class NotificationTextLocalizeTest : ScreenTestHost
    {
        /// <web-test file="src/features/notification/notificationMessages.test.ts">missing values remain visible for %s</web-test>
        [TestCase("saveMigration.missingMasterPruned", NotificationCategory.SaveMigration, "ui.notification.saveMigrationMissingMasterPruned", "researchCount")]
        [TestCase("achievement.researchCompleted", NotificationCategory.Achievement, "ui.notification.researchCompleted", "research")]
        [TestCase("achievement.challengeCompleted", NotificationCategory.Achievement, "ui.notification.challengeCompleted", "challenge")]
        public void 欠けた補間値は枠のまま見える(string messageId, NotificationCategory category, string key, string name)
        {
            // 除去告知はほかの 2 つの件数を渡し、欠けるのを 1 つに絞る（ログの期待を 1 行にするため）
            // Give the prune notice its other two counts so exactly one value is missing (one expected log line)
            var isSaveMigration = category == NotificationCategory.SaveMigration;
            var wire = isSaveMigration
                ? new Dictionary<string, string> { ["blockCount"] = "1", ["itemStackCount"] = "2" }
                : new Dictionary<string, string>();
            if (!isSaveMigration) LogAssert.Expect(LogType.Warning, $"[NotificationParams] {messageId}: missing param {name}");
            LogAssert.Expect(LogType.Error, $"[Localize] {key}: no value for placeholder {{{name}}}");

            NotificationContent content = isSaveMigration ? SaveMigration(messageId, Wire(messageId, wire)) : Achievement(messageId, Wire(messageId, wire), NoIcon);
            var text = NotificationText.Resolve(content, new FakeNotificationTextLookup()).Resolve();
            StringAssert.Contains($"{{{name}}}", text);
        }

        [Test]
        public void 獲得の引数に同名のitemNameとcountが来ても実値で出てエラーにならない()
        {
            // C# の補間は同名の先勝ちで重複を LogError にする。Web は実値が後勝ち（notificationMessages.ts の { ...values, itemName, count }）
            // C# interpolation keeps the first of a duplicate and logs it; Web lets the real values win
            const string id = "itemEarned.mined";
            var lookup = new FakeNotificationTextLookup();
            lookup.SetItemName(new ItemId(2), "Stone");
            var content = new EarnedNotificationContent(id, Wire(id, new Dictionary<string, string> { ["itemName"] = "wrong", ["count"] = "999" }), new ItemId(2), 5);

            var text = NotificationText.Resolve(content, lookup).Resolve();
            StringAssert.Contains("Stone", text);
            StringAssert.Contains("5", text);
            StringAssert.DoesNotContain("wrong", text);
            StringAssert.DoesNotContain("999", text);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
