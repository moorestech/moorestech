using System.Linq;
using Client.Ui.State.Notification.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Notification.Coverage
{
    /// <summary>
    /// サーバー・クライアントが送る通知 id を走査で全部拾い、C# の表が未知以外へ解決することを確かめる（S。Web の notificationServerIdCoverage.test.ts）
    /// Scans every notification id the server and client send and checks the C# table resolves each to a known key (S; Web notificationServerIdCoverage.test.ts)
    /// </summary>
    public class NotificationServerIdCoverageTest
    {
        private static readonly string[] KnownIdsOfEachForm =
        {
            "denied.miningInventoryFull", "denied.inventoryMoveUnresolved", "achievement.unlockedItem", "saveMigration.missingMasterPruned",
            "denied.blueprint.NotUnlocked", "denied.blueprint.InvalidRequest", "denied.blueprint.NotFound", "denied.blueprint.PasteCostShortage",
            "denied.blueprint.PasteNotUnlocked", "denied.blueprint.PasteLineFailed", "denied.blueprint.PastePlacementFailed", "denied.railEdit.InvalidNode",
            "denied.electricWireDisconnect.InventoryFull", "denied.gearChainDisconnect.InventoryFull", "denied.gearChainConnect.NoItem", "denied.undoRestoreSkipped",
        };

        private System.Collections.Generic.Dictionary<string, string> _sources;
        private NotificationServerIdScanner.ScanResult _scan;

        [OneTimeSetUp]
        public void Scan()
        {
            _sources = NotificationServerIdScanner.ReadSenderSources();
            _scan = NotificationServerIdScanner.Collect(_sources);
        }

        /// <web-test file="src/features/notification/notificationServerIdCoverage.test.ts">走査が送信元の各形を拾えている</web-test>
        [Test]
        public void 走査が送信元の各形を拾えている()
        {
            // 走査が壊れて 0 件になると網羅検査が空振りで緑になるため、各形の既知 id で走査自体を確かめる
            // A broken scan would yield no ids and pass vacuously, so known ids of each form prove the scan works
            foreach (var id in KnownIdsOfEachForm) CollectionAssert.Contains(_scan.Ids, id);
        }

        /// <web-test file="src/features/notification/notificationServerIdCoverage.test.ts">通知生成APIの引数は全て分類できる形である</web-test>
        [Test]
        public void 通知生成APIの引数は全て分類できる形である()
        {
            CollectionAssert.IsEmpty(_scan.Unclassified);
        }

        /// <web-test file="src/features/notification/notificationServerIdCoverage.test.ts">除外指定したenum値は実在する</web-test>
        [Test]
        public void 除外指定したenum値は実在する()
        {
            // enum 値の改名で除外が空振りし、送られる値を黙って対象外にしないようにする
            // Guard against a renamed enum value leaving a stale exclusion that silently skips a sent value
            foreach (var entry in InterpolatedNotificationIdEnums.ByPrefix.Values)
            {
                var members = NotificationServerIdScanner.FindEnumMembers(_sources, entry.EnumName);
                Assert.IsNotNull(members, entry.EnumName);
                foreach (var member in entry.NotSentMembers) CollectionAssert.Contains(members, member, entry.EnumName);
            }
        }

        /// <web-test file="src/features/notification/notificationServerIdCoverage.test.ts">サーバーが送る通知idは全て不明通知以外のキーへ解決する</web-test>
        [Test]
        public void サーバーが送る通知idは全て不明通知以外のキーへ解決する()
        {
            var unknown = LocalizationKeys.Ui.Notification.UnknownMessage.Key;
            CollectionAssert.IsEmpty(_scan.Ids.Where(id => NotificationMessageKeys.Resolve(id).Key == unknown).ToList());
        }

        /// <web-test file="src/features/notification/notificationServerIdCoverage.test.ts">採掘で満杯により失った通知は専用文言を使う</web-test>
        [Test]
        public void 採掘で満杯により失った通知は専用文言を使う()
        {
            Assert.AreEqual(LocalizationKeys.Ui.Notification.MiningInventoryFull.Key, NotificationMessageKeys.Resolve("denied.miningInventoryFull").Key);
        }
    }
}
