using System.Collections.Generic;
using System.Reflection;
using Core.Inventory;
using Core.Item.Interface;
using Game.Block.Blocks.Connector;
using Game.Block.Blocks.RocketLaunchPad;
using Game.Block.Event;
using Game.Block.Interface.Component;
using Game.Block.Interface.Event;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util.Rocket;
using UniRx;
using UnityEngine;

namespace Tests.CombinedTest.Game.RocketLaunchPad.Regression
{
    public class RocketLaunchPadLocalInventoryTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void 通常と復元の台は自台の全書込だけで待機を無効化する(bool restore)
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var pad = restore
                ? RocketLaunchPadSaveTestUtil.Load(RocketLaunchPadSaveTestUtil.CreateSavedWorld(RocketLaunchPadPhase.AwaitingBuildCommand))
                : RocketLaunchPadTestUtil.PlacePoweredPad(Vector3Int.zero, 100);
            var processor = pad.GetComponent<RocketLaunchPadProcessorComponent>();
            var inventory = pad.GetComponent<IOpenableBlockInventoryComponent>();
            var slots = (OpenableInventoryItemDataStoreService)ReadField(processor, "_slots");
            var sequence = ReadField(processor, "_sequence");
            var generation = sequence.GetType().GetField("_waitGeneration", BindingFlags.NonPublic | BindingFlags.Instance);
            var processed = sequence.GetType().GetField("_processedWaitGeneration", BindingFlags.NonPublic | BindingFlags.Instance);
            var fuel = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.RocketFuelId, 1);
            var worldEvents = (BlockOpenableInventoryUpdateEvent)ServerContext.BlockOpenableInventoryUpdateEvent;
            var networkChanges = 0;
            using var subscription = worldEvents.OnInventoryUpdated.Subscribe(_ => networkChanges++);
            var other = RocketLaunchPadTestUtil.PlacePoweredPad(new Vector3Int(20, 0, 0), 100);
            other.GetComponent<IOpenableBlockInventoryComponent>().InsertItem(fuel);
            processed.SetValue(sequence, generation.GetValue(sequence));

            // 他台の実書込も、同じIDを含む世界通知だけでも局所待機は変えない
            // Neither another pad's actual write nor a world-only notification with this ID changes local waits
            other.GetComponent<IOpenableBlockInventoryComponent>().InsertItem(fuel);
            worldEvents.OnInventoryUpdateInvoke(new BlockOpenableInventoryUpdateEventProperties(pad.BlockInstanceId, 1, fuel));
            Assert.AreEqual(generation.GetValue(sequence), processed.GetValue(sequence));

            // プレイヤー・ベルト入力・直接保管・成果・搬出の全実書込口を通す
            // Exercise actual writes through player, belt input, direct store, arrival and transfer paths
            var before = networkChanges;
            inventory.SetItem(1, fuel); AssertInvalidated();
            inventory.SetItem(1, ForUnitTestItemId.RocketFuelId, 2); AssertInvalidated();
            inventory.ReplaceItem(1, fuel); AssertInvalidated();
            inventory.ReplaceItem(1, ForUnitTestItemId.RocketFuelId, 2); AssertInvalidated();
            inventory.InsertItem(fuel); AssertInvalidated();
            inventory.InsertItem(fuel, InsertItemContext.Empty); AssertInvalidated();
            inventory.InsertItem(ForUnitTestItemId.RocketFuelId, 1); AssertInvalidated();
            inventory.InsertItem(new List<IItemStack> { fuel }); AssertInvalidated();
            slots.SetItem(1, fuel); AssertInvalidated();
            var output = (RocketLaunchPadOutputSlots)ReadField(processor, "_outputSlots");
            Assert.IsTrue(output.TryCompleteArrival(ForUnitTestItemId.SatellitePayloadId, 1,
                new FixedOutputArrivalService(new[] { ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ComputeDataId, 1) }), out _)); AssertInvalidated();
            var layout = new RocketLaunchPadSlotLayout((Mooresmaster.Model.BlocksModule.RocketLaunchPadBlockParam)pad.BlockMasterElement.BlockParam);
            new RocketLaunchPadOutputSlots(layout, slots, new AcceptAll()).PushToConnected(); AssertInvalidated();
            Assert.AreEqual(11, networkChanges - before);

            // 台の破棄で購読だけでなくTemplate所有の通知源も解放する
            // Destroying the pad releases the template-owned source as well as its subscription
            var lifetime = (CompositeDisposable)ReadField(processor, "_subscriptions");
            var source = (Subject<Unit>)System.Linq.Enumerable.First(lifetime);
            processor.Destroy();
            Assert.IsTrue(lifetime.IsDisposed);
            Assert.Throws<System.ObjectDisposedException>(() => source.Subscribe(_ => { }));

            #region Internal
            void AssertInvalidated()
            {
                Assert.AreNotEqual(generation.GetValue(sequence), processed.GetValue(sequence));
                processed.SetValue(sequence, generation.GetValue(sequence));
            }
            #endregion
        }

        private static object ReadField(object instance, string name)
        {
            return instance.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance);
        }

        private sealed class AcceptAll : IBlockInventoryInserter
        {
            public IItemStack InsertItem(IItemStack itemStack) => ServerContext.ItemStackFactory.CreatEmpty();
        }
    }
}
