using System;
using System.Collections;
using System.Linq;
using System.Threading;
using Client.Game.Common;
using Client.Game.InGame.Context;
using Client.Game.InGame.Player;
using Client.Game.InGame.Player.StateController;
using Client.Game.InGame.Riding;
using Client.Game.InGame.Train.Network;
using Client.Game.InGame.Train.Network.TickSynchronization;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.Train.View;
using Client.Game.InGame.Train.View.Object.Core;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Network.API;
using Client.Tests.Common;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using StarterAssets;
using UniRx;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest
{
    [Category("CiShardClientPlay3")]
    public class TrainTickSynchronizationPlayTest
    {
        [UnityTest]
        public IEnumerator SavedRidingWorld_RestoresSeatThenAppliesResyncAndAdvances()
        {
            EnterPlayModeUtil();
            yield return new EnterPlayMode(expectDomainReload: true);
            LogAssert.ignoreFailingMessages = true;
            yield return null;
            LogAssert.ignoreFailingMessages = false;
            yield return Body().ToCoroutine();

            // 終了時のsocket切断だけを既存bootと同様に許容する。
            // Permit only teardown socket disconnects, following the existing boot fixture.
            LogAssert.ignoreFailingMessages = true;
            yield return new ExitPlayMode();
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

            #region Internal
            async UniTask Body()
            {
                using var fixture = new SavedRidingWorldFixture();
                var initialized = false;
                using var subscription = GameInitializedEvent.OnGameInitialized.Subscribe(_ => initialized = true);
                var tracing = TraceStartup().Preserve();
                await LoadMainGame(fixture.ServerDirectory, fixture.WorldDirectory);
                await UniTask.WaitUntil(() => initialized).Timeout(TimeSpan.FromSeconds(180));
                await tracing;

                // 実loaderとhandshakeが保存車両と座席を復元したことを確認する。
                // Verify the real loader and handshake restored the saved car and seat.
                var resolver = ClientDIContext.DIContainer.DIContainerResolver;
                var handshake = resolver.Resolve<InitialHandshakeResponse>();
                Assert.IsNotNull(handshake.RidingTarget);
                Assert.AreEqual(fixture.CarId.AsPrimitive(), handshake.RidingTarget.TrainCarInstanceId);
                Assert.AreEqual(fixture.SeatIndex, handshake.RidingSeatIndex);
                var handler = resolver.Resolve<TrainFullSnapshotEventNetworkHandler>();
                Assert.AreEqual(UniTaskStatus.Succeeded, handler.WaitForInitialApplyAsync().Status);
                Assert.AreEqual(UIStateEnum.TrainHUDScreen, resolver.Resolve<UIStateControl>().CurrentState);
                Assert.AreEqual(PlayerStateEnum.Riding, resolver.Resolve<PlayerStateController>().CurrentState);
                Assert.IsTrue(resolver.Resolve<TrainHUDScreenState>().IsRiding);
                VerifySeat(fixture, resolver);
                await VerifyResync(fixture, resolver);

                #region Internal
                async UniTask TraceStartup()
                {
                    while (!initialized)
                    {
                        if (PlayerSystemContainer.Instance != null && ClientDIContext.DIContainer != null)
                        {
                            var activeResolver = ClientDIContext.DIContainer.DIContainerResolver;
                            var activePlayer = (PlayerObjectController)PlayerSystemContainer.Instance.PlayerObjectController;
                            var snapshot = activeResolver.Resolve<TrainFullSnapshotEventNetworkHandler>();
                            var views = activeResolver.Resolve<TrainCarObjectDatastore>();
                            if (snapshot.WaitForInitialApplyAsync().Status != UniTaskStatus.Succeeded || !views.TryGetEntity(fixture.CarId, out _))
                            {
                                Assert.IsFalse(TestReflection.GetField<bool>(activePlayer, "isRuntimeStarted"));
                                Assert.IsFalse(activePlayer.GetComponent<ThirdPersonController>().enabled);
                            }
                        }
                        await UniTask.Yield();
                    }
                }
                #endregion
            }
            #endregion
        }

        private static void VerifySeat(SavedRidingWorldFixture fixture, IObjectResolver resolver)
        {
            var views = resolver.Resolve<TrainCarObjectDatastore>();
            Assert.IsTrue(views.TryGetEntity(fixture.CarId, out var view));
            Assert.IsTrue(view.GetComponent<SeatPositionResolver>().TryGetSeatPosition(fixture.SeatIndex, out var seat));
            var player = (PlayerObjectController)PlayerSystemContainer.Instance.PlayerObjectController;
            Assert.IsTrue(TestReflection.GetField<bool>(player, "isRuntimeStarted"));
            Assert.IsFalse(player.GetComponent<ThirdPersonController>().enabled);
            TestReflection.InvokePrivate(player, "LateUpdate");
            AssertPose(player, seat);

            // 実座席markerの変化を追い、固定poseの一致だけでは通らないようにする。
            // Move the actual seat marker so a coincidentally matching fixed pose cannot pass.
            var position = seat.localPosition;
            var rotation = seat.localRotation;
            seat.localPosition += new Vector3(1, 2, 3);
            seat.localRotation *= Quaternion.Euler(0, 35, 0);
            TestReflection.InvokePrivate(player, "LateUpdate");
            AssertPose(player, seat);
            seat.SetLocalPositionAndRotation(position, rotation);
            TestReflection.InvokePrivate(player, "LateUpdate");
        }

        private static void AssertPose(PlayerObjectController player, Transform seat)
        {
            Assert.Less(Vector3.Distance(player.Position, seat.position), 0.001f);
            Assert.Less(Quaternion.Angle(player.transform.rotation, seat.rotation), 0.01f);
        }

        private static async UniTask VerifyResync(SavedRidingWorldFixture fixture, IObjectResolver resolver)
        {
            var context = resolver.Resolve<TrainTickContext>();
            var handler = resolver.Resolve<TrainFullSnapshotEventNetworkHandler>();
            var trains = resolver.Resolve<TrainUnitClientCache>();
            var rails = resolver.Resolve<RailGraphClientCache>();
            var views = resolver.Resolve<TrainCarObjectDatastore>();
            var beforeTrain = trains.Units[fixture.TrainId];
            var railNodeId = rails.Nodes.First(node => node != null).NodeId;
            var beforeRail = rails.Nodes[railNodeId];
            Assert.IsTrue(views.TryGetEntity(fixture.CarId, out var beforeView));
            Assert.AreEqual(0, TestReflection.GetField<int>(resolver.Resolve<TrainUnitHashVerifier>(), "_resyncInProgress"));

            // 要求前に3つの購読を揃え、別要求の混入も件数で検出する。
            // Subscribe to all three signals before requesting; counts detect interleaved requests too.
            using var observation = new TrainSnapshotApplyObservation(ClientContext.VanillaApi.Event, handler,
                context, trains, rails, views, fixture.TrainId, fixture.CarId, railNodeId);
            var ack = ClientContext.VanillaApi.Response.SendTrainResync(true, CancellationToken.None).Preserve();
            for (var frame = 0; frame < 300 && !observation.HasCompletePair; frame++) await UniTask.Yield();
            Assert.IsTrue(observation.HasCompletePair, observation.Diagnostic);
            Assert.Less(observation.RailReceivedOrder, observation.TrainAppliedOrder);
            Assert.AreEqual(observation.RailWatermark, observation.TrainWatermark);
            Assert.AreEqual(observation.TrainWatermark, observation.AppliedWatermark);
            Assert.AreEqual(observation.AppliedWatermark, observation.StateAtApply);
            Assert.AreEqual(observation.TrainPayloadHash, observation.TrainHashAtApply);
            Assert.AreEqual(observation.RailPayloadHash, observation.RailHashAtApply);
            Assert.AreNotSame(beforeTrain, observation.TrainUnitAtApply);
            Assert.AreNotSame(beforeRail, observation.RailNodeAtApply);
            Assert.AreNotSame(beforeView, observation.CarViewAtApply);
            Assert.AreEqual(fixture.CarId, observation.CarViewAtApply.TrainCarInstanceId);
            Assert.IsNotNull(await ack.Timeout(TimeSpan.FromSeconds(15)));

            var appliedTick = (uint)(observation.AppliedWatermark >> 32);
            for (var frame = 0; frame < 300 && context.State.GetTick() <= appliedTick; frame++) await UniTask.Yield();
            Assert.Greater(context.State.GetTick(), appliedTick, observation.Diagnostic);
            Assert.AreEqual(1, observation.RailCount, observation.Diagnostic);
            Assert.AreEqual(1, observation.TrainCount, observation.Diagnostic);
            Assert.AreEqual(1, observation.AppliedCount, observation.Diagnostic);
        }
    }
}
