using Game.Block.Blocks.BeltConveyor.Connection;
using System;
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
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using Random = System.Random;
using Game.Block.Interface.Component.ConnectJudge;

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

            // チェストブロックの配置
            // Place the chest block
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var chest);
            var chestComponent = chest.GetComponent<VanillaChestComponent>();

            // ベルトコンベアブロックの配置
            // Place the belt conveyor block
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var beltConveyor);
            var beltConveyorComponent = beltConveyor.GetComponent<VanillaBeltConveyorComponent>();
            beltConveyorComponent.InsertItem(item, InsertItemContext.Empty);
            
            // 中央tickで実搬送の完了待機。
            // Wait for resolved transport with bounded central ticks.
            for (int tick = 0; tick < 100 && !chestComponent.GetItem(0).Equals(item); tick++) GameUpdater.UpdateOneTick();

            Assert.True(chestComponent.GetItem(0).Equals(item));
        }
        
        [Test]
        public void BeltConveyorOutputChestLogicTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            var blockFactory = ServerContext.BlockFactory;

            // チェストブロックの配置
            // Place the chest block
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, -1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var chest);
            var chestComponent = chest.GetComponent<VanillaChestComponent>();

            // ベルトコンベアブロックの配置
            // Place the belt conveyor block
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var beltconveyor);
            var beltConveyorComponent = beltconveyor.GetComponent<VanillaBeltConveyorComponent>();
            
            chestComponent.SetItem(0, new ItemId(1), 1);
            
            GameUpdater.UpdateOneTick();
            
            
            Assert.AreEqual(chestComponent.GetItem(0).Count, 0);
        }
    }
}