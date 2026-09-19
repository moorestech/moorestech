using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using Client.Game.InGame.BugReport.Playtest;
using Client.Localization;
using Client.PlaytestReceiver.Gate;
using Client.PlaytestReceiver.Http;
using Game.Paths;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.PlaytestReceiver
{
    // 照合がAllowedになったときだけ受け口の検証済みSteamIDが識別に入ることを押さえる（ADR 0065）
    // Pins that the receiver's verified SteamID becomes the identity only when the launch check settles as Allowed (ADR 0065)
    public class PlaytestLaunchGateIdentityTest
    {
        private static readonly DateTime Now = new(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc);

        private byte[] _originalBuildInfo;
        private byte[] _originalBuildInfoMeta;

        // 実物の build-info.json を置き換えるので元の中身を退避し、識別も既定へ戻してから始める
        // The real build-info.json is replaced, so its content is kept aside and the identity is reset before each test
        [SetUp]
        public void SetUp()
        {
            Localize.Initialize();
            var path = GameSystemPaths.BuildInfoFilePath;
            _originalBuildInfo = File.Exists(path) ? File.ReadAllBytes(path) : null;
            _originalBuildInfoMeta = File.Exists(path + ".meta") ? File.ReadAllBytes(path + ".meta") : null;
            PlaytestSessionIdentityProvider.SetCurrent(new EmptyPlaytestSessionIdentity());
        }

        [TearDown]
        public void TearDown()
        {
            Restore(GameSystemPaths.BuildInfoFilePath, _originalBuildInfo);
            Restore(GameSystemPaths.BuildInfoFilePath + ".meta", _originalBuildInfoMeta);
            PlaytestLaunchGate.SetCurrent(PlaytestGateResult.NotEvaluated);
            PlaytestSessionIdentityProvider.SetCurrent(new EmptyPlaytestSessionIdentity());
        }

        [Test]
        public void 許可されると受け口の検証済みSteamIDが識別に入る()
        {
            PlaceBuildInfoMarker();
            Evaluate(PlaytestApiResult.Responded(200, PlaytestSessionBodies.AllowedFarFuture));

            Assert.AreEqual(PlaytestGateStatus.Allowed, PlaytestLaunchGate.Current.Value.Status);
            Assert.AreEqual("7656", PlaytestSessionIdentityProvider.Current.SteamId);
        }

        [Test]
        public void 許可されなければ識別は空のまま()
        {
            PlaceBuildInfoMarker();
            LogAssert.Expect(LogType.Error, new Regex(@"\[PlaytestReceiver\] launch blocked: NotAllowed"));
            Evaluate(PlaytestApiResult.Responded(403, "{\"reason\":\"not-allowed\"}"));

            Assert.IsNull(PlaytestSessionIdentityProvider.Current.SteamId);
        }

        // steamIdの無い200で通すと、報告・進行記録・異常終了箱が誰のものか分からないまま走る
        // Passing a 200 without steamId would run with reports, progress records and crash boxes that name nobody
        [Test]
        public void steamIdの欠けた200は止めて識別は空のまま()
        {
            PlaceBuildInfoMarker();
            LogAssert.Expect(LogType.Error, new Regex(@"\[PlaytestReceiver\] launch blocked: Unreachable malformed session response"));
            Evaluate(PlaytestApiResult.Responded(200, "{\"allowed\":true,\"token\":\"tok\",\"expiresAt\":\"2999-01-01T00:00:00.000Z\"}"));

            Assert.IsTrue(PlaytestLaunchGate.Current.Value.IsBlocked);
            Assert.IsNull(PlaytestSessionIdentityProvider.Current.SteamId);
        }

        [Test]
        public void 開発者モードでは照合せず識別は空のまま()
        {
            RemoveBuildInfoMarker();
            Evaluate(PlaytestApiResult.Responded(200, PlaytestSessionBodies.AllowedFarFuture));

            Assert.AreEqual(PlaytestGateStatus.DeveloperMode, PlaytestLaunchGate.Current.Value.Status);
            Assert.IsNull(PlaytestSessionIdentityProvider.Current.SteamId);
        }

        private static void Evaluate(PlaytestApiResult sessionResponse)
        {
            var api = new FakeApi();
            api.SessionResponses.Add(sessionResponse);
            PlaytestLaunchGate.EvaluateAsync(new FakeTicketProvider("aabb"), api, Now, CancellationToken.None).GetAwaiter().GetResult();
        }

        private static void PlaceBuildInfoMarker()
        {
            var path = GameSystemPaths.BuildInfoFilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "{}");
        }

        private static void RemoveBuildInfoMarker()
        {
            if (File.Exists(GameSystemPaths.BuildInfoFilePath)) File.Delete(GameSystemPaths.BuildInfoFilePath);
        }

        // 元から在ったものは戻し、無かったものだけを消す
        // Whatever existed is restored, and only what did not exist is deleted
        private static void Restore(string target, byte[] original)
        {
            if (original != null) File.WriteAllBytes(target, original);
            else if (File.Exists(target)) File.Delete(target);
        }
    }
}
