using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Client.Game.InGame.BeltTransport;
using Client.Game.InGame.Entity;
using Client.Game.InGame.Entity.Factory;
using Core.BeltTransport;
using MessagePack;
using Server.Event.EventReceive;
using Server.Util.MessagePack.BeltTransport;
using Client.Tests.Inventory;
using Core.Master;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Client.Tests.BeltTransport
{
    public sealed class BeltMissingAssetTest
    {
        [Test]
        public void MissingPrefabReturnsFailureWithoutInstantiatingNullTest()
        {
            var parent = new GameObject("Missing asset parent");
            var factory = new BeltConveyorItemEntityObjectFactory(new MissingLoader());
            var result = factory.CreateItem(parent.transform, 17, new ItemId(1), Vector3.zero).GetAwaiter().GetResult();
            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual("Missing fixture prefab", result.FailureReason);
            Assert.AreEqual(0, parent.transform.childCount);
            UnityEngine.Object.DestroyImmediate(parent);
        }
        [UnityTest]
        public IEnumerator MissingViewReleasesPendingOwnershipAndIsNotRetriedTest() => UniTask.ToCoroutine(async () =>
        {
            var factory = new DelayedFactory();
            var store = new BeltItemViewStore(factory);
            var initial = store.ApplyAsync(BeltTestState.Snapshot(1, 0, true));
            var pendingWait = store.WaitForPendingAsync();
            LogAssert.Expect(LogType.Error, new Regex("Belt item .* could not be rendered: Missing fixture prefab"));
            factory.Pending[0].TrySetResult(BeltItemCreationResult.Missing("Missing fixture prefab"));
            await initial; await pendingWait;
            Assert.IsNotNull(store.FailureReason);
            // 失敗も所有状態を解除し、同GUIDの反復通知ではロードし直さない。
            // Release ownership after failure without retrying on repeated identity notifications.
            foreach (string field in new[] { "_creating", "_pendingTasks" })
            {
                var collection = typeof(BeltItemViewStore).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(store);
                Assert.AreEqual(0, collection.GetType().GetProperty("Count").GetValue(collection), field);
            }
            await store.ApplyAsync(BeltTestState.Snapshot(256, 0, true));
            await store.ApplyAsync(BeltTestState.Snapshot(256, 0, false));
            await store.ApplyAsync(BeltTestState.Snapshot(256, 0, true));
            Assert.AreEqual(1, factory.Pending.Count);
        });
        [TestCase(false)]
        [TestCase(true)]
        public void MissingInitialOrBufferedViewInterruptsStartupWaitingTest(bool buffered)
        {
            var events = new CapturingVanillaApiEvent();
            var handler = new BeltNetworkEventHandler(BeltInitialEventBufferTest.Initial(), events);
            var factory = new DelayedFactory();
            var renderer = new BeltItemRenderer(handler, factory);
            renderer.Initialize();
            if (buffered)
            {
                factory.Pending[0].TrySetResult(BeltItemCreationResult.Created(new View()));
                var item = new BeltCellItemState(1, 256, BeltDirection.Back, 0, new BeltItem(new Guid("00000002-0000-0000-0000-000000000000"), 1), false);
                var change = new BeltCellItemsChange(1, new[] { item });
                var tick = new BeltTickDifference(11, Array.Empty<BeltBoundaryChange>(), Array.Empty<BeltOutputResult>(), new BeltBoundaryChange[] { change });
                events.Dispatch(BeltTickCompletedEventPacket.EventTag, MessagePackSerializer.Serialize(new BeltTickMessagePack(tick)));
            }
            var wait = renderer.WaitForInitialApplyAsync();
            Assert.AreEqual(UniTaskStatus.Pending, wait.Status);
            LogAssert.Expect(LogType.Error, new Regex("Belt item .* could not be rendered: Missing fixture prefab"));
            factory.Pending[buffered ? 1 : 0].TrySetResult(BeltItemCreationResult.Missing("Missing fixture prefab"));
            Assert.Throws<InvalidOperationException>(() => wait.GetAwaiter().GetResult());
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
        private sealed class MissingLoader : IBeltItemPrefabLoader
        {
            public UniTask<BeltItemPrefabLoadResult> LoadAsync(ItemId itemId) => UniTask.FromResult(BeltItemPrefabLoadResult.Missing("Missing fixture prefab"));
        }
        private sealed class DelayedFactory : IBeltItemViewFactory
        {
            internal readonly List<UniTaskCompletionSource<BeltItemCreationResult>> Pending = new();
            public UniTask<BeltItemCreationResult> CreateAsync(Guid id, ItemId item, Vector3 position)
            { var task = new UniTaskCompletionSource<BeltItemCreationResult>(); Pending.Add(task); return task.Task; }
        }
    }
}
