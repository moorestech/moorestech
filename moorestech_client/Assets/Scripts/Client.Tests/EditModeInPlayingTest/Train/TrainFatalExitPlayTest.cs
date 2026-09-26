using System;
using System.Collections;
using System.Reflection;
using System.Runtime.Serialization;
using Client.Game.InGame.Context;
using Client.Game.InGame.Train.Network;
using Client.Network.API;
using Client.Tests.Common;
using Cysharp.Threading.Tasks;
using MessagePack;
using Server.Event;
using Server.Event.EventReceive;
using Server.Util.MessagePack;
using System.Text.RegularExpressions;
using Client.Game.Common;
using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.Train.Network.TickSynchronization;
using Client.Starter;
using Client.Tests.TickSynchronization;
using NUnit.Framework;
using UniRx;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest
{
    [Category("CiShardClientPlay3")]
    public class TrainFatalExitPlayTest
    {
        private const int ProcessId = 999003;
        private const string Session = "fatal_playmode_test";

        [UnityTest]
        public IEnumerator InitialSnapshotFailure_DetectionQuitsBeforeStartupObservesFailure()
        {
            EnterPlayModeUtil();
            yield return new EnterPlayMode(expectDomainReload: true);
            LogAssert.ignoreFailingMessages = true;
            yield return null;
            LogAssert.ignoreFailingMessages = false;
            yield return new FailedSnapshotExit();
            Assert.IsFalse(EditorApplication.isPlaying);
            Assert.AreEqual(1, SessionState.GetInt("TrainFatalExit_Notices", 0));
            Assert.AreEqual(0, SessionState.GetInt("TrainFatalExit_Flushes", 0));
            var marks = CleanExitMarker.ConsumeSessionMarks(ProcessId, Session);
            Assert.IsFalse(marks.ExitedCleanly);
            Assert.IsFalse(marks.ShutdownStalled);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (EditorApplication.isPlaying) yield return new ExitPlayMode();
            GameShutdownEvent.ResetForNewSession();
            CleanExitMarker.ConsumeSessionMarks(ProcessId, Session);
            SessionState.EraseInt("TrainFatalExit_Notices");
            SessionState.EraseInt("TrainFatalExit_Flushes");
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);
            LogAssert.ignoreFailingMessages = false;
        }

        // 受信境界自身が終了し、未観測の初期待機に依存しないことを確認する。
        // Let the receive boundary exit without relying on startup observing the failed wait.
        private sealed class FailedSnapshotExit : IEditModeTestYieldInstruction
        {
            public bool ExpectDomainReload => false;
            public bool ExpectedPlaymodeState => false;

            public IEnumerator Perform()
            {
                GameShutdownEvent.ResetForNewSession();
                SessionState.SetInt("TrainFatalExit_Notices", 0);
                SessionState.SetInt("TrainFatalExit_Flushes", 0);
                CleanExitMarkWriter.InstallAtStartup(ProcessId, Session);
                GameShutdownEvent.RegisterParticipant(new SaveProbe());
                using var client = new TrainSnapshotClientFixture();
                var initialApply = client.Handler.WaitForInitialApplyAsync();
                var previousApi = ClientContext.VanillaApi;
                using var source = new Subject<EventMessagePack>();
                var exchange = (PacketExchangeManager)FormatterServices.GetUninitializedObject(typeof(PacketExchangeManager));
                TestReflection.SetField(exchange, "_eventPacketSubject", source);
                var dispatcher = new VanillaApiEvent(exchange);
                var api = (VanillaApi)FormatterServices.GetUninitializedObject(typeof(VanillaApi));
                typeof(VanillaApi).GetField("Event").SetValue(api, dispatcher);
                try
                {
                    TestReflection.SetStaticProperty(typeof(ClientContext), "VanillaApi", api);
                    client.Handler.Initialize();
                    using var deltas = new TrainUnitTickDiffBundleEventNetworkHandler(client.Context, client.Trains);
                    deltas.Initialize();
                    using var notification = GameShutdownEvent.OnGameShutdown.Subscribe(reason =>
                    {
                        Assert.AreEqual(GameShutdownReason.FatalSynchronizationFailure, reason);
                        Assert.AreEqual(UniTaskStatus.Pending, initialApply.Status);
                        SessionState.SetInt("TrainFatalExit_Notices", SessionState.GetInt("TrainFatalExit_Notices", 0) + 1);
                        // 終了通知内の購読解除と通常終了の再入にも保存させない。
                        // Unsubscribing and re-entering normal quit during the notice must not save.
                        client.Handler.Dispose();
                        Assert.AreEqual(ShutdownFlushResult.AlreadyShutdown, GameShutdownEvent.FireGameShutdownAsync(GameShutdownReason.IntentionalExit).GetAwaiter().GetResult());
                        Assert.IsFalse(GameShutdownEvent.NotifyUnannouncedExit());
                    });
                    var replayedDeltas = 0;
                    using var replay = dispatcher.SubscribeEventResponse(TrainUnitTickDiffBundleEventPacket.EventTag, _ => replayedDeltas++);
                    source.OnNext(new EventMessagePack(TrainFullSnapshotEventPacket.RailGraphFullSnapshotEventTag, new byte[] { 0xC1 }));
                    var delta = new TrainUnitTickDiffBundleMessagePack(1, 1, 1, uint.MaxValue, uint.MaxValue,
                        Array.Empty<global::Game.Train.Unit.TrainTickDiffData>());
                    source.OnNext(new EventMessagePack(TrainUnitTickDiffBundleEventPacket.EventTag, MessagePackSerializer.Serialize(delta)));
                    LogAssert.Expect(LogType.Error, new Regex("TrainFullSnapshot.*initial apply failed"));
                    Assert.DoesNotThrow(() => dispatcher.InitializeDispatch());

                    // 待機結果を読む前に保存なし終了と後続replay停止を観測する。
                    // Observe no-save exit and stopped delta application before reading the wait result.
                    Assert.AreEqual(1, SessionState.GetInt("TrainFatalExit_Notices", 0));
                    Assert.AreEqual(0, SessionState.GetInt("TrainFatalExit_Flushes", 0));
                    Assert.AreEqual(1, replayedDeltas);
                    Assert.IsTrue(client.Context.State.IsStopped);
                    Assert.IsFalse(client.Context.Events.TryFlushEvent(1ul << 32 | 1));
                    GameShutdownEvent.QuitApplicationAsync().GetAwaiter().GetResult();
                    Assert.AreEqual(0, SessionState.GetInt("TrainFatalExit_Flushes", 0));
                    var failure = Assert.Throws<TrainInitialSnapshotException>(() => initialApply.GetAwaiter().GetResult());
                    var callback = typeof(InitializeScenePipeline).GetMethod("OnMainGameInitializationFailed", BindingFlags.NonPublic | BindingFlags.Static);
                    Assert.IsNotNull(callback);
                    var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
                    callback.Invoke(null, new object[] { failure });
                    Assert.AreEqual(scene, UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
                    Assert.AreEqual(1, SessionState.GetInt("TrainFatalExit_Notices", 0));
                    while (EditorApplication.isPlaying) yield return null;
                }
                finally
                {
                    TestReflection.SetStaticProperty(typeof(ClientContext), "VanillaApi", previousApi);
                }
            }
        }

        private sealed class SaveProbe : IGameShutdownParticipant
        {
            public Cysharp.Threading.Tasks.UniTask<ShutdownFlushResult> FlushOnShutdownAsync()
            {
                SessionState.SetInt("TrainFatalExit_Flushes", SessionState.GetInt("TrainFatalExit_Flushes", 0) + 1);
                return Cysharp.Threading.Tasks.UniTask.FromResult(ShutdownFlushResult.Flushed);
            }
        }
    }
}
