using System.Collections;
using Client.Game.InGame.Train.Network;
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
        public IEnumerator SnapshotThenSilence_KeepsFramesRunning_AndSavesConfirmedSequenceGap()
        {
            EnterPlayModeUtil();
            yield return new EnterPlayMode(expectDomainReload: true);
            LogAssert.ignoreFailingMessages = true;

            // 同じsnapshotでhashが既着なら進まないことにより、初回の通常予算ゼロを確かめる。
            // Verify zero initial normal budget by showing an available hash does not advance the same snapshot.
            const uint snapshotTick = 1000;
            using (var buffered = new TrainSynchronizationTestContext())
            {
                buffered.ApplyInitialSnapshot(snapshotTick);
                var initialId = buffered.State.GetAppliedTickUnifiedId();
                buffered.Buffer.EnqueueHash(uint.MaxValue, uint.MaxValue, snapshotTick, 1);
                buffered.Simulator.Tick();
                Assert.That(buffered.State.GetAppliedTickUnifiedId(), Is.EqualTo(initialId));
                Assert.That(buffered.Diagnostics.IsWaiting, Is.False);
                Assert.That(buffered.Reports(), Is.Empty);
            }

            using (var context = new TrainSynchronizationTestContext())
            {
                // 大きいsnapshot tickで通常ループ予算がゼロになる経路を通す。
                // Use a large snapshot tick to exercise the zero normal-loop-budget path.
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

                // 同tickの後続連番を受信して欠番が確定した時点で保存する。
                // Save as soon as a later sequence in the same tick confirms the missing ordered message.
                context.Buffer.EnqueueHash(uint.MaxValue, uint.MaxValue, snapshotTick, 2);
                context.Simulator.Tick();
                var report = context.ReadReport();
                Assert.That((ulong)report["ExpectedId"], Is.EqualTo(initialId + 1));
                Assert.That((uint)report["TickGapAtOnset"], Is.Zero);
                Assert.That((uint)report["TickGapAtCapture"], Is.Zero);
                Assert.That((string)report["CaptureReason"], Is.EqualTo("ConfirmedOrderedGap"));
                var applied = false;
                context.Buffer.EnqueueEvent("test:late-event", snapshotTick, 1, TrainTickBufferedEvent.Create(() => applied = true));
                context.Simulator.Tick();
                Assert.That(applied, Is.True);
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
