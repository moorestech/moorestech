#if NET6_0
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Block.BlockInventory;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.Chest;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.InventoryController;
using Game.Block.Interface;
using Game.World.Interface.DataStore;
using Game.World.Interface.Util;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Test.Module.TestMod;

namespace Test.UnitTest.Game
{
    /// <summary>
    ///     
    /// </summary>
    public class BlockPlaceToConnectionBlockTest
    {
        private const int MachineId = UnitTestModBlockId.MachineId;
        private const int BeltConveyorId = UnitTestModBlockId.BeltConveyorId;
        private const int ChestId = UnitTestModBlockId.ChestId;


        ///     
        ///     

        [Test]
        public void BeltConveyorConnectMachineTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var world = serviceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = serviceProvider.GetService<IBlockFactory>();


            
            var (blockEntityId, connectorEntityId) = BlockPlaceToGetMachineIdAndConnectorId(
                0, 10,
                0, 9, BlockDirection.North, blockFactory, world);
            Assert.AreEqual(blockEntityId, connectorEntityId);

            
            (blockEntityId, connectorEntityId) = BlockPlaceToGetMachineIdAndConnectorId(
                10, 0,
                9, 0, BlockDirection.East, blockFactory, world);
            Assert.AreEqual(blockEntityId, connectorEntityId);

            
            (blockEntityId, connectorEntityId) = BlockPlaceToGetMachineIdAndConnectorId(
                0, -10,
                0, -9, BlockDirection.South, blockFactory, world);
            Assert.AreEqual(blockEntityId, connectorEntityId);

            
            (blockEntityId, connectorEntityId) = BlockPlaceToGetMachineIdAndConnectorId(
                -10, 0,
                -9, 0, BlockDirection.West, blockFactory, world);
            Assert.AreEqual(blockEntityId, connectorEntityId);
        }

