using System.Collections;
using System.Threading;
using Client.Game.InGame.BugReport.Playtest;
using Client.Localization;
using Client.PlaytestReceiver.Launch;
using Client.Starter.EventMode;
using Client.Starter.Playtest.TitleGates;
using Client.Tests.BugReport;
using Client.Tests.PlaytestReceiver;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Client.Tests.EventMode
{
    public class EventModeAutoStartSequenceTest
    {
        private const float ShortDeadlineSeconds = 0.2f;
        private const float ObservationLimitSeconds = 5f;

        [SetUp]
        public void SetUp()
        {
            Localize.Initialize();
            PlaytestTitleGates.ResetOnPlayMode();
            PlaytestStartGateBypass.ResetOnPlayMode();
            PlaytestLaunchProfile.Apply(PlaytestLaunchKind.DeveloperMode,
                new EmptyPlaytestSessionIdentity(EmptyPlaytestSessionIdentity.DeveloperModeReason));
        }

        [TearDown]
        public void TearDown()
        {
            PlaytestTitleGates.ResetOnPlayMode();
            PlaytestStartGateBypass.ResetOnPlayMode();
            PlaytestLaunchProfile.ResetOnPlayMode();
        }

        [UnityTest, Timeout(5000)]
        public IEnumerator WaitsBeforeTitleStartsAndStartsOnceAfterPass() => UniTask.ToCoroutine(async () =>
        {
            using var exit = new CancellationTokenSource();
            var actions = new RecordingEventModeAutoStartActions();
            var run = EventModeAutoStartSequence.RunAsync(actions, ObservationLimitSeconds, exit.Token);
            Assert.AreEqual(UniTaskStatus.Pending, run.Status);
            Assert.IsEmpty(actions.Calls);
            PassUnattendedTitle(exit.Token);
            await run;
            await UniTask.Yield();
            CollectionAssert.AreEqual(new[] { "start" }, actions.Calls);
            exit.Cancel();
        });

        [UnityTest, Timeout(5000)]
        public IEnumerator AlreadyPassedTitleStartsOnce() => UniTask.ToCoroutine(async () =>
        {
            using var exit = new CancellationTokenSource();
            PassUnattendedTitle(exit.Token);
            var actions = new RecordingEventModeAutoStartActions();
            await EventModeAutoStartSequence.RunAsync(actions, ObservationLimitSeconds, exit.Token);
            CollectionAssert.AreEqual(new[] { "start" }, actions.Calls);
            exit.Cancel();
        });

        [UnityTest, Timeout(5000)]
        public IEnumerator DeadlineQuitsWithoutStartingAndNeverStartsLate() => UniTask.ToCoroutine(async () =>
        {
            using var exit = new CancellationTokenSource();
            var actions = new RecordingEventModeAutoStartActions();
            await EventModeAutoStartSequence.RunAsync(actions, ShortDeadlineSeconds, exit.Token);
            CollectionAssert.AreEqual(new[] { "quit" }, actions.Calls);
            Assert.AreEqual(ShortDeadlineSeconds, actions.QuitDeadlineSeconds);
            PassUnattendedTitle(exit.Token);
            await UniTask.Yield();
            CollectionAssert.AreEqual(new[] { "quit" }, actions.Calls);
            exit.Cancel();
        });

        [UnityTest, Timeout(5000)]
        public IEnumerator ExitCancellationDoesNotStartOrQuitAgain() => UniTask.ToCoroutine(async () =>
        {
            using var exit = new CancellationTokenSource();
            var actions = new RecordingEventModeAutoStartActions();
            var run = EventModeAutoStartSequence.RunAsync(actions, ObservationLimitSeconds, exit.Token);
            exit.Cancel();
            Assert.IsTrue(await run.SuppressCancellationThrow());
            Assert.IsEmpty(actions.Calls);
        });

        [UnityTest, Timeout(5000)]
        public IEnumerator CancelledExitDoesNotStartAnAlreadyPassedTitle() => UniTask.ToCoroutine(async () =>
        {
            using var exit = new CancellationTokenSource();
            PassUnattendedTitle(exit.Token);
            exit.Cancel();
            var actions = new RecordingEventModeAutoStartActions();
            var run = EventModeAutoStartSequence.RunAsync(actions, ObservationLimitSeconds, exit.Token);
            Assert.IsTrue(await run.SuppressCancellationThrow());
            Assert.IsEmpty(actions.Calls);
        });

        private static void PassUnattendedTitle(CancellationToken exitToken)
        {
            // 本物の無人ゲートを合成し、同意ファイルには触れない
            // Compose the real unattended gates without modifying the consent file
            PlaytestTitleGates.BeginComposed(TestPreviousSessionArtifacts.Unclean(), true,
                new RecordingUploadRequester(), "eventModeAutoStart", exitToken);
        }
    }
}
