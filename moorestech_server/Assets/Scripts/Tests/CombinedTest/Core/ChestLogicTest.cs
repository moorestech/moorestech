using System.Collections.Generic;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.Chest;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Mooresmaster.Model.BlockConnectInfoModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using Random = System.Random;

namespace Tests.CombinedTest.Core
{
    public class ChestLogicTest
    {
        //ベルトコンベアからアイテムを搬入する
        [Test]
        public void BeltConveyorInsertChestLogicTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            var itemStackFactory = ServerContext.ItemStackFactory;
            var blockFactory = ServerContext.BlockFactory;
            
            var random = new Random(4123);
            
            var id = new ItemId(random.Next(1, 11));
            var count = 1;
            var item = itemStackFactory.Create(id, count);
            
            var chest = blockFactory.Create(ForUnitTestModBlockId.ChestId, new BlockInstanceId(0), new BlockPositionInfo(Vector3Int.one, BlockDirection.North, Vector3Int.one));
            var chestComponent = chest.GetComponent<VanillaChestComponent>();
            
            var beltConveyor = blockFactory.Create(ForUnitTestModBlockId.BeltConveyorId, new BlockInstanceId(int.MaxValue), new BlockPositionInfo(Vector3Int.one, BlockDirection.North, Vector3Int.one));
            var beltConveyorComponent = beltConveyor.GetComponent<VanillaBeltConveyorComponent>();
            beltConveyorComponent.InsertItem(item);
            
            var beltConnectInventory = (Dictionary<IBlockInventory, ConnectedInfo>)beltConveyor.GetComponent<BlockConnectorComponent<IBlockInventory>>().ConnectedTargets;
            beltConnectInventory.Add(chestComponent, new ConnectedInfo());
            
            
            while (!chestComponent.GetItem(0).Equals(item)) GameUpdater.UpdateWithWait();
            
            Assert.True(chestComponent.GetItem(0).Equals(item));
        }
        
        [Test]
        public void BeltConveyorOutputChestLogicTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            var blockFactory = ServerContext.BlockFactory;
            
            var chest = blockFactory.Create(ForUnitTestModBlockId.ChestId, new BlockInstanceId(0), new BlockPositionInfo(Vector3Int.one, BlockDirection.North, Vector3Int.one));
            var chestComponent = chest.GetComponent<VanillaChestComponent>();
            
            var beltconveyor = blockFactory.Create(ForUnitTestModBlockId.BeltConveyorId, new BlockInstanceId(0), new BlockPositionInfo(Vector3Int.one, BlockDirection.North, Vector3Int.one));
            var beltConveyorComponent = beltconveyor.GetComponent<VanillaBeltConveyorComponent>();
            
            chestComponent.SetItem(0, new ItemId(1), 1);
            
            var chestConnectInventory = (Dictionary<IBlockInventory, ConnectedInfo>)chest.GetComponent<BlockConnectorComponent<IBlockInventory>>().ConnectedTargets;
            chestConnectInventory.Add(beltConveyorComponent, new ConnectedInfo());
            GameUpdater.UpdateWithWait();
            
            
            Assert.AreEqual(chestComponent.GetItem(0).Count, 0);
        }
    }
}