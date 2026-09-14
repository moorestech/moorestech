using System;
using System.IO;
using System.Threading;
using Client.PlaytestReceiver;
using Client.PlaytestReceiver.Gate;
using Client.PlaytestReceiver.Http;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver
{
    // 配布ビルドの照合中だけに現れる状態の検証。印を実際に置かないと RequiresCheck が true にならない
    // Covers the state that exists only while a distribution build is being checked; RequiresCheck needs the real marker
    public class PlaytestLaunchGateCheckingTest
    {
        [SetUp]
        public void PlaceBuildInfoMarker()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PlaytestBuildInfoFile.Path));
            File.WriteAllText(PlaytestBuildInfoFile.Path, "{}");
        }

        [TearDown]
        public void RemoveBuildInfoMarker()
        {
            // 残すと PlaytestBuildInfoFileTest が落ち、Editor 起動が配布版として照合され始める
            // Leaving it behind fails PlaytestBuildInfoFileTest and makes the Editor start checking as a distribution build
            File.Delete(PlaytestBuildInfoFile.Path);
            File.Delete(PlaytestBuildInfoFile.Path + ".meta");
            PlaytestLaunchGate.SetCurrent(PlaytestGateDecision.DeveloperMode);
        }

        [Test]
        public void 配布ビルドでは照合が終わるまで開始を拒否し許可が出てから通す()
        {
            var ticketGate = new UniTaskCompletionSource<string>();
            var ticketProvider = new GatedTicketProvider(ticketGate);
            var api = new FakeApi();
            api.SessionResponses.Add(new PlaytestApiResult { StatusCode = 200, Body = "{\"steamId\":\"7656\",\"allowed\":true,\"token\":\"tok-1\"}" });
            var session = new PlaytestSession(api, ticketProvider);

            var evaluating = PlaytestLaunchGate.EvaluateAsync(session, ticketProvider, new DateTime(2026, 9, 13, 0, 0, 0, DateTimeKind.Utc), CancellationToken.None);

            // チケット待ちで止まっている間。ここが素通しだと待ち文言を閉じるだけで開始できてしまう
            // While the ticket is still pending; passing here would let a tester start by closing the waiting message
            Assert.AreEqual(PlaytestGateStatus.Checking, PlaytestLaunchGate.Current.Status);
            Assert.IsTrue(PlaytestLaunchGate.RejectStart("during-check"));

            ticketGate.TrySetResult("aabb");
            var result = evaluating.GetAwaiter().GetResult();

            Assert.AreEqual(PlaytestGateStatus.Allowed, result.Status);
            Assert.IsFalse(PlaytestLaunchGate.RejectStart("after-check"));
        }
    }
}
