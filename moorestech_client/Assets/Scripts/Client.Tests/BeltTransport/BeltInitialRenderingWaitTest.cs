using System;
using System.Collections;
using System.Collections.Generic;
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
            var buffer = BeltTestState.Buffer(out var state);
            var handler = new BeltNetworkEventHandler(BeltInitialEventBufferTest.Initial(), events, buffer);
            var factory = new DelayedFactory();
            var renderer = new BeltItemRenderer(handler, factory);
            renderer.Initialize();
            factory.Pending[0].TrySetResult(new View());
            // 初回生成の後、バッファ再生で別GUIDの生成が始まる状況を作る。
            // Start another identity during buffered replay after the first creation completed.
            var item = new BeltCellItemState(1, 256, BeltDirection.Back, 0, new BeltItem(new Guid("00000002-0000-0000-0000-000000000000"), 1), false);
            var change = new BeltCellItemsChange(1, new[] { item });
            var tick = new BeltTickDifference(11, Array.Empty<BeltBoundaryChange>(), Array.Empty<BeltOutputResult>(), new BeltBoundaryChange[] { change }, BeltTestState.Order(11, 0, 1));
            events.Dispatch(BeltTickCompletedEventPacket.EventTag, MessagePackSerializer.Serialize(new BeltTickMessagePack(tick)));
            Assert.AreEqual(1, factory.Pending.Count, "Receipt alone must not create the next tick's item.");
            BeltTestState.FlushTick(buffer, state, 11);
            var wait = renderer.WaitForInitialApplyAsync();
            if (wait.Status == UniTaskStatus.Faulted) wait.GetAwaiter().GetResult();
            Assert.AreEqual(UniTaskStatus.Pending, wait.Status);
            Assert.AreEqual(2, factory.Pending.Count);
            factory.Pending[1].TrySetResult(new View());
            await wait;
        });
        private sealed class DelayedFactory : IBeltItemViewFactory
        {
            internal readonly List<UniTaskCompletionSource<IEntityObject>> Pending = new();
            public UniTask<IEntityObject> CreateAsync(Guid id, ItemId item, Vector3 position)
            { var task = new UniTaskCompletionSource<IEntityObject>(); Pending.Add(task); return task.Task; }
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
