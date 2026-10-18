using Game.Block.Interface.Extension;
using Tests.CombinedTest.Server.PacketTest.Event;
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.TrainRail;
using Game.Block.Blocks.TrainRail.ContainerComponents;
using Game.Block.Interface;
using Game.Context;
using Game.Fluid;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using MessagePack;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Event.EventReceive.UnifiedInventoryEvent;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class TrainFluidRefreshEventTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void TransferAndDetachingRefreshOnlySubscribedPlayer(bool unload)
        {
            var env = TrainTestHelper.CreateEnvironment();
            var (block, rails) = TrainTestHelper.PlaceBlockWithRailComponents(env, ForUnitTestModBlockId.TestTrainFluidPlatform, Vector3Int.zero, BlockDirection.North);
            var platform = block.GetComponent<TrainPlatformFluidContainerComponent>();
            var docking = block.GetComponent<TrainPlatformDockingComponent>();
            var water = MasterHolder.FluidMaster.GetFluidId(Guid.Parse("00000000-0000-0000-1234-000000000001"));
            platform.AddLiquid(new FluidStack(500, water), default);
            var length = block.BlockPositionInfo.BlockSize.z;
            var position = new RailPosition(new List<IRailNode> { rails[1].FrontNode, rails[0].FrontNode }, length, 0);
            var (car, fluid) = TrainTestCarFactory.CreateTrainCarWithFluidContainer(0, 400000, 1000, length, true);
            fluid.Container.AddLiquid(new FluidStack(200, water));
            if (unload) block.GetComponent<TrainPlatformTransferComponent>().SetMode(TrainPlatformTransferComponent.TransferMode.UnloadToPlatform);
            var train = new TrainUnit(position, new List<TrainCar> { car }, env.GetTrainRailPositionManager(), env.GetTrainDiagramManager());
            env.GetITrainUnitMutationDatastore().RegisterTrain(train);

            // 加入者と非加入者の配信を分けて捕捉する
            // Capture subscribed and unsubscribed players separately
            var subscribed = EventTestUtil.RegisterCaptureSink(env.ServiceProvider, 1);
            var other = EventTestUtil.RegisterCaptureSink(env.ServiceProvider, 2);
            var identifier = InventoryIdentifierMessagePack.CreateTrainMessage(car.TrainCarInstanceId.AsPrimitive());
            var request = new SubscribeInventoryProtocol.SubscribeInventoryRequestMessagePack(identifier, true);
            env.PacketResponseCreator.GetPacketResponse(MessagePackSerializer.Serialize(request), Tests.Util.PlayerIdentity.BoundPacketContext.Bind(1));
            subscribed.TakeAll();
            other.TakeAll();
            train.trainUnitStationDocking.TryDockWhenStopped();
            var param = (TrainFluidPlatformBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestTrainFluidPlatform).BlockParam;
            var ticks = (int)GameUpdater.SecondsToTicks(param.LoadingAnimeSpeed) + 1;
            for (var i = 0; i < ticks; i++)
            {
                docking.Update();
                platform.Update();
            }

            // 実際に転送した後のRefreshと宛先を確認する
            // Verify refresh and recipient after an actual transfer
            Assert.AreEqual(unload ? 0 : 700, fluid.Container.Amount, 0.001);
            AssertRefresh(subscribed.TakeAll().Where(e => e.Tag == UnifiedInventoryEventPacket.EventTag).Select(e => e.Payload));
            Assert.IsFalse(other.TakeAll().Any(e => e.Tag == UnifiedInventoryEventPacket.EventTag));
            docking.Update();
            platform.Update();
            Assert.IsFalse(subscribed.TakeAll().Any(e => e.Tag == UnifiedInventoryEventPacket.EventTag));
            car.SetContainer(null);
            AssertRefresh(subscribed.TakeAll().Where(e => e.Tag == UnifiedInventoryEventPacket.EventTag).Select(e => e.Payload));
            car.SetContainer(fluid);
            AssertRefresh(subscribed.TakeAll().Where(e => e.Tag == UnifiedInventoryEventPacket.EventTag).Select(e => e.Payload));

            #region Internal

            void AssertRefresh(IEnumerable<byte[]> payloads)
            {
                var events = payloads.Select(p => MessagePackSerializer.Deserialize<UnifiedInventoryEventMessagePack>(p)).ToList();
                Assert.IsTrue(events.Any(e => e.EventType == InventoryEventType.Refresh && e.Identifier.TrainCarInstanceId == identifier.TrainCarInstanceId));
            }

            #endregion
        }
    }
}
