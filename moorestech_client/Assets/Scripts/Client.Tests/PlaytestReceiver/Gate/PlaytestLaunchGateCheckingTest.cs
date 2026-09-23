using System;
using System.IO;
using System.Threading;
using Client.Localization;
using Client.PlaytestReceiver.Gate;
using Client.PlaytestReceiver.Http;
using Cysharp.Threading.Tasks;
using Game.Paths;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver
{
    // 配布ビルドの照合中だけに現れる状態の検証。印を実際に置かないと照合が始まらない
    // Covers the state that exists only while a distribution build is being checked; the check needs the real marker
    public class PlaytestLaunchGateCheckingTest
    {
        private byte[] _originalBuildInfo;
        private byte[] _originalBuildInfoMeta;

        // 実物の build-info.json を上書きするので、元の中身を退避してから印を置く
        // The real build-info.json is overwritten, so its original content is kept aside before placing the marker
        [SetUp]
        public void PlaceBuildInfoMarker()
        {
            // 照合中の拒否でゲートが理由の文言を解決するため、EditModeでも辞書を読み込んでおく
            // Refusing during the check resolves the reason text in the gate, so the dictionary is loaded in EditMode too
            Localize.Initialize();

            var path = GameSystemPaths.BuildInfoFilePath;
            _originalBuildInfo = File.Exists(path) ? File.ReadAllBytes(path) : null;
            _originalBuildInfoMeta = File.Exists(path + ".meta") ? File.ReadAllBytes(path + ".meta") : null;

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "{}");
        }

        // 元から在ったものは戻し、無かったものだけを消す
        // Whatever existed is restored, and only what did not exist is deleted
        [TearDown]
        public void RestoreBuildInfo()
        {
            var path = GameSystemPaths.BuildInfoFilePath;
            Restore(path, _originalBuildInfo);
            Restore(path + ".meta", _originalBuildInfoMeta);
            // 識別の解除は SetCurrent が担う。ここで直に戻すと、解除の窓口が1箇所という前提をテスト側から崩す（D-C4）
            // Clearing the identity is SetCurrent's job; doing it directly here would break the single-window premise from the test side (D-C4)
            PlaytestLaunchGate.SetCurrent(PlaytestGateResult.NotEvaluated);

            #region Internal

            void Restore(string target, byte[] original)
            {
                if (original != null) File.WriteAllBytes(target, original);
                else if (File.Exists(target)) File.Delete(target);
            }

            #endregion
        }

        [Test]
        public void 配布ビルドでは照合が終わるまで開始を拒否し許可が出てから通す()
        {
            var ticketGate = new UniTaskCompletionSource<string>();
            var ticketProvider = new GatedTicketProvider(ticketGate);
            var api = new FakeApi();
            api.SessionResponses.Add(PlaytestApiResult.Responded(200, PlaytestSessionBodies.AllowedFarFuture));

            var evaluating = PlaytestLaunchGate.EvaluateAsync(ticketProvider, api, new DateTime(2026, 9, 13, 0, 0, 0, DateTimeKind.Utc), CancellationToken.None);

            // チケット待ちで止まっている間。ここが素通しだと待ち文言を閉じるだけで開始できてしまう
            // While the ticket is still pending; passing here would let a tester start by closing the waiting message
            Assert.AreEqual(PlaytestGateStatus.Checking, PlaytestLaunchGate.Current.Value.Status);
            Assert.IsFalse(PlaytestLaunchGate.TryPassStart("during-check", out _));

            ticketGate.TrySetResult("aabb");
            evaluating.GetAwaiter().GetResult();

            Assert.AreEqual(PlaytestGateStatus.Allowed, PlaytestLaunchGate.Current.Value.Status);
            Assert.IsTrue(PlaytestLaunchGate.TryPassStart("after-check", out _));
        }

        // 確定待ちは購読で照合中を越え、確定した結論をそのまま返す。出展モードとsmokeはこの1本だけで待つ
        // The settled-verdict wait rides a subscription past Checking and returns the settled verdict; event mode and smoke both wait through it alone
        [Test]
        public void 確定待ちは照合中の間は返らず確定した結論を返す()
        {
            PlaytestLaunchGate.SetCurrent(PlaytestGateResult.Checking);
            var waiting = PlaytestLaunchGate.WaitForSettledVerdictAsync(180f, CancellationToken.None);
            Assert.AreEqual(UniTaskStatus.Pending, waiting.Status);

            PlaytestLaunchGate.SetCurrent(PlaytestGateResult.Blocked(PlaytestGateStatus.NotAllowed, ""));
            Assert.AreEqual(PlaytestGateStatus.NotAllowed, waiting.GetAwaiter().GetResult().Status);
        }
    }
}
