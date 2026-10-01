using Client.Game.InGame.Entity.Factory;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Client.Game.InGame.BeltTransport;
using Client.Game.InGame.Entity;
using Client.Tests.Inventory;
using Core.BeltTransport;
using Core.Master;
using Cysharp.Threading.Tasks;
using MessagePack;
using NUnit.Framework;
using Server.Event.EventReceive;
using Server.Util.MessagePack.BeltTransport;
using UnityEngine;
using UnityEngine.TestTools;
namespace Client.Tests.BeltTransport
{
    public sealed class BeltInitialRenderingWaitTest
    {
        [UnityTest]
        public IEnumerator BufferedNewIdentityIsIncludedInStartupWaitTest() => UniTask.ToCoroutine(async () =>
        {
            var events = new CapturingVanillaApiEvent();
            var handler = new BeltNetworkEventHandler(BeltInitialEventBufferTest.Initial(), events);
            var factory = new DelayedFactory();
            var renderer = new BeltItemRenderer(handler, factory);
            renderer.Initialize();
            factory.Pending[0].TrySetResult(BeltItemCreationResult.Created(new View()));
            // 初回生成の後、バッファ再生で別GUIDの生成が始まる状況を作る。
            // Start another identity during buffered replay after the first creation completed.
            var item = new BeltCellItemState(1, 256, BeltDirection.Back, 0, new BeltItem(new Guid("00000002-0000-0000-0000-000000000000"), 1), false);
            var change = new BeltCellItemsChange(1, new[] { item });
            var tick = new BeltTickDifference(11, Array.Empty<BeltBoundaryChange>(), Array.Empty<BeltOutputResult>(), new BeltBoundaryChange[] { change });
            events.Dispatch(BeltTickCompletedEventPacket.EventTag, MessagePackSerializer.Serialize(new BeltTickMessagePack(tick)));
            var wait = renderer.WaitForInitialApplyAsync();
            if (wait.Status == UniTaskStatus.Faulted) wait.GetAwaiter().GetResult();
            Assert.AreEqual(UniTaskStatus.Pending, wait.Status);
            Assert.AreEqual(2, factory.Pending.Count);
            factory.Pending[1].TrySetResult(BeltItemCreationResult.Created(new View()));
            await wait;
        });
        [Test]
        public void MalformedPacketInterruptsPendingInitialAssetLoadTest()
        {
            var events = new CapturingVanillaApiEvent();
            var handler = new BeltNetworkEventHandler(BeltInitialEventBufferTest.Initial(), events);
            var factory = new DelayedFactory();
            var renderer = new BeltItemRenderer(handler, factory);
            renderer.Initialize();
            var wait = renderer.WaitForInitialApplyAsync();
            if (wait.Status == UniTaskStatus.Faulted) wait.GetAwaiter().GetResult();
            Assert.AreEqual(UniTaskStatus.Pending, wait.Status);
            LogAssert.Expect(LogType.Error, new Regex("Belt transport packet failed:"));
            events.Dispatch(BeltTickCompletedEventPacket.EventTag, new byte[] { 0xc1 });
            Assert.AreEqual(UniTaskStatus.Faulted, wait.Status);
            Assert.Throws<InvalidOperationException>(() => wait.GetAwaiter().GetResult());
            factory.Pending[0].TrySetResult(BeltItemCreationResult.Created(new View()));
        }
        private sealed class DelayedFactory : IBeltItemViewFactory
        {
            internal readonly List<UniTaskCompletionSource<BeltItemCreationResult>> Pending = new();
            public UniTask<BeltItemCreationResult> CreateAsync(Guid id, ItemId item, Vector3 position)
            { var task = new UniTaskCompletionSource<BeltItemCreationResult>(); Pending.Add(task); return task.Task; }
        }
        private sealed class View : IEntityObject
        {
            public long EntityId => 0;
            public void Initialize(long id) { }
            public void SetDirectPosition(Vector3 position) { }
            public void Destroy() { }
            public void SetPositionWithLerp(Vector3 position) => Assert.Fail();
            public void SetEntityData(byte[] data) => Assert.Fail();
        }
    }
}
