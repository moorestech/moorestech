using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using MessagePack;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using System;
using Server.Protocol;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class RequestBlockInventoryTest : RequestBlockInventoryTestBase
    {

        //通常の機械のテスト
        [Test]
        public void MachineInventoryRequest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;


            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(5, 10), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machineBlock);
            var machineComponent = machineBlock.GetComponent<VanillaMachineBlockInventoryComponent>();

            // 束縛(ADR 0042)のためレシピを選択し、スロット位置とIDをレシピ自身から取る
            // Binding (ADR 0042) requires a selected recipe; slot positions and ids come from the recipe itself
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];
            MachineRecipeSelectTestUtil.SelectRecipe(machineBlock, recipe);
            var input0 = MasterHolder.ItemMaster.GetItemId(recipe.InputItems[0].ItemGuid);
            var output0 = MasterHolder.ItemMaster.GetItemId(recipe.OutputItems[0].ItemGuid);
            machineComponent.SetItem(0, itemStackFactory.Create(input0, 2));
            machineComponent.SetItem(recipe.InputItems.Length, itemStackFactory.Create(output0, 5));

            //レスポンスの取得
            var data = MessagePackSerializer.Deserialize<InventoryRequestProtocol.ResponseInventoryRequestProtocolMessagePack>(packet.GetPacketResponse(RequestBlock(new Vector3Int(5, 10)), Tests.Util.BoundPacketContext.Bind(1))[0]);

            Assert.AreEqual(InputSlotNum + OutPutSlotNum + ModuleSlotNum, data.Items.Length); // slot num


            Assert.AreEqual(input0.AsPrimitive(), data.Items[0].Id.AsPrimitive()); // item id
            Assert.AreEqual(2, data.Items[0].Count); // item count

            Assert.AreEqual(0, data.Items[1].Id.AsPrimitive());
            Assert.AreEqual(0, data.Items[1].Count);

            Assert.AreEqual(output0.AsPrimitive(), data.Items[recipe.InputItems.Length].Id.AsPrimitive());
            Assert.AreEqual(5, data.Items[recipe.InputItems.Length].Count);
        }

        [Test]
        public void TrainItemPlatformInventoryRequestReturnsConfiguredEmptySlots()
        {
            // 空の貨物PFでもUI用にマスタ定義分の空スロットを返す
            // Return configured empty slots for UI even when the cargo platform has no container
            var environment = TrainTestHelper.CreateEnvironment();
            var position = new Vector3Int(10, 20, 0);
            TrainTestHelper.PlaceBlock(environment, ForUnitTestModBlockId.TestTrainItemPlatform, position, BlockDirection.North);

            var responseBytes = environment.PacketResponseCreator.GetPacketResponse(RequestBlock(position), Tests.Util.BoundPacketContext.Bind(1))[0];
            var data = MessagePackSerializer.Deserialize<InventoryRequestProtocol.ResponseInventoryRequestProtocolMessagePack>(responseBytes);
            var param = (TrainItemPlatformBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestTrainItemPlatform).BlockParam;

            Assert.AreEqual(InventoryType.Block, data.InventoryType);
            Assert.AreEqual(InventoryRequestResult.Success, data.Result);
            Assert.AreEqual(param.ItemSlotCount, data.Items.Length);
            Assert.IsTrue(data.Items.All(item => item.Id.AsPrimitive() == 0 && item.Count == 0));
        }

        [Test]
        public void TrainStationInventoryRequestReturnsConfiguredEmptySlots()
        {
            // 空の駅でもUI用にマスタ定義分の空スロットを返す
            // Return configured empty slots for UI even when the station has no container
            var environment = TrainTestHelper.CreateEnvironment();
            var position = new Vector3Int(30, 20, 0);
            TrainTestHelper.PlaceBlock(environment, ForUnitTestModBlockId.TestTrainStation, position, BlockDirection.North);

            var responseBytes = environment.PacketResponseCreator.GetPacketResponse(RequestBlock(position), Tests.Util.BoundPacketContext.Bind(1))[0];
            var data = MessagePackSerializer.Deserialize<InventoryRequestProtocol.ResponseInventoryRequestProtocolMessagePack>(responseBytes);
            var param = (TrainStationBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestTrainStation).BlockParam;

            Assert.AreEqual(InventoryType.Block, data.InventoryType);
            Assert.AreEqual(InventoryRequestResult.Success, data.Result);
            Assert.AreEqual(param.ItemSlotCount, data.Items.Length);
            Assert.IsTrue(data.Items.All(item => item.Id.AsPrimitive() == 0 && item.Count == 0));
        }

        [Test]
        public void BlockInventoryRequestWithoutBlockReturnsBlockNotFound()
        {
            // 存在しないブロックへの要求は成功扱いにしない
            // Requests for missing blocks must not be reported as success
            var environment = TrainTestHelper.CreateEnvironment();
            var position = new Vector3Int(999, 20, 0);

            var responseBytes = environment.PacketResponseCreator.GetPacketResponse(RequestBlock(position), Tests.Util.BoundPacketContext.Bind(1))[0];
            var data = MessagePackSerializer.Deserialize<InventoryRequestProtocol.ResponseInventoryRequestProtocolMessagePack>(responseBytes);

            Assert.AreEqual(InventoryType.Block, data.InventoryType);
            Assert.AreEqual(InventoryRequestResult.BlockNotFound, data.Result);
            Assert.AreEqual(0, data.Items.Length);
        }

        [Test]
        public void BlockInventoryRequestWithoutOpenableInventoryReturnsContainerNotFound()
        {
            // インベントリを持たないブロックへの要求は成功扱いにしない
            // Requests for blocks without openable inventory must not be reported as success
            var environment = TrainTestHelper.CreateEnvironment();
            var position = new Vector3Int(40, 20, 0);
            TrainTestHelper.PlaceBlock(environment, ForUnitTestModBlockId.TestTrainRail, position, BlockDirection.North);

            var responseBytes = environment.PacketResponseCreator.GetPacketResponse(RequestBlock(position), Tests.Util.BoundPacketContext.Bind(1))[0];
            var data = MessagePackSerializer.Deserialize<InventoryRequestProtocol.ResponseInventoryRequestProtocolMessagePack>(responseBytes);

            Assert.AreEqual(InventoryType.Block, data.InventoryType);
            Assert.AreEqual(InventoryRequestResult.ContainerNotFound, data.Result);
            Assert.AreEqual(0, data.Items.Length);
        }
    }
}
