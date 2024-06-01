using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.Chest;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.InventoryController;
using Game.Block.Component.IOConnector;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Game.World.Interface.DataStore;
using Game.World.Interface.Util;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Game
{
    /// <summary>
    ///     ブロックが設置された時ブロック同士が接続するテスト
    /// </summary>
    public class BlockPlaceToConnectionBlockTest
    {
        private const int MachineId = ForUnitTestModBlockId.MachineId;
        private const int BeltConveyorId = ForUnitTestModBlockId.BeltConveyorId;
        private const int ChestId = ForUnitTestModBlockId.ChestId;

        /// <summary>
        ///     機械にベルトコンベアが自動でつながるかをテストする
        ///     機械にアイテムを入れる向きでベルトコンベアのテストを行う
        /// </summary>
        [Test]
        public void BeltConveyorConnectMachineTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var world = ServerContext.WorldBlockDatastore;
            var blockFactory = ServerContext.BlockFactory;

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

        private (int, int) BlockPlaceToGetMachineIdAndConnectorId(
            int machineX, int machineZ,
            int conveyorX, int conveyorZ,
            BlockDirection direction, IBlockFactory blockFactory, IWorldBlockDatastore world)
        {
            //機械の設置
            var machinePosInfo = new BlockPositionInfo(new Vector3Int(machineX, 0, machineZ), direction, Vector3Int.one);
            var vanillaMachine = blockFactory.Create(MachineId, CreateBlockEntityId.Create(), machinePosInfo);
            world.AddBlock(vanillaMachine);

            //ベルトコンベアの設置
            var beltPosInfo = new BlockPositionInfo(new Vector3Int(conveyorX, 0, conveyorZ), direction, Vector3Int.one);
            var beltConveyor = blockFactory.Create(BeltConveyorId, CreateBlockEntityId.Create(), beltPosInfo);
            world.AddBlock(beltConveyor);

            //繋がっているコネクターを取得
            var connectedMachine = (VanillaElectricMachineComponent)beltConveyor.ComponentManager.GetComponent<BlockConnectorComponent<IBlockInventory>>().ConnectTargets[0];

            //それぞれのentityIdを返却
            return (vanillaMachine.EntityId, connectedMachine.EntityId);
        }

        /// <summary>
        ///     機械がベルトコンベアに自動でつながるかをテストする
        ///     機械をあらかじめ設置しておき、後に機械からアイテムが出る方向でベルトコンベアをおく
        ///     ブロックが削除されたらつながるベルトコンベアが消えるので、それをテストする
        /// </summary>
        [Test]
        public void MachineConnectToBeltConveyorTest()
        {
            var (packet, serviceProvider) =
                new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var world = ServerContext.WorldBlockDatastore;
            var blockFactory = ServerContext.BlockFactory;

            //機械の設置
            var machinePosInfo = new BlockPositionInfo(new Vector3Int(0, 0), BlockDirection.North, Vector3Int.one);
            var vanillaMachine = blockFactory.Create(MachineId, CreateBlockEntityId.Create(), machinePosInfo);
            world.AddBlock(vanillaMachine);

            //機械から4方向にベルトコンベアが出るように配置
            var beltConveyors = new List<IBlock>
            {
                blockFactory.Create(BeltConveyorId, CreateBlockEntityId.Create(), new BlockPositionInfo(new Vector3Int(1, 0, 0), BlockDirection.North, Vector3Int.one)),
                blockFactory.Create(BeltConveyorId, CreateBlockEntityId.Create(), new BlockPositionInfo(new Vector3Int(0, 0, 1), BlockDirection.East, Vector3Int.one)),
                blockFactory.Create(BeltConveyorId, CreateBlockEntityId.Create(), new BlockPositionInfo(new Vector3Int(-1, 0, 0), BlockDirection.South, Vector3Int.one)),
                blockFactory.Create(BeltConveyorId, CreateBlockEntityId.Create(), new BlockPositionInfo(new Vector3Int(0, 0, -1), BlockDirection.West, Vector3Int.one))
            };
            world.AddBlock(beltConveyors[0]);
            world.AddBlock(beltConveyors[1]);
            world.AddBlock(beltConveyors[2]);
            world.AddBlock(beltConveyors[3]);

            //繋がっているコネクターを取得
            var connectInventory = (List<IBlockInventory>)vanillaMachine.ComponentManager.GetComponent<BlockConnectorComponent<IBlockInventory>>().ConnectTargets;

            Assert.AreEqual(4, connectInventory.Count);

            //ベルトコンベアを削除する
            world.RemoveBlock(new Vector3Int(1, 0, 0));
            world.RemoveBlock(new Vector3Int(-1, 0, 0));
            //接続しているコネクターが消えているか確認
            Assert.AreEqual(2, connectInventory.Count);
            world.RemoveBlock(new Vector3Int(0, 0, 1));
            world.RemoveBlock(new Vector3Int(0, 0, -1));

            //接続しているコネクターが消えているか確認
            Assert.AreEqual(0, connectInventory.Count);
        }


        /// <summary>
        ///     ベルトコンベアを設置した後チェストを設置する
        ///     ベルトコンベアのコネクターが正しく設定されているかをチェックする
        /// </summary>
        [Test]
        public void BeltConveyorToChestConnectTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);

            //チェストの設置
            var chestPosInfo = new BlockPositionInfo(new Vector3Int(0, 0), BlockDirection.North, Vector3Int.one);
            var vanillaChest = ServerContext.BlockFactory.Create(ChestId, CreateBlockEntityId.Create(), chestPosInfo);
            ServerContext.WorldBlockDatastore.AddBlock(vanillaChest);


            //北向きにベルトコンベアを設置してチェック
            BeltConveyorPlaceAndCheckConnector(new Vector3Int(0, 0, -1), BlockDirection.North, vanillaChest);

            //東向きにベルトコンベアを設置してチェック
            BeltConveyorPlaceAndCheckConnector(new Vector3Int(-1, 0, 0), BlockDirection.East, vanillaChest);

            //南向きにベルトコンベアを設置してチェック
            BeltConveyorPlaceAndCheckConnector(new Vector3Int(0, 0, 1), BlockDirection.South, vanillaChest);

            //西向きにベルトコンベアを設置してチェック
            BeltConveyorPlaceAndCheckConnector(new Vector3Int(1, 0, 0), BlockDirection.West, vanillaChest);
        }

        private void BeltConveyorPlaceAndCheckConnector(Vector3Int beltConveyorPos, BlockDirection direction, IBlock targetChest)
        {
            var posInfo = new BlockPositionInfo(beltConveyorPos, direction, Vector3Int.one);
            var northBeltConveyor = ServerContext.BlockFactory.Create(BeltConveyorId, CreateBlockEntityId.Create(), posInfo);

            ServerContext.WorldBlockDatastore.AddBlock(northBeltConveyor);

            var connector = (VanillaChestComponent)northBeltConveyor.ComponentManager.GetComponent<BlockConnectorComponent<IBlockInventory>>().ConnectTargets[0];

            Assert.AreEqual(targetChest.EntityId, connector.EntityId);
        }

        /// <summary>
        ///     接続できないブロック(機械とチェスト)同士が接続していないテスト
        /// </summary>
        [Test]
        public void NotConnectableBlockTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var world = ServerContext.WorldBlockDatastore;
            var blockFactory = ServerContext.BlockFactory;

            //機械とチェストを設置
            var machinePosInfo = new BlockPositionInfo(new Vector3Int(0, 0), BlockDirection.North, Vector3Int.one);
            var machine = blockFactory.Create(MachineId, CreateBlockEntityId.Create(), machinePosInfo);
            world.AddBlock(machine);

            var chestPosInfo = new BlockPositionInfo(new Vector3Int(0, 1), BlockDirection.North, Vector3Int.one);
            var chest = blockFactory.Create(ChestId, CreateBlockEntityId.Create(), chestPosInfo);
            world.AddBlock(chest);

            //機械のコネクターを取得
            var machineConnectInventory = (List<IBlockInventory>)machine.ComponentManager.GetComponent<BlockConnectorComponent<IBlockInventory>>().ConnectTargets;

            //接続されていないことをチェック
            Assert.AreEqual(0, machineConnectInventory.Count);

            //チェストのコネクターを取得
            var chestConnectInventory = (List<IBlockInventory>)chest.ComponentManager.GetComponent<BlockConnectorComponent<IBlockInventory>>().ConnectTargets;

            //接続されていないことをチェック
            Assert.AreEqual(0, chestConnectInventory.Count);
        }


        /// <summary>
        /// 大きさが1x1x1以上のブロックで複数のコネクターがある場合、正しく接続されるかをテスト
        /// </summary>
        [Test]
        public void MultiBlockConnectTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var world = ServerContext.WorldBlockDatastore;
            var blockFactory = ServerContext.BlockFactory;
            
            //ベルトコンベアを設置
            
            //接続するベルトコンベア
            var belt1PosInfo = new BlockPositionInfo(new Vector3Int(2, 0, 3), BlockDirection.North, Vector3Int.one);
            var belt1 = blockFactory.Create(BeltConveyorId, CreateBlockEntityId.Create(), belt1PosInfo);
            world.AddBlock(belt1);
            var belt2PosInfo = new BlockPositionInfo(new Vector3Int(2, 0, -1), BlockDirection.South, Vector3Int.one);
            var belt2 = blockFactory.Create(BeltConveyorId, CreateBlockEntityId.Create(), belt2PosInfo);
            world.AddBlock(belt2);
            
            //接続されないベルトコンベア
            var belt3PosInfo = new BlockPositionInfo(new Vector3Int(3, 0, 3), BlockDirection.North, Vector3Int.one);
            var belt3 = blockFactory.Create(BeltConveyorId, CreateBlockEntityId.Create(), belt3PosInfo);
            world.AddBlock(belt3);
            var belt4PosInfo = new BlockPositionInfo(new Vector3Int(1, 0, -1), BlockDirection.South, Vector3Int.one);
            var belt4 = blockFactory.Create(BeltConveyorId, CreateBlockEntityId.Create(), belt4PosInfo);
            world.AddBlock(belt4);

            //マルチブロックを設置
            var machinePosInfo = new BlockPositionInfo(new Vector3Int(0, 0), BlockDirection.North, Vector3Int.one);
            var multiBlock = blockFactory.Create(ForUnitTestModBlockId.MultiBlockGeneratorId, CreateBlockEntityId.Create(), machinePosInfo);
            world.AddBlock(multiBlock);
            
            // マルチブロックのコネクターを取得
            var connector = (List<IBlockInventory>)multiBlock.ComponentManager.GetComponent<BlockConnectorComponent<IBlockInventory>>().ConnectTargets;
            
            // ベルトコンベアが正しく接続されているかをチェック
            Assert.AreEqual(2, connector.Count);
        }
    }
}