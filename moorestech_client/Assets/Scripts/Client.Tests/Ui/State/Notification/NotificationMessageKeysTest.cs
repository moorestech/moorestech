using Client.Ui.State.Notification.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Notification
{
    /// <summary>
    /// messageId から辞書キーへの表を確かめる（L0。Web の notificationMessages.test.ts の resolveNotificationKey）
    /// Verifies the messageId-to-key table (L0; Web notificationMessages.test.ts resolveNotificationKey)
    /// </summary>
    public class NotificationMessageKeysTest
    {
        /// <web-test file="src/features/notification/notificationMessages.test.ts">既知のmessageIdは型付きキーを返す</web-test>
        [Test]
        public void 既知のmessageIdは型付きキーを返す()
        {
            Assert.AreEqual(LocalizationKeys.Ui.Notification.CraftMaterialShortage.Key, NotificationMessageKeys.Resolve("denied.craftMaterialShortage").Key);
        }

        /// <web-test file="src/features/notification/notificationMessages.test.ts">在庫の取得失敗と取得待ちを別の文言へ解決する</web-test>
        [Test]
        public void 在庫の取得失敗と取得待ちを別の文言へ解決する()
        {
            Assert.AreEqual(LocalizationKeys.Ui.BlockInventory.InventoryOpenFailed.Key, NotificationMessageKeys.Resolve("denied.inventoryOpenFailed").Key);
            Assert.AreEqual(LocalizationKeys.Ui.BlockInventory.DetailLoading.Key, NotificationMessageKeys.Resolve("denied.inventoryDetailLoading").Key);
            Assert.AreEqual(LocalizationKeys.Ui.Notification.InventoryMoveSlotRejected.Key, NotificationMessageKeys.Resolve("denied.inventoryMoveSlotRejected").Key);
        }

        /// <web-test file="src/features/notification/notificationMessages.test.ts">未解決在庫の拒否は利用不可の文言へ解決する</web-test>
        [Test]
        public void 未解決在庫の拒否は利用不可の文言へ解決する()
        {
            Assert.AreEqual(LocalizationKeys.Ui.BlockInventory.InventoryOpenFailed.Key, NotificationMessageKeys.Resolve("denied.inventoryMoveUnresolved").Key);
        }

        /// <web-test file="src/features/notification/notificationMessages.test.ts">未知のmessageIdは専用キーで可視化する</web-test>
        [Test]
        public void 未知のmessageIdは専用キーで可視化する()
        {
            Assert.AreEqual(LocalizationKeys.Ui.Notification.UnknownMessage.Key, NotificationMessageKeys.Resolve("unknown.id").Key);
        }

        /// <web-test file="src/features/notification/notificationMessages.test.ts">獲得通知のmessageIdは表から到達できない</web-test>
        [Test]
        public void 獲得通知のmessageIdは表から到達できない()
        {
            Assert.AreEqual(LocalizationKeys.Ui.Notification.UnknownMessage.Key, NotificationMessageKeys.Resolve("itemEarned.mined").Key);
        }
    }
}
