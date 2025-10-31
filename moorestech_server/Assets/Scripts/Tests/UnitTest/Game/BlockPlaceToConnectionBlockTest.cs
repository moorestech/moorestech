using System.Collections.Generic;
using System.Linq;
using Game.Block.Blocks.Chest;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.World.Interface.DataStore;
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
        /// <summary>
        ///     機械にベルトコンベアが自動でつながるかをテストする
        ///     機械にアイテムを入れる向きでベルトコンベアのテストを行う
        /// </summary>
        [Test]
        public void BeltConveyorConnectMachineTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var blockFactory = ServerContext.BlockFactory;
            
            //北向きにベルトコンベアを設置した時、機械とつながるかをテスト
            BlockPlaceToGetMachineIdAndConnectorId(
                0, 10,
                0, 9, BlockDirection.North, blockFactory, world);
            
            //東向きにベルトコンベアを設置した時、機械とつながるかをテスト
            BlockPlaceToGetMachineIdAndConnectorId(
                10, 0,
                9, 0, BlockDirection.East, blockFactory, world);
            
            //南向きにベルトコンベアを設置した時、機械とつながるかをテスト
            BlockPlaceToGetMachineIdAndConnectorId(
                0, -10,
                0, -9, BlockDirection.South, blockFactory, world);
            
            //西向きにベルトコンベアを設置した時、機械とつながるかをテスト
            BlockPlaceToGetMachineIdAndConnectorId(
                -10, 0,
                -9, 0, BlockDirection.West, blockFactory, world);
        }
        
        private void BlockPlaceToGetMachineIdAndConnectorId(
            int machineX, int machineZ,
            int conveyorX, int conveyorZ,
            BlockDirection direction, IBlockFactory blockFactory, IWorldBlockDatastore world)
        {
            //機械の設置
            world.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(machineX, 0, machineZ), direction, out var vanillaMachine, System.Array.Empty<BlockCreateParam>());
            
            //ベルトコンベアの設置
            world.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(conveyorX, 0, conveyorZ), direction, out var beltConveyor, System.Array.Empty<BlockCreateParam>());
            
            //繋がっているコネクターを取得
            var connectedMachine = (VanillaMachineBlockInventoryComponent)beltConveyor.GetComponent<BlockConnectorComponent<IBlockInventory>>().ConnectedTargets.First().Key;
            
            //繋がっているかを検証
            var machineInventory = vanillaMachine.GetComponent<VanillaMachineBlockInventoryComponent>();
            
            Assert.IsTrue(connectedMachine == machineInventory);
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
                new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var blockFactory = ServerContext.BlockFactory;
            
            //機械の設置
            world.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(0, 0), BlockDirection.North, out var vanillaMachine, System.Array.Empty<BlockCreateParam>());
            
            //機械から4方向にベルトコンベアが出るように配置
            var beltConveyorTransforms = new List<(Vector3Int, BlockDirection)>
            {
                (new Vector3Int(1, 0, 0), BlockDirection.North),
                (new Vector3Int(0, 0, 1), BlockDirection.East),
                (new Vector3Int(-1, 0, 0), BlockDirection.South),
                (new Vector3Int(0, 0, -1), BlockDirection.West),
            };
            foreach (var (position, direction) in beltConveyorTransforms) world.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, position, direction, out _, System.Array.Empty<BlockCreateParam>());
            
            //繋がっているコネクターを取得
            var connectInventory = (Dictionary<IBlockInventory, ConnectedInfo>)vanillaMachine.GetComponent<BlockConnectorComponent<IBlockInventory>>().ConnectedTargets;
            
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
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            //チェストの設置
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0), BlockDirection.North, out var vanillaChest, System.Array.Empty<BlockCreateParam>());
            
            
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
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, beltConveyorPos, direction, out var northBeltConveyor, System.Array.Empty<BlockCreateParam>());
            
            var connector = (VanillaChestComponent)northBeltConveyor.GetComponent<BlockConnectorComponent<IBlockInventory>>().ConnectedTargets.First().Key;
            
            Assert.AreEqual(targetChest.BlockInstanceId, connector.BlockInstanceId);
        }
        
        /// <summary>
        ///     接続できないブロック(機械とチェスト)同士が接続していないテスト
        /// </summary>
        [Test]
        public void NotConnectableBlockTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            
            //機械とチェストを設置
            world.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(0, 0), BlockDirection.North, out var machine, System.Array.Empty<BlockCreateParam>());
            world.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 1), BlockDirection.North, out var chest, System.Array.Empty<BlockCreateParam>());
            
            //機械のコネクターを取得
            var machineConnectInventory = (Dictionary<IBlockInventory, ConnectedInfo>)machine.GetComponent<BlockConnectorComponent<IBlockInventory>>().ConnectedTargets;
            
            //接続されていないことをチェック
            Assert.AreEqual(0, machineConnectInventory.Count);
            
            //チェストのコネクターを取得
            var chestConnectInventory = (Dictionary<IBlockInventory, ConnectedInfo>)chest.GetComponent<BlockConnectorComponent<IBlockInventory>>().ConnectedTargets;
            
            //接続されていないことをチェック
            Assert.AreEqual(0, chestConnectInventory.Count);
        }
        
        
        /// <summary>
        ///     大きさが1x1x1以上のブロックで複数のコネクターがある場合、正しく接続されるかをテスト
        /// </summary>
        [Test]
        public void MultiBlockConnectTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            
            //ベルトコンベアを設置
            
            //接続するベルトコンベア
            world.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(2, 0, 3), BlockDirection.North, out _, System.Array.Empty<BlockCreateParam>());
            world.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(2, 0, -1), BlockDirection.South, out _, System.Array.Empty<BlockCreateParam>());
            
            //接続されないベルトコンベア
            world.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(3, 0, 3), BlockDirection.North, out _, System.Array.Empty<BlockCreateParam>());
            world.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(1, 0, -1), BlockDirection.South, out _, System.Array.Empty<BlockCreateParam>());
            
            //マルチブロックを設置
            world.TryAddBlock(ForUnitTestModBlockId.MultiBlockGeneratorId, new Vector3Int(0, 0), BlockDirection.North, out var multiBlock, System.Array.Empty<BlockCreateParam>());
            
            // マルチブロックのコネクターを取得
            var connector = (Dictionary<IBlockInventory, ConnectedInfo>)multiBlock.GetComponent<BlockConnectorComponent<IBlockInventory>>().ConnectedTargets;
            
            // ベルトコンベアが正しく接続されているかをチェック
            Assert.AreEqual(2, connector.Count);
        }
    }
}