using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core.Block.BlockFactory;
using Core.Block.BlockInventory;
using Core.Block.Blocks.BeltConveyor;
using Core.Block.Blocks.Chest;
using Core.Block.Blocks.Machine;
using Core.Block.Blocks.Machine.Inventory;
using Core.Block.Blocks.Machine.InventoryController;
using Game.World.EventHandler;
using Game.World.Interface.DataStore;
using Game.World.Interface.Util;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using Server.Boot;

using Test.Module.TestConfig;
using Test.Module.TestMod;

namespace Test.UnitTest.Game
{
    public class BlockPlaceToConnectionBlockTest
    {
        const int MachineId = 1;
        const int BeltConveyorId = 3;
        private const int ChestId = 7;

        /// <summary>
        /// 機械にベルトコンベアが自動でつながるかをテストする
        /// 機械にアイテムを入れる向きでベルトコンベアのテストを行う
        /// </summary>
        [Test]
        public void BeltConveyorConnectMachineTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var world = serviceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = serviceProvider.GetService<BlockFactory>();


            //北向きにベルトコンベアを設置した時、機械とつながるかをテスト
            var (blockEntityId, connectorEntityId) = BlockPlaceToGetMachineIdAndConnectorId(
                0, 10,
                0, 9, BlockDirection.North, blockFactory, world);
            Assert.AreEqual(blockEntityId, connectorEntityId);

            //東向きにベルトコンベアを設置した時、機械とつながるかをテスト
            (blockEntityId, connectorEntityId) = BlockPlaceToGetMachineIdAndConnectorId(
                10, 0,
                9, 0, BlockDirection.East, blockFactory, world);
            Assert.AreEqual(blockEntityId, connectorEntityId);

            //南向きにベルトコンベアを設置した時、機械とつながるかをテスト
            (blockEntityId, connectorEntityId) = BlockPlaceToGetMachineIdAndConnectorId(
                0, -10,
                0, -9, BlockDirection.South, blockFactory, world);
            Assert.AreEqual(blockEntityId, connectorEntityId);

            //西向きにベルトコンベアを設置した時、機械とつながるかをテスト
            (blockEntityId, connectorEntityId) = BlockPlaceToGetMachineIdAndConnectorId(
                -10, 0,
                -9, 0, BlockDirection.West, blockFactory, world);
            Assert.AreEqual(blockEntityId, connectorEntityId);
        }

