using System;
using System.Collections;
using System.IO;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.BugReport.Playtest;
using Client.PlaytestReceiver;
using Client.PlaytestReceiver.Credentials;
using Client.PlaytestReceiver.Delivery;
using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Steam;
using Client.PlaytestReceiver.Upload;
using Cysharp.Threading.Tasks;
using Game.Paths;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.PlaytestReceiver
{
    public sealed class DeveloperReportRealReceiverCheck
    {
        [UnityTest, Explicit("機材のトークンで本番へ報告を送信する実機確認")]
        public IEnumerator DeveloperReportReachesProduction() => UniTask.ToCoroutine(async () =>
        {
            // 実機資格情報と本番宛先を先に検証する
            // Validate the machine credential and production destination first
            Assert.AreEqual("https://playtest.moores.tech", PlaytestReceiverConfig.BaseUrl);
            var credential = PlaytestDeveloperTokenFile.Read(GameSystemPaths.PlaytestDeveloperTokenFilePath);
            Assert.IsInstanceOf<PlaytestDeveloperTokenPresent>(credential);
            var route = PlaytestDeliveryRouteResolver.Resolve(false, credential);
            Assert.IsInstanceOf<PlaytestDeveloperTokenRoute>(route);

            // 実outboxへ書き、証拠は取り込み照合用に残す
            // Write the real outbox and retain evidence for ingestion checks
            var writer = new BugReportBundleWriter(new EmptyPlaytestSessionIdentity(EmptyPlaytestSessionIdentity.DeveloperModeReason));
            var data = new BugReportCapturedData { CaptureId = 1, ReportTick = 0 };
            var bundle = await writer.WriteAsync(data, "DeveloperReportRealReceiverCheck " + Application.platform, PlaytestReportKind.Bug);
            Assert.IsTrue(bundle.Ready);
            Assert.IsTrue(File.Exists(Path.Combine(bundle.BundleDirectory, "repo", "bundle-status.txt")));
            Debug.Log("[RealReceiverCheck] bundle=" + bundle.BundleDirectory);

            // 実HTTPと本番の走行役で送信完了印まで待つ
            // Use real HTTP and the production runner through the uploaded marker
            PlaytestUploadRunner.ResetOnPlayMode();
            var runner = new PlaytestUploadRunner(new PlaytestReceiverClient(PlaytestReceiverConfig.BaseUrl),
                PlaytestOutboxDirectories.FromGameSystemPaths(), new PlaytestSteamTicketProvider());
            runner.RequestUploadWith(route);
            var marker = Path.Combine(bundle.BundleDirectory, PlaytestOutboxScanner.UploadedMarker);
            var deadline = DateTime.UtcNow.AddMinutes(5);
            while (!File.Exists(marker) && DateTime.UtcNow < deadline)
            {
                Assert.IsFalse(PlaytestDeliveryStatusBoard.Current.Value is PlaytestDeliveryRefused, "本番が資格情報を拒否した");
                await UniTask.Yield();
            }
            Assert.IsTrue(File.Exists(marker), "5分以内にUPLOADEDが付かなかった: " + bundle.BundleDirectory);
            Debug.Log("[RealReceiverCheck] UPLOADED=" + Path.GetFileName(bundle.BundleDirectory));
        });
    }
}
