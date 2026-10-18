using Game.Block.Interface.State;
using System;
using System.Collections.Generic;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Network.API;
using Client.WebUiHost.Game.Topics;
using Core.Item.Interface;
using Core.Master;
using Game.Fluid;
using Game.PlayerInventory.Interface.Subscription;
using Game.Train.Unit;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;

namespace Client.Tests.WebUi.BlockDetail
{
    public class TrainInventoryFluidDtoTest
    {
        [Test]
        public void FluidInventoryResponseReachesWebDto()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var guid = Guid.Parse("00000000-0000-0000-1234-000000000001");
            var water = MasterHolder.FluidMaster.GetFluidId(guid);
            var source = new TrainSubInventorySource(new TrainCarInstanceId(1), null);
            var response = new InventoryResponse(source.InventoryIdentifier, new List<IItemStack>(), new List<FluidMessagePack> { new(water, 300, 1000) }, InventoryRequestResult.Success);

            // 応答からモデル、表示DTOまで通す
            // Run the response through the model into the display DTO
            var model = source.CreateModel(response);
            Assert.AreEqual(1, model.Fluids.Count);
            Assert.IsNull(source.LastOpenMessage);
            var dto = TrainInventoryDtoFactory.Create(source, model, new TrainUnitClientCache(null), null, null, null);
            Assert.AreEqual(0, dto.ItemSlots.Count);
            Assert.AreEqual(1, dto.FluidSlots.Count);
            Assert.AreEqual(300, dto.FluidSlots[0].Amount);
            Assert.AreEqual(1000, dto.FluidSlots[0].Capacity);
            Assert.AreEqual(guid.ToString("D"), dto.FluidSlots[0].FluidGuid);
        }
    }
}
