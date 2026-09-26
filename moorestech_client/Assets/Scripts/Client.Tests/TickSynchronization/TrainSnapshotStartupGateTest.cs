using Core.Update.TickSynchronization;
using System;
using System.Collections;
using System.Runtime.Serialization;
using Client.Game.Common;
using Client.Game.InGame.Context;
using Client.Game.InGame.Control;
using Client.Game.InGame.Player;
using Client.Game.InGame.Player.StateController;
using Client.Game.InGame.Player.StateController.State;
using Client.Game.InGame.Riding;
using Client.Game.InGame.Train.Network;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Network.API;
using Client.Starter;
using Client.Starter.Initialization;
using Client.Tests.Common;
using Client.Tests.EditModeInPlayingTest;
using Client.Tests.TickSynchronization;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using MessagePack;
using Server.Util.MessagePack;
using Server.Event;
using StarterAssets;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;
using VContainer;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest
{
    public class TrainSnapshotStartupGateTest
    {
        [Test]
        public void TrainInitialApply_RemainsPendingBeforePayload()
        {
            using var handler = new TrainFullSnapshotEventNetworkHandler(null, null, null);
            Assert.AreEqual(UniTaskStatus.Pending, handler.WaitForInitialApplyAsync().Preserve().Status);
        }

        [UnityTest]
        [Category("CiShardClientPlay3")]
        public IEnumerator SavedRidingHandshake_DelayedTrainPayloadBlocksRuntimeUntilRealSeatExists()
        {
            EnterPlayModeUtil();
            yield return new EnterPlayMode(expectDomainReload: true);
            LogAssert.ignoreFailingMessages = true;
            yield return null;
            LogAssert.ignoreFailingMessages = false;
            // domain reload後にlocal functionのcaptureを生成する。
            // Create local-function captures after the PlayMode domain reload.
            {
                using var world = new SavedRidingWorldFixture();
                using var client = new TrainSnapshotClientFixture();
                var root = new GameObject("DelayedTrainStartup");
                root.SetActive(false);
                var previousPlayer = PlayerSystemContainer.Instance;
                var previousApi = ClientContext.VanillaApi;
                var player = root.AddComponent<PlayerObjectController>();
                root.AddComponent<CharacterController>();
                root.AddComponent<StarterAssetsInputs>();
                var controller = root.AddComponent<ThirdPersonController>();
                TestReflection.SetField(player, "controller", controller);
                player.Initialize(Vector3.zero, Vector3.zero);
                var playerContainer = root.AddComponent<PlayerSystemContainer>();
                TestReflection.SetField(playerContainer, "playerObjectController", player);
                TestReflection.SetStaticProperty(typeof(PlayerSystemContainer), "Instance", playerContainer);

                // 通信受信ループなしで実イベント配送口を提供する。
                // Provide the real event dispatcher without starting a perpetual network receive loop.
                var exchange = (PacketExchangeManager)FormatterServices.GetUninitializedObject(typeof(PacketExchangeManager));
                using var eventSource = new Subject<EventMessagePack>();
                TestReflection.SetField(exchange, "_eventPacketSubject", eventSource);
                var api = (VanillaApi)FormatterServices.GetUninitializedObject(typeof(VanillaApi));
                typeof(VanillaApi).GetField("Event").SetValue(api, new VanillaApiEvent(exchange));
                TestReflection.SetStaticProperty(typeof(ClientContext), "VanillaApi", api);
                var states = new PlayerStateController(new PlayerStateDictionary(new NormalPlayerState(),
                    new RidingPlayerState(new TrainCarRideFollowTargetResolver(client.Views))));
                var hud = new TrainHUDScreenState(states, client.Trains, root.AddComponent<InGameCameraController>(), null);
                var ui = root.AddComponent<UIStateControl>();
                ui.Construct(new UIStateDictionary(null, null, null, null, null, null, null, null, null, null, hud, null));
                var builder = new ContainerBuilder();
                builder.RegisterInstance(ui);
                var resolver = builder.Build();
                var starter = root.AddComponent<MainGameStarter>();
                TestReflection.SetField(starter, "_resolver", resolver);
                var handshake = new InitialHandshakeResponse(world.Handshake, default);

                // グローバルcontextとUnityオブジェクトをassert失敗時も復元する。
                // Restore global contexts and Unity objects even when an assertion fails.
                try
                {
                    Assert.AreEqual(world.CarId.AsPrimitive(), handshake.RidingTarget.TrainCarInstanceId);
                    Assert.AreEqual(world.SeatIndex, handshake.RidingSeatIndex);
                    var waiting = RestoreAfterSnapshot().Preserve();
                    Assert.AreEqual(UniTaskStatus.Pending, waiting.Status);
                    Assert.IsFalse(TestReflection.GetField<bool>(player, "isRuntimeStarted"));
                    Assert.IsFalse(controller.enabled);
                    Assert.IsFalse(client.Views.TryGetEntity(world.CarId, out _));
                    client.ApplyRail(world.RailPayload);
                    Assert.AreEqual(UniTaskStatus.Pending, waiting.Status);
                    Assert.IsFalse(TestReflection.GetField<bool>(player, "isRuntimeStarted"));
                    client.ApplyTrain(world.TrainPayload);
                    waiting.GetAwaiter().GetResult();

                    Assert.AreEqual(UIStateEnum.TrainHUDScreen, ui.CurrentState);
                    Assert.AreEqual(PlayerStateEnum.Riding, states.CurrentState);
                    Assert.IsTrue(hud.IsRiding);
                    Assert.IsTrue(client.Views.TryGetEntity(world.CarId, out var view));
                    Assert.IsTrue(view.GetComponent<SeatPositionResolver>().TryGetSeatPosition(world.SeatIndex, out var seat));
                    TestReflection.InvokePrivate(player, "LateUpdate");
                    Assert.Less(Vector3.Distance(player.Position, seat.position), 0.001f);
                    Assert.IsFalse(controller.enabled);

                    // 座席を動かすことで実resolverが取得したmarkerへの追従を確認する。
                    // Moving the seat verifies following the marker chosen by the production resolver.
                    var position = seat.localPosition;
                    var rotation = seat.localRotation;
                    seat.localPosition += new Vector3(2, 1, 3);
                    seat.localRotation *= Quaternion.Euler(0, 40, 0);
                    TestReflection.InvokePrivate(player, "LateUpdate");
                    Assert.Less(Vector3.Distance(player.Position, seat.position), 0.001f);
                    Assert.Less(Quaternion.Angle(player.transform.rotation, seat.rotation), 0.01f);
                    seat.SetLocalPositionAndRotation(position, rotation);

                    // 実server通知payloadをbuffer経由で削除・再生成まで通す。
                    // Apply real server structural payloads through the buffer to deletion and recreation.
                    var structural = new TrainUnitSnapshotEventNetworkHandler(client.Context, client.Trains, client.Views);
                    TrainSnapshotClientFixture.Receive(structural, "OnEventReceived", world.DeletePayload);
                    Assert.IsTrue(client.Views.TryGetEntity(world.CarId, out _));
                    var deleted = MessagePackSerializer.Deserialize<TrainUnitSnapshotEventMessagePack>(world.DeletePayload);
                    Assert.IsTrue(client.Context.Events.TryFlushEvent(TickUnifiedIdUtility.CreateTickUnifiedId(deleted.ServerTick, deleted.TickSequenceId)));
                    Assert.IsFalse(client.Trains.TryGet(world.TrainId, out _));
                    Assert.IsFalse(client.Views.TryGetEntity(world.CarId, out _));
                    TrainSnapshotClientFixture.Receive(structural, "OnEventReceived", world.UpsertPayload);
                    Assert.IsFalse(client.Views.TryGetEntity(world.CarId, out _));
                    var upsert = MessagePackSerializer.Deserialize<TrainUnitSnapshotEventMessagePack>(world.UpsertPayload);
                    Assert.IsTrue(client.Context.Events.TryFlushEvent(TickUnifiedIdUtility.CreateTickUnifiedId(upsert.ServerTick, upsert.TickSequenceId)));
                    Assert.IsTrue(client.Trains.TryGet(world.TrainId, out _));
                    Assert.IsTrue(client.Views.TryGetEntity(world.CarId, out var recreated));
                    Assert.AreNotSame(view, recreated);
                }
                finally
                {
                    hud.OnExit();
                    TestReflection.SetStaticProperty(typeof(PlayerSystemContainer), "Instance", previousPlayer);
                    TestReflection.SetStaticProperty(typeof(ClientContext), "VanillaApi", previousApi);
                    resolver.Dispose();
                    UnityEngine.Object.DestroyImmediate(root);
                }

                #region Internal
                async UniTask RestoreAfterSnapshot()
                {
                    await InitialEventApplyWaiter.WaitAllAsync(new IInitialEventApplyWaitTarget[] { client.Handler });
                    playerContainer.StartPlayerRuntime();
                    starter.RestoreLoginState(handshake);
                }
                #endregion
            }
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // assertion失敗時も退出して次のテストへplay状態を残さない。
            // Leave PlayMode even after assertion failure so the next test starts cleanly.
            LogAssert.ignoreFailingMessages = true;
            try
            {
                if (EditorApplication.isPlaying) yield return new ExitPlayMode();
            }
            finally
            {
                SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);
                LogAssert.ignoreFailingMessages = false;
            }
        }
    }
}
