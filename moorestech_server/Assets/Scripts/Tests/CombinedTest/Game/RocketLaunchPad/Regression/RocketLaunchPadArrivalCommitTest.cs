using System;
using System.Linq;
using System.Text.RegularExpressions;
using Core.Update;
using Core.Master;
using Game.World.Interface.DataStore;
using Game.Block.Blocks.RocketLaunchPad;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using Game.Orbit;
using Game.SpaceElevator;
using Game.SpaceElevator.Save;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Protocol.PacketResponse;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Tests.CombinedTest.Game.SpaceElevator;
using Tests.Module.TestMod;
using Tests.Util.Rocket;
using Tests.Util;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.CombinedTest.Game.RocketLaunchPad.Regression
{
    public class RocketLaunchPadArrivalCommitTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void PoolRejectionPreservesCargoSaveRefundAndRetry(bool reload)
        {
            var (_, services) = SpaceElevatorTestUtil.CreateServer();
            var pool = ServerContext.GetService<SpaceElevatorCablePool>();
            pool.Restore(new SpaceElevatorCablePoolSaveData { CableCount = int.MaxValue });
            var pad = Prepare();
            var processor = pad.GetComponent<RocketLaunchPadProcessorComponent>();
            var before = JObject.FromObject(processor.GetSaveState());
            var refunds = processor.GetRefundItems().Select(i => (i.Id, i.Count)).ToArray();
            Assert.AreEqual(4, refunds.Length);

            // 本番の同期プール購読で加算拒否させる
            // Reject through the production synchronous pool subscription
            LogAssert.Expect(LogType.Error, new Regex("ケーブルプールの加算が整数範囲"));
            Assert.Throws<InvalidOperationException>(() => processor.Update());
            Assert.AreEqual(RocketLaunchPadPhase.ReadyToLaunch, RocketLaunchPadTestUtil.ReadState(processor).Phase);
            Assert.IsTrue(JToken.DeepEquals(before, JObject.FromObject(processor.GetSaveState())));
            CollectionAssert.AreEqual(refunds, processor.GetRefundItems().Select(i => (i.Id, i.Count)));
            Assert.IsFalse(ServerContext.GetService<RocketLaunchHistory>().HasLaunchedOnce);
            Assert.AreEqual(int.MaxValue, pool.Amount);
            Debug.Log($"W7 rejected phase={RocketLaunchPadTestUtil.ReadState(processor).Phase} cargo={refunds.Length} pool={pool.Amount} history=False");
            if (reload)
            {
                pad = RocketLaunchPadSaveTestUtil.Load(JObject.Parse(SaveLoadJsonTestHelper.AssembleSaveJson(services)));
                processor = pad.GetComponent<RocketLaunchPadProcessorComponent>();
                pool = ServerContext.GetService<SpaceElevatorCablePool>();
                Assert.IsTrue(JToken.DeepEquals(before, JObject.FromObject(processor.GetSaveState())));
                CollectionAssert.AreEqual(refunds, processor.GetRefundItems().Select(i => (i.Id, i.Count)));
            }

            // 容量解放後に同便だけを受理
            // Free only pool capacity and accept the same cargo once without inventory changes
            var arrivalCount = 0;
            using var sub = ServerContext.GetService<IOrbitalArrivalService>().OnOrbitalArrival.Subscribe(_ =>
            {
                arrivalCount++;
                Assert.AreEqual(RocketLaunchPadPhase.ReadyToLaunch, RocketLaunchPadTestUtil.ReadState(processor).Phase);
                Assert.AreEqual(4, processor.GetRefundItems().Count);
                Assert.IsFalse(ServerContext.GetService<RocketLaunchHistory>().HasLaunchedOnce);
            });
            pool.Restore(new SpaceElevatorCablePoolSaveData { CableCount = int.MaxValue - 50 });
            processor.Update();
            Assert.AreEqual(RocketLaunchPadPhase.Launching, RocketLaunchPadTestUtil.ReadState(processor).Phase);
            Assert.IsEmpty(processor.GetRefundItems());
            Assert.AreEqual(0, RocketLaunchPadSaveTestUtil.Capture(pad).CargoPayload.Count);
            Assert.IsTrue(ServerContext.GetService<RocketLaunchHistory>().HasLaunchedOnce);
            processor.Update();
            Assert.AreEqual(1, arrivalCount);
            Assert.AreEqual(int.MaxValue, pool.Amount);
            Debug.Log($"W7 accepted phase={RocketLaunchPadTestUtil.ReadState(processor).Phase} arrivals={arrivalCount} pool={pool.Amount}");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RemovalReturnsOnlyCargoNotYetAccepted(bool reject)
        {
            var (packet, services) = SpaceElevatorTestUtil.CreateServer();
            var pad = Prepare();
            var processor = pad.GetComponent<RocketLaunchPadProcessorComponent>();
            if (reject)
            {
                ServerContext.GetService<SpaceElevatorCablePool>().Restore(new SpaceElevatorCablePoolSaveData { CableCount = int.MaxValue });
                LogAssert.Expect(LogType.Error, new Regex("ケーブルプールの加算が整数範囲"));
                Assert.Throws<InvalidOperationException>(() => processor.Update());
            }
            else processor.Update();

            // 実撤去で未受理の積荷と資材を返す
            // Exercise real removal and refund only unaccepted materials and cables
            var inventory = services.GetRequiredService<IPlayerInventoryDataStore>().GetInventoryData(3).MainOpenableInventory;
            for (var i = 0; i < inventory.GetSlotSize(); i++) inventory.SetItem(i, ServerContext.ItemStackFactory.CreatEmpty());
            packet.GetPacketResponse(MessagePackSerializer.Serialize(new RemoveBlockProtocol.RemoveBlockProtocolMessagePack(Vector3Int.zero)),
                Tests.Util.PlayerIdentity.BoundPacketContext.Bind(3));
            Assert.IsFalse(ServerContext.WorldBlockDatastore.Exists(Vector3Int.zero));
            Assert.AreEqual(reject ? 50 : 0, Count(ForUnitTestItemId.TestElevatorCable));
            Assert.AreEqual(reject ? 2 : 0, Count(ForUnitTestItemId.RocketCaseId));
            Assert.AreEqual(reject ? 3 : 0, Count(ForUnitTestItemId.RocketFuelId));
            Assert.AreEqual(reject ? 1 : 0, Count(ForUnitTestItemId.RocketComputerId));

            #region Internal
            int Count(ItemId id) => inventory.InventoryItems.Where(i => i.Id == id).Sum(i => i.Count);
            #endregion
        }

        [Test]
        public void InventoryInvalidatedDuringArrivalSurvivesUntilNextAdvance()
        {
            SpaceElevatorTestUtil.CreateServer();
            var pad = Prepare();
            var processor = pad.GetComponent<RocketLaunchPadProcessorComponent>();
            var inventory = pad.GetComponent<IOpenableBlockInventoryComponent>();
            using var sub = ServerContext.GetService<IOrbitalArrivalService>().OnOrbitalArrival.Subscribe(_ =>
            {
                Assert.AreEqual(RocketLaunchPadPhase.ReadyToLaunch, RocketLaunchPadTestUtil.ReadState(processor).Phase);
                inventory.InsertItem(ForUnitTestItemId.RocketFuelId, 1);
            });
            processor.Update();

            // 通知中に増えた世代の保持を検証
            // Observe that normal completion did not consume an inventory change made during notification
            var sequence = Field(processor, "_sequence");
            Assert.AreNotEqual(Field(sequence, "_waitGeneration"), Field(sequence, "_processedWaitGeneration"));
            var nextGeneration = Field(sequence, "_waitGeneration");
            processor.Update();
            Assert.AreEqual(nextGeneration, Field(sequence, "_processedWaitGeneration"));

            #region Internal
            object Field(object target, string name) => target.GetType().GetField(name,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(target);
            #endregion
        }

        private static IBlock Prepare()
        {
            var pad = RocketLaunchPadTestUtil.PlacePoweredPad(Vector3Int.zero, 100);
            RocketLaunchPadTestUtil.FillOneLaunch(pad, ForUnitTestItemId.TestElevatorCable, 50);
            var processor = pad.GetComponent<RocketLaunchPadProcessorComponent>();
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.AwaitingBuildCommand, 5));
            Assert.AreEqual(RocketLaunchPadCommandResult.Accepted, processor.RequestBuild());
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.AwaitingLaunchCommand, 40));
            Assert.AreEqual(RocketLaunchPadCommandResult.Accepted, processor.RequestLaunch());
            return pad;
        }
    }
}