        private (int, int) BlockPlaceToGetMachineIdAndConnectorId(int machineX, int machineY, int conveyorX,
            int conveyorY, BlockDirection direction, IBlockFactory blockFactory, IWorldBlockDatastore world)
        {
            
            var vanillaMachine = blockFactory.Create(MachineId, CreateBlockEntityId.Create());
            world.AddBlock(vanillaMachine, machineX, machineY, BlockDirection.North);

            
            var beltConveyor = (VanillaBeltConveyor)blockFactory.Create(BeltConveyorId, CreateBlockEntityId.Create());
            world.AddBlock(beltConveyor, conveyorX, conveyorY, direction);

            
            var _connector = (VanillaMachineBase)typeof(VanillaBeltConveyor)
                .GetField("_connector", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(beltConveyor);

            //entityId
            return (vanillaMachine.EntityId, _connector.EntityId);
        }


        ///     
        ///     
        ///     

        [Test]
        public void MachineConnectToBeltConveyorTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var world = serviceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = serviceProvider.GetService<IBlockFactory>();

            
            var vanillaMachine = (VanillaMachineBase)blockFactory.Create(MachineId, CreateBlockEntityId.Create());
            world.AddBlock(vanillaMachine, 0, 0, BlockDirection.North);

            //4
            var beltConveyors = new List<VanillaBeltConveyor>
            {
                (VanillaBeltConveyor)blockFactory.Create(BeltConveyorId, CreateBlockEntityId.Create()),
                (VanillaBeltConveyor)blockFactory.Create(BeltConveyorId, CreateBlockEntityId.Create()),
                (VanillaBeltConveyor)blockFactory.Create(BeltConveyorId, CreateBlockEntityId.Create()),
                (VanillaBeltConveyor)blockFactory.Create(BeltConveyorId, CreateBlockEntityId.Create())
            };
            world.AddBlock(beltConveyors[0], 1, 0, BlockDirection.North);
            world.AddBlock(beltConveyors[1], 0, 1, BlockDirection.East);
            world.AddBlock(beltConveyors[2], -1, 0, BlockDirection.South);
            world.AddBlock(beltConveyors[3], 0, -1, BlockDirection.West);

            

            var machineInventory = (VanillaMachineBlockInventory)typeof(VanillaMachineBase)
                .GetField("_vanillaMachineBlockInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(vanillaMachine);
            var vanillaMachineOutputInventory = (VanillaMachineOutputInventory)typeof(VanillaMachineBlockInventory)
                .GetField("_vanillaMachineOutputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(machineInventory);
            var connectInventory = (List<IBlockInventory>)typeof(VanillaMachineOutputInventory)
                .GetField("_connectInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(vanillaMachineOutputInventory);

            Assert.AreEqual(4, connectInventory.Count);

            
            var _connectInventoryItem =
                connectInventory.Select(item => ((VanillaBeltConveyor)item).EntityId).ToList();
            foreach (var beltConveyor in beltConveyors) Assert.True(_connectInventoryItem.Contains(beltConveyor.EntityId));

            
            world.RemoveBlock(1, 0);
            world.RemoveBlock(-1, 0);
            
            Assert.AreEqual(2, connectInventory.Count);
            world.RemoveBlock(0, 1);
            world.RemoveBlock(0, -1);

            
            Assert.AreEqual(0, connectInventory.Count);
        }



        ///     
        ///     

        [Test]
        public void BeltConveyorToChestConnectTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var world = serviceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = serviceProvider.GetService<IBlockFactory>();

            
            var vanillaChest = (VanillaChest)blockFactory.Create(ChestId, CreateBlockEntityId.Create());
            world.AddBlock(vanillaChest, 0, 0, BlockDirection.North);


            
            BeltConveyorPlaceAndCheckConnector(0, -1, BlockDirection.North, vanillaChest, blockFactory, world);

            
            BeltConveyorPlaceAndCheckConnector(-1, 0, BlockDirection.East, vanillaChest, blockFactory, world);

            
            BeltConveyorPlaceAndCheckConnector(0, 1, BlockDirection.South, vanillaChest, blockFactory, world);

            
            BeltConveyorPlaceAndCheckConnector(1, 0, BlockDirection.West, vanillaChest, blockFactory, world);
        }

        private void BeltConveyorPlaceAndCheckConnector(int beltConveyorX, int beltConveyorY, BlockDirection direction, VanillaChest targetChest, IBlockFactory blockFactory, IWorldBlockDatastore world)
        {
            var northBeltConveyor = (VanillaBeltConveyor)blockFactory.Create(BeltConveyorId, CreateBlockEntityId.Create());
            world.AddBlock(northBeltConveyor, beltConveyorX, beltConveyorY, direction);
            var connector = (VanillaChest)typeof(VanillaBeltConveyor)
                .GetField("_connector", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(northBeltConveyor);

            Assert.AreEqual(targetChest.EntityId, connector.EntityId);
        }


        ///     ()

        [Test]
        public void NotConnectableBlockTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var world = serviceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = serviceProvider.GetService<IBlockFactory>();


            var machine = (VanillaMachineBase)blockFactory.Create(MachineId, CreateBlockEntityId.Create());
            var chest = (VanillaChest)blockFactory.Create(ChestId, CreateBlockEntityId.Create());

            
            world.AddBlock(machine, 0, 0, BlockDirection.North);
            world.AddBlock(chest, 0, 1, BlockDirection.North);

            
            var machineInventory = (VanillaMachineBlockInventory)typeof(VanillaMachineBase)
                .GetField("_vanillaMachineBlockInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(machine);
            var vanillaMachineOutputInventory = (VanillaMachineOutputInventory)typeof(VanillaMachineBlockInventory)
                .GetField("_vanillaMachineOutputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(machineInventory);
            var machineConnectInventory = (List<IBlockInventory>)typeof(VanillaMachineOutputInventory)
                .GetField("_connectInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(vanillaMachineOutputInventory);

            
            Assert.AreEqual(0, machineConnectInventory.Count);

            
            var chestConnectInventory = (List<IBlockInventory>)typeof(VanillaChest)
                .GetField("_connectInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(chest);

            
            Assert.AreEqual(0, chestConnectInventory.Count);
        }
    }
}
#endif