using System.IO;
using Client.PlaytestReceiver.Delivery;
using Client.PlaytestReceiver.Upload;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver.Upload
{
    public class PlaytestInventoryReadFailureTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void 読取不能は古い完了を件数不明で上書きし復旧後に再走査する(bool localOnly)
        {
            var root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(root);
            var directories = PlaytestOutboxTestBoxes.Directories(root);
            Directory.CreateDirectory(Path.GetDirectoryName(directories.ReportOutbox));
            Directory.Delete(directories.ReportOutbox);
            File.WriteAllText(directories.ReportOutbox, "blocks directory enumeration");
            PlaytestUploadRunner.ResetOnPlayMode();
            PlaytestDeliveryStatusBoard.Set(PlaytestDeliveryAllSent.Instance);
            var api = new FakeUploadApi();
            var runner = new PlaytestUploadRunner(api, directories, new FakeTicketProvider("aabb"));
            PlaytestDeliveryRoute route = localOnly
                ? new PlaytestLocalOnlyRoute(PlaytestLocalOnlyReason.DeveloperTokenMissing, PlaytestOutboxScope.ReportsOnly, "missing")
                : new PlaytestDeveloperTokenRoute("token");
            runner.RequestUploadWith(route);
            var status = PlaytestDeliveryStatusBoard.Current.Value;
            var inventory = status is PlaytestDeliveryLocalOnly local ? local.Inventory : ((PlaytestDeliveryPending)status).Inventory;
            Assert.IsInstanceOf<PlaytestOutboxInventoryUnavailable>(inventory);
            Assert.IsNotEmpty(((PlaytestOutboxInventoryUnavailable)inventory).Reason);
            Assert.AreEqual(0, api.DeveloperSessionCallCount);
            File.Delete(directories.ReportOutbox);
            runner.RequestUploadWith(route);
            if (localOnly) Assert.IsInstanceOf<PlaytestOutboxDeliveryCounts>(((PlaytestDeliveryLocalOnly)PlaytestDeliveryStatusBoard.Current.Value).Inventory);
            else Assert.IsInstanceOf<PlaytestDeliveryAllSent>(PlaytestDeliveryStatusBoard.Current.Value);
            Directory.Delete(root, true);
            PlaytestUploadRunner.ResetOnPlayMode();
        }
    }
}