        private (int, int) BlockPlaceToGetMachineIdAndConnectorId(int machineX, int machineY, int conveyorX,
            int conveyorY, BlockDirection direction, BlockFactory blockFactory, IWorldBlockDatastore world)
        {
            //機械の設置
            var vanillaMachine = blockFactory.Create(MachineId, EntityId.NewEntityId());
            world.AddBlock(vanillaMachine, machineX, machineY, BlockDirection.North);

            //ベルトコンベアの設置
            var beltConveyor = (VanillaBeltConveyor) blockFactory.Create(BeltConveyorId, EntityId.NewEntityId());
            world.AddBlock(beltConveyor, conveyorX, conveyorY, direction);

            //繋がっているコネクターを取得
            var _connector = (VanillaMachine) typeof(VanillaBeltConveyor)
                .GetField("_connector", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(beltConveyor);

            //それぞれのentityIdを返却
            return (vanillaMachine.EntityId, _connector.EntityId);
        }

        /// <summary>
        /// 機械がベルトコンベアに自動でつながるかをテストする
        /// 機械をあらかじめ設置しておき、後に機械からアイテムが出る方向でベルトコンベアをおく
        /// ブロックが削除されたらつながるベルトコンベアが消えるので、それをテストする
        /// </summary>
        [Test]
        public void MachineConnectToBeltConveyorTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var world = serviceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = serviceProvider.GetService<BlockFactory>();

            //機械の設置
            var vanillaMachine = (VanillaMachine) blockFactory.Create(MachineId, EntityId.NewEntityId());
            world.AddBlock(vanillaMachine, 0, 0, BlockDirection.North);

            //機械から4方向にベルトコンベアが出るように配置
            var beltConveyors = new List<VanillaBeltConveyor>
            {
                (VanillaBeltConveyor) blockFactory.Create(BeltConveyorId, EntityId.NewEntityId()),
                (VanillaBeltConveyor) blockFactory.Create(BeltConveyorId, EntityId.NewEntityId()),
                (VanillaBeltConveyor) blockFactory.Create(BeltConveyorId, EntityId.NewEntityId()),
                (VanillaBeltConveyor) blockFactory.Create(BeltConveyorId, EntityId.NewEntityId()),
            };
            world.AddBlock(beltConveyors[0], 1, 0, BlockDirection.North);
            world.AddBlock(beltConveyors[1], 0, 1, BlockDirection.East);
            world.AddBlock(beltConveyors[2], -1, 0, BlockDirection.South);
            world.AddBlock(beltConveyors[3], 0, -1, BlockDirection.West);

            //繋がっているコネクターを取得

            var machineInventory = (VanillaMachineInventory) typeof(VanillaMachine)
                .GetField("_vanillaMachineInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(vanillaMachine);
            var vanillaMachineOutputInventory = (VanillaMachineOutputInventory) typeof(VanillaMachineInventory)
                .GetField("_vanillaMachineOutputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(machineInventory);
            var connectInventory = (List<IBlockInventory>) typeof(VanillaMachineOutputInventory)
                .GetField("_connectInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(vanillaMachineOutputInventory);

            Assert.AreEqual(4, connectInventory.Count);

            //繋がっているコネクターの中身を確認
            var _connectInventoryItem =
                connectInventory.Select(item => ((VanillaBeltConveyor) item).EntityId).ToList();
            foreach (var beltConveyor in beltConveyors)
            {
                Assert.True(_connectInventoryItem.Contains(beltConveyor.EntityId));
            }

            //ベルトコンベアを削除する
            world.RemoveBlock(1, 0);
            world.RemoveBlock(-1, 0);
            //接続しているコネクターが消えているか確認
            Assert.AreEqual(2, connectInventory.Count);
            world.RemoveBlock(0, 1);
            world.RemoveBlock(0, -1);

            //接続しているコネクターが消えているか確認
            Assert.AreEqual(0, connectInventory.Count);
        }



        /// <summary>
        /// ベルトコンベアを設置した後チェストを設置する
        /// ベルトコンベアのコネクターが正しく設定されているかをチェックする
        /// </summary>
        [Test]
        public void BeltConveyorToChestConnectTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var world = serviceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = serviceProvider.GetService<BlockFactory>();
            
            //チェストの設置
            var vanillaChest = (VanillaChest) blockFactory.Create(ChestId, EntityId.NewEntityId());
            world.AddBlock(vanillaChest, 0, 0, BlockDirection.North);
            
            
            //北向きにベルトコンベアを設置してチェック
            BeltConveyorPlaceAndCheckConnector(0,-1,BlockDirection.North,vanillaChest,blockFactory,world);
            
            //東向きにベルトコンベアを設置してチェック
            BeltConveyorPlaceAndCheckConnector(-1,0,BlockDirection.East,vanillaChest,blockFactory,world);
            
            //南向きにベルトコンベアを設置してチェック
            BeltConveyorPlaceAndCheckConnector(0,1,BlockDirection.South,vanillaChest,blockFactory,world);
            
            //西向きにベルトコンベアを設置してチェック
            BeltConveyorPlaceAndCheckConnector(1,0,BlockDirection.West,vanillaChest,blockFactory,world);
        }

        private void BeltConveyorPlaceAndCheckConnector(int beltConveyorX,int beltConveyorY,BlockDirection direction,VanillaChest targetChest,BlockFactory blockFactory,IWorldBlockDatastore world)
        {
            var northBeltConveyor = (VanillaBeltConveyor) blockFactory.Create(BeltConveyorId, EntityId.NewEntityId());
            world.AddBlock(northBeltConveyor, beltConveyorX, beltConveyorY, direction);
            var connector = (VanillaChest) typeof(VanillaBeltConveyor)
                .GetField("_connector", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(northBeltConveyor);
            
            Assert.AreEqual(targetChest.EntityId,connector.EntityId);
        }
        
        
    }
}