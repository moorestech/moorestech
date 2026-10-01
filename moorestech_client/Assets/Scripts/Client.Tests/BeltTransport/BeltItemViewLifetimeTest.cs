using System;
using System.Collections;
using System.Collections.Generic;
using Client.Game.InGame.BeltTransport;
using Client.Game.InGame.Entity;
using Core.Master;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Client.Tests.BeltTransport
{
    public sealed class BeltItemViewLifetimeTest
    {
        [UnityTest]
        public IEnumerator PendingCreationUsesLatestPositionTest() => UniTask.ToCoroutine(async () =>
        {
            var factory = new DelayedFactory();
            var store = new BeltItemViewStore(factory);
            var first = store.ApplyAsync(BeltTestState.Snapshot(1, 0, true));
            await store.ApplyAsync(BeltTestState.Snapshot(200, 0, true));
            Assert.AreEqual(1, factory.Pending.Count);
            var view = new View();
            factory.Pending[0].TrySetResult(view);
            await first;
            Assert.AreEqual(new Vector3(-1.5f, 3.35f, 4.5f - 56f / 256), view.Position);
            Assert.IsFalse(view.Destroyed);
            await store.ApplyAsync(BeltTestState.Snapshot(200, 0, false));
            Assert.IsTrue(view.Destroyed);
        });
        [UnityTest]
        public IEnumerator RemovedDuringCreationCannotCreateGhostTest() => UniTask.ToCoroutine(async () =>
        {
            var factory = new DelayedFactory();
            var store = new BeltItemViewStore(factory);
            var first = store.ApplyAsync(BeltTestState.Snapshot(64, 0, true));
            await store.ApplyAsync(BeltTestState.Snapshot(64, 0, false));
            var departed = new View(); factory.Pending[0].TrySetResult(departed); await first;
            Assert.IsTrue(departed.Destroyed);
            var second = store.ApplyAsync(BeltTestState.Snapshot(128, 0, true));
            var restored = new View(); factory.Pending[1].TrySetResult(restored); await second;
            Assert.IsFalse(restored.Destroyed);
            Assert.AreEqual(4f, restored.Position.z);
        });
        private sealed class DelayedFactory : IBeltItemViewFactory
        {
            internal readonly List<UniTaskCompletionSource<IEntityObject>> Pending = new();
            public UniTask<IEntityObject> CreateAsync(Guid id, ItemId item, Vector3 position)
            { var pending = new UniTaskCompletionSource<IEntityObject>(); Pending.Add(pending); return pending.Task; }
        }
        private sealed class View : IEntityObject
        {
            public long EntityId => 0;
            internal Vector3 Position;
            internal bool Destroyed;
            public void Initialize(long id) { }
            public void SetDirectPosition(Vector3 position) { Position = position; }
            public void Destroy() { Destroyed = true; }
            public void SetPositionWithLerp(Vector3 position) => Assert.Fail("CPU view must use direct positioning.");
            public void SetEntityData(byte[] data) => Assert.Fail("CPU view must not use legacy entity data.");
        }
    }
}
