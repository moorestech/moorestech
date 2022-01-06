using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core.Block.BlockFactory;
using Core.Block.BlockInventory;
using Core.Block.Blocks.BeltConveyor;
using Core.Block.Blocks.Machine;
using Core.Block.Blocks.Machine.Inventory;
using Core.Block.Blocks.Machine.InventoryController;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using World.Util;

namespace Test.UnitTest.Game
{
    public class BlockPlaceToConnectionBlockTest
    {
        const int MachineId = 1;
        const int BeltConveyorId = 3;

        /// <summary>
        /// 機械にベルトコンベアが自動でつながるかをテストする
        /// 機械にアイテムを入れる向きでベルトコンベアのテストを行う
        /// </summary>
        [Test]
        public void BeltConveyorConnectMachineTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            var world = serviceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = serviceProvider.GetService<BlockFactory>();


            //北向きにベルトコンベアを設置した時、機械とつながるかをテスト
            var (blockIntId, connectorIntId) = BlockPlaceToGetMachineIdAndConnectorId(
                10, 0,
                9, 0, BlockDirection.North, blockFactory, world);
            Assert.AreEqual(blockIntId, connectorIntId);

            //東向きにベルトコンベアを設置した時、機械とつながるかをテスト
            (blockIntId, connectorIntId) = BlockPlaceToGetMachineIdAndConnectorId(
                0, 10,
                0, 9, BlockDirection.East, blockFactory, world);
            Assert.AreEqual(blockIntId, connectorIntId);

            //南向きにベルトコンベアを設置した時、機械とつながるかをテスト
            (blockIntId, connectorIntId) = BlockPlaceToGetMachineIdAndConnectorId(
                -10, 0,
                -9, 0, BlockDirection.South, blockFactory, world);
            Assert.AreEqual(blockIntId, connectorIntId);

            //西向きにベルトコンベアを設置した時、機械とつながるかをテスト
            (blockIntId, connectorIntId) = BlockPlaceToGetMachineIdAndConnectorId(
                0, -10,
                0, -9, BlockDirection.West, blockFactory, world);
            Assert.AreEqual(blockIntId, connectorIntId);
        }

        private (int, int) BlockPlaceToGetMachineIdAndConnectorId(int machineX, int machineY, int conveyorX,
            int conveyorY, BlockDirection direction, BlockFactory blockFactory, IWorldBlockDatastore world)
        {
            //機械の設置
            var vanillaMachine = blockFactory.Create(MachineId, IntId.NewIntId());
            world.AddBlock(vanillaMachine, machineX, machineY, BlockDirection.North);

            //ベルトコンベアの設置
            var beltConveyor = (VanillaBeltConveyor) blockFactory.Create(BeltConveyorId, IntId.NewIntId());
            world.AddBlock(beltConveyor, conveyorX, conveyorY, direction);

            //繋がっているコネクターを取得
            var _connector = (VanillaMachine) typeof(VanillaBeltConveyor)
                .GetField("_connector", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(beltConveyor);

            //それぞれのintIdを返却
            return (vanillaMachine.GetIntId(), _connector.GetIntId());
        }

        /// <summary>
        /// ベルトコンベアに機械が自動でつながるかをテストする
        /// 機械をあらかじめ設置しておき、後に機械からアイテムが出る方向でベルトコンベアをおく
        /// ブロックが削除されたらつながる機械が消えるので、それをテストする
        /// </summary>
        [Test]
        public void MachineConnectToBeltConveyorTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            var world = serviceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = serviceProvider.GetService<BlockFactory>();

            //機械の設置
            var vanillaMachine = (VanillaMachine) blockFactory.Create(MachineId, IntId.NewIntId());
            world.AddBlock(vanillaMachine, 0, 0, BlockDirection.North);

            //機械から4方向にベルトコンベアが出るように配置
            var beltConveyors = new List<VanillaBeltConveyor>
            {
                (VanillaBeltConveyor) blockFactory.Create(BeltConveyorId, IntId.NewIntId()),
                (VanillaBeltConveyor) blockFactory.Create(BeltConveyorId, IntId.NewIntId()),
                (VanillaBeltConveyor) blockFactory.Create(BeltConveyorId, IntId.NewIntId()),
                (VanillaBeltConveyor) blockFactory.Create(BeltConveyorId, IntId.NewIntId()),
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
                connectInventory.Select(item => ((VanillaBeltConveyor) item).GetIntId()).ToList();
            foreach (var beltConveyor in beltConveyors)
            {
                Assert.True(_connectInventoryItem.Contains(beltConveyor.GetIntId()));
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
    }
}