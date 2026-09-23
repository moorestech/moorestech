using System;
using System.Collections;
using System.Text.RegularExpressions;
using System.Threading;
using Client.Game.InGame.BeltSegment.Model;
using Client.Game.InGame.BeltSegment.Network;
using Cysharp.Threading.Tasks;
using MessagePack;
using NUnit.Framework;
using Server.Event.EventReceive;
using Server.Util.MessagePack.BeltSegment;
using UnityEngine;
using UnityEngine.TestTools;
using static Client.Tests.BeltSegment.Network.BeltNetworkFixture;
namespace Client.Tests.BeltSegment.Network
{
    public sealed class BeltSnapshotRaceTest
    {
        [Test]
        public void NewEventWinsPendingInitialResponseWithoutFreeingItsSlot()
        {
            using var cancellation = new CancellationTokenSource();
            var world = World(); var request = new ControlledBeltRequester(); var events = new BeltTestEvents();
            var recovery = new BeltWorldRecovery(world, request, cancellation.Token);
            var handler = new BeltWorldEventHandler(events, world, recovery, cancellation.Token); handler.Initialize();
            var waiting = handler.WaitForInitialApplyAsync(); Assert.AreEqual(UniTaskStatus.Pending, waiting.Status);
            events.Send(BeltWorldEventPacket.SnapshotTag, MessagePackSerializer.Serialize(new BeltWorldSnapshotMessagePack(Empty(3, 2, 2))));
            Assert.AreEqual(UniTaskStatus.Succeeded, waiting.Status); Assert.IsTrue(recovery.IsPending);
            recovery.Request(); Assert.AreEqual(1, request.Calls.Count);
            request.Calls[0].TrySetResult(Empty(1, 1, 1));
            Assert.AreEqual(3UL, world.Position.Tick); Assert.AreEqual(2UL, world.Generation); Assert.IsFalse(recovery.IsPending);
            world.Simulation.Dispose();
        }
        [Test]
        public void ResponseThenReplacementAndOldSnapshotsCannotRollBack()
        {
            using var cancellation = new CancellationTokenSource();
            var world = World(); var request = new ControlledBeltRequester(); var events = new BeltTestEvents();
            var recovery = new BeltWorldRecovery(world, request, cancellation.Token);
            var handler = new BeltWorldEventHandler(events, world, recovery, cancellation.Token); handler.Initialize();
            request.Calls[0].TrySetResult(Empty(5, 1, 1)); world.ReceiveSnapshot(Empty(5, 2, 2));
            world.ReceiveSnapshot(Empty(5, 1, 2)); world.ReceiveSnapshot(Empty(4, 5, 3));
            Assert.AreEqual(2UL, world.Generation); Assert.AreEqual(2U, world.Position.Sequence);
            Assert.AreEqual(UniTaskStatus.Succeeded, handler.WaitForInitialApplyAsync().Status); world.Simulation.Dispose();
        }
        [Test]
        public void MalformedInitialEventSettlesWaiterWithoutAbortingNextDispatch()
        {
            using var cancellation = new CancellationTokenSource();
            var world = World(); var request = new ControlledBeltRequester(); var events = new BeltTestEvents();
            var recovery = new BeltWorldRecovery(world, request, cancellation.Token);
            var handler = new BeltWorldEventHandler(events, world, recovery, cancellation.Token); handler.Initialize();
            LogAssert.Expect(LogType.Error, new Regex("\\[BeltWorld\\] Network state apply failed:"));
            Assert.DoesNotThrow(() => events.Send(BeltWorldEventPacket.SnapshotTag, new byte[] { 0xc0 }));
            var waiting = handler.WaitForInitialApplyAsync(); Assert.AreEqual(UniTaskStatus.Faulted, waiting.Status);
            Assert.Throws<ArgumentException>(() => waiting.GetAwaiter().GetResult());
            events.Send(BeltWorldEventPacket.SnapshotTag, MessagePackSerializer.Serialize(new BeltWorldSnapshotMessagePack(Empty(0, 0, 1))));
            Assert.AreEqual(BeltStreamStatus.Running, world.Status); cancellation.Cancel(); world.Simulation.Dispose();
        }
        [UnityTest]
        public IEnumerator TimeoutRetriesSingleFlightAndCancellationSettlesStartup() => UniTask.ToCoroutine(async () =>
        {
            using var cancellation = new CancellationTokenSource();
            var world = World(); var request = new ControlledBeltRequester(); var events = new BeltTestEvents();
            var recovery = new BeltWorldRecovery(world, request, cancellation.Token);
            var handler = new BeltWorldEventHandler(events, world, recovery, cancellation.Token); handler.Initialize();
            request.Calls[0].TrySetException(new TimeoutException("10 second packet timeout"));
            recovery.Request(); Assert.AreEqual(1, request.Calls.Count);
            await UniTask.Delay(650, ignoreTimeScale: true);
            Assert.AreEqual(2, request.Calls.Count); Assert.IsTrue(recovery.IsPending);
            cancellation.Cancel(); await UniTask.Yield();
            Assert.IsFalse(recovery.IsPending); var waiting = handler.WaitForInitialApplyAsync();
            Assert.AreEqual(UniTaskStatus.Canceled, waiting.Status);
            Assert.Throws<OperationCanceledException>(() => waiting.GetAwaiter().GetResult());
        });
        [Test]
        public void InitialGpuConstructionFailureSettlesWaiterWithoutPublishingSnapshot()
        {
            using var cancellation = new CancellationTokenSource();
            var world = new ClientBeltWorld(null); var request = new ControlledBeltRequester(); var events = new BeltTestEvents();
            var recovery = new BeltWorldRecovery(world, request, cancellation.Token);
            var handler = new BeltWorldEventHandler(events, world, recovery, cancellation.Token); handler.Initialize();
            LogAssert.Expect(LogType.Error, new Regex("\\[BeltWorld\\] Network state apply failed:"));
            request.Calls[0].TrySetResult(Empty(0, 0, 1));
            var waiting = handler.WaitForInitialApplyAsync(); Assert.AreEqual(UniTaskStatus.Faulted, waiting.Status);
            Assert.Catch<Exception>(() => waiting.GetAwaiter().GetResult());
            Assert.AreEqual(BeltStreamStatus.Recovering, world.Status); Assert.IsNull(world.Simulation);
            cancellation.Cancel();
        }
        [UnityTest]
        public IEnumerator CancellationDuringRetryDelayDoesNotSendAnotherRequest() => UniTask.ToCoroutine(async () =>
        {
            using var cancellation = new CancellationTokenSource();
            var world = World(); var request = new ControlledBeltRequester(); var events = new BeltTestEvents();
            var recovery = new BeltWorldRecovery(world, request, cancellation.Token);
            var handler = new BeltWorldEventHandler(events, world, recovery, cancellation.Token); handler.Initialize();
            request.Calls[0].TrySetException(new TimeoutException("10 second packet timeout"));
            cancellation.Cancel(); await UniTask.Delay(650, ignoreTimeScale: true);
            Assert.AreEqual(1, request.Calls.Count); Assert.IsFalse(recovery.IsPending);
            Assert.Throws<OperationCanceledException>(() => handler.WaitForInitialApplyAsync().GetAwaiter().GetResult());
        });
        [Test]
        public void EmptyResponseCompletesStartupAndLaterRecoveryDoesNotResetWaiter()
        {
            using var cancellation = new CancellationTokenSource();
            var world = World(); var request = new ControlledBeltRequester(); var events = new BeltTestEvents();
            var recovery = new BeltWorldRecovery(world, request, cancellation.Token);
            var handler = new BeltWorldEventHandler(events, world, recovery, cancellation.Token); handler.Initialize();
            request.Calls[0].TrySetResult(Empty(0, 0, 1));
            Assert.AreEqual(UniTaskStatus.Succeeded, handler.WaitForInitialApplyAsync().Status);
            world.Recover("test gap"); Assert.AreEqual(2, request.Calls.Count);
            Assert.AreEqual(UniTaskStatus.Succeeded, handler.WaitForInitialApplyAsync().Status);
            cancellation.Cancel(); world.Simulation.Dispose();
        }
    }
}
