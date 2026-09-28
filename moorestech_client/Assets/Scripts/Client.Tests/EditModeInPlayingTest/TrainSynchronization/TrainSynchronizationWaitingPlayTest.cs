using System.Collections;
using Client.Tests.TrainSynchronization;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest
{
    public sealed class TrainSynchronizationWaitingPlayTest
    {
        [UnityTest]
        public IEnumerator SnapshotThenSilence_KeepsFramesRunning_AndSavesOnlyAfterObservedLag()
        {
            EnterPlayModeUtil();
            yield return new EnterPlayMode(expectDomainReload: true);
            LogAssert.ignoreFailingMessages = true;

            using (var context = new TrainSynchronizationTestContext())
            {
                // 大きいsnapshot tickで通常ループ予算がゼロになる経路を通す。
                // Use a large snapshot tick to exercise the zero normal-loop-budget path.
                const uint snapshotTick = 500000;
                context.ApplyInitialSnapshot(snapshotTick);
                var initialId = context.State.GetAppliedTickUnifiedId();
                var initialFrame = Time.frameCount;
                context.Simulator.Tick();
                Assert.That(context.Diagnostics.IsWaiting, Is.True);
                for (var i = 0; i < 30; i++)
                {
                    context.Simulator.Tick();
                    yield return null;
                }
                Assert.That(Time.frameCount, Is.GreaterThan(initialFrame));
                Assert.That(context.State.GetAppliedTickUnifiedId(), Is.EqualTo(initialId));
                Assert.That(context.Reports(), Is.Empty);

                // 実際の後続受信が閾値へ到達した時点だけで、最初の欠落位置を保存する。
                // Save the original missing position only when an actual later arrival reaches the threshold.
                context.Buffer.EnqueueHash(uint.MaxValue, uint.MaxValue, snapshotTick + 199, 1);
                context.Simulator.Tick();
                Assert.That(context.Reports(), Is.Empty);
                context.Buffer.EnqueueHash(uint.MaxValue, uint.MaxValue, snapshotTick + 200, 1);
                context.Simulator.Tick();
                var report = context.ReadReport();
                Assert.That((ulong)report["ExpectedId"], Is.EqualTo(initialId + 1));
                Assert.That((uint)report["TickGapAtOnset"], Is.Zero);
                Assert.That((uint)report["TickGapAtCapture"], Is.EqualTo(200));
                context.Buffer.EnqueueHash(uint.MaxValue, uint.MaxValue, snapshotTick, 1);
                context.Simulator.Tick();
                Assert.That(context.State.GetTick(), Is.EqualTo(snapshotTick + 1));
            }

            yield return new ExitPlayMode();
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);
        }

        [UnityTearDown]
        public IEnumerator RestoreEditorAfterFailure()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            LogAssert.ignoreFailingMessages = false;
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);
        }
    }
}
