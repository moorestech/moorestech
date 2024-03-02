using System;
using System.Collections.Generic;
using System.Reflection;
using Core.Item;
using Core.Update;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Interface.BlockConfig;
using Game.Entity.Interface;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Const;
using Server.Protocol.PacketResponse.Util;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server
{
    public class CollectBeltConveyorItemsTest
    {
        //4秒で入って出るベルトコンベアで残り1秒で出る時の座標が正しいかどうかをテストする
        private const int TimeOfItemEnterToExit = 4000;
        private const int RemainingTime = 1000;
        private const int ItemInstanceId = 100;

        private readonly List<Vector2Int> _minusPlayerCoordinate = new()
            { new Vector2Int(-ChunkResponseConst.ChunkSize, -ChunkResponseConst.ChunkSize) };

        private readonly List<Vector2Int> _plusPlayerCoordinate = new() { new Vector2Int(0, 0) };

        /// <summary>
        ///     各方向に向いたベルトコンベア内のアイテムの位置が正しいかどうかをチェックする
        /// </summary>
        [Test]
        public void BlockDirectionItemPositionTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var itemsStackFactory = serviceProvider.GetService<ItemStackFactory>();
            var worldDataStore = serviceProvider.GetService<IWorldBlockDatastore>();
            var blockConfig = serviceProvider.GetService<IBlockConfig>();
            var entityFactory = serviceProvider.GetService<IEntityFactory>();

            //x,yがともにプラスの時のテスト 

            //北向きに設置
            worldDataStore.AddBlock(CreateOneItemInsertedItem(itemsStackFactory), 0, 0, BlockDirection.North);

            //エンティティを取得
            var itemEntity = CollectBeltConveyorItems.CollectItem(_plusPlayerCoordinate, worldDataStore, blockConfig, entityFactory)[0];
            //座標を検証
            Assert.AreEqual(0.5, itemEntity.Position.x); //0,0に設置してベルトコンベアの中心にアイテムがあるため、Z座標は0.5となる 
            Assert.AreEqual(0, itemEntity.Position.y); //2次元座標から3次元座標に変換されているため、Y座標は0となる
            Assert.AreEqual(0.75, itemEntity.Position.z); //4秒のベルトコンベアで残り1秒の時の座標のため、1の3/4の位置にある
            //エンティティを検証
            Assert.AreEqual(ItemInstanceId, itemEntity.InstanceId);
            Assert.AreEqual(VanillaEntityType.VanillaItem, itemEntity.EntityType);


            //東向きに設置
            worldDataStore.RemoveBlock(0, 0);
            worldDataStore.AddBlock(CreateOneItemInsertedItem(itemsStackFactory), 0, 0, BlockDirection.East);
            itemEntity = CollectBeltConveyorItems.CollectItem(_plusPlayerCoordinate, worldDataStore, blockConfig, entityFactory)[0];
            //座標を検証
            Assert.AreEqual(0.75, itemEntity.Position.x);
            Assert.AreEqual(0, itemEntity.Position.y);
            Assert.AreEqual(0.5, itemEntity.Position.z);


            //ブロックを削除し南向きに設置
            worldDataStore.RemoveBlock(0, 0);
            worldDataStore.AddBlock(CreateOneItemInsertedItem(itemsStackFactory), 0, 0, BlockDirection.South);
            itemEntity = CollectBeltConveyorItems.CollectItem(_plusPlayerCoordinate, worldDataStore, blockConfig, entityFactory)[0];
            //座標を検証
            Assert.AreEqual(0.5, itemEntity.Position.x);
            Assert.AreEqual(0, itemEntity.Position.y);
            Assert.AreEqual(0.25, itemEntity.Position.z);


            //ブロックを削除し西向きに設置
            worldDataStore.RemoveBlock(0, 0);
            worldDataStore.AddBlock(CreateOneItemInsertedItem(itemsStackFactory), 0, 0, BlockDirection.West);
            itemEntity = CollectBeltConveyorItems.CollectItem(_plusPlayerCoordinate, worldDataStore, blockConfig, entityFactory)[0];
            //ブロックを削除し座標を検証
            Assert.AreEqual(0.25, itemEntity.Position.x);
            Assert.AreEqual(0, itemEntity.Position.y);
            Assert.AreEqual(0.5, itemEntity.Position.z);


            //x、yがマイナスであるときのテスト
            //北向きに設
            worldDataStore.RemoveBlock(0, 0);
            worldDataStore.AddBlock(CreateOneItemInsertedItem(itemsStackFactory), -1, -1, BlockDirection.North);

            //エンティティを取得
            itemEntity = CollectBeltConveyorItems.CollectItem(_minusPlayerCoordinate, worldDataStore, blockConfig, entityFactory)[0];
            //座標を検証
            Assert.AreEqual(-0.5, itemEntity.Position.x);
            Assert.AreEqual(0, itemEntity.Position.y);
            Assert.AreEqual(-0.25, itemEntity.Position.z); //ブロックの座標がマイナスなので-1を原点として3/4の値である-0.25となる


            //東向きに設置
            worldDataStore.RemoveBlock(-1, -1);
            worldDataStore.AddBlock(CreateOneItemInsertedItem(itemsStackFactory), -1, -1, BlockDirection.East);
            itemEntity = CollectBeltConveyorItems.CollectItem(_minusPlayerCoordinate, worldDataStore, blockConfig, entityFactory)[0];
            //座標を検証
            Assert.AreEqual(-0.25, itemEntity.Position.x);
            Assert.AreEqual(0, itemEntity.Position.y);
            Assert.AreEqual(-0.5, itemEntity.Position.z);


            //ブロックを削除し南向きに設置
            worldDataStore.RemoveBlock(-1, -1);
            worldDataStore.AddBlock(CreateOneItemInsertedItem(itemsStackFactory), -1, -1, BlockDirection.South);
            itemEntity = CollectBeltConveyorItems.CollectItem(_minusPlayerCoordinate, worldDataStore, blockConfig, entityFactory)[0];
            //座標を検証
            Assert.AreEqual(-0.5, itemEntity.Position.x);
            Assert.AreEqual(0, itemEntity.Position.y);
            Assert.AreEqual(-0.75, itemEntity.Position.z);


            //ブロックを削除し西向きに設置
            worldDataStore.RemoveBlock(-1, -1);
            worldDataStore.AddBlock(CreateOneItemInsertedItem(itemsStackFactory), -1, -1, BlockDirection.West);
            itemEntity = CollectBeltConveyorItems.CollectItem(_minusPlayerCoordinate, worldDataStore, blockConfig, entityFactory)[0];
            //ブロックを削除し座標を検証
            Assert.AreEqual(-0.75, itemEntity.Position.x);
            Assert.AreEqual(0, itemEntity.Position.y);
            Assert.AreEqual(-0.5, itemEntity.Position.z);
        }


        /// <summary>
        ///     ベルトコンベアから別のベルトコンベアに移ってもInstanceIdは変化しないことをテスト
        /// </summary>
        [Test]
        public void ItemInstanceIdTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            GameUpdater.ResetUpdate();
            
            var itemsStackFactory = serviceProvider.GetService<ItemStackFactory>();
            var worldDataStore = serviceProvider.GetService<IWorldBlockDatastore>();

            var belt1 = CreateOneItemInsertedItem(itemsStackFactory);
            var belt2 = new VanillaBeltConveyor(1, 11, 1, itemsStackFactory, 4, TimeOfItemEnterToExit);
            //二つのベルトコンベアを繋がるように設置
            worldDataStore.AddBlock(belt1, 0, 0, BlockDirection.North);
            worldDataStore.AddBlock(belt2, 0, 1, BlockDirection.North);

            //4秒間アップデートする
            var now = DateTime.Now;
            while (DateTime.Now - now < TimeSpan.FromMilliseconds(RemainingTime * 1.1)) GameUpdater.UpdateWithWait();

            //ベルトコンベアからアイテムを取得
            var inventoryItemsField = typeof(VanillaBeltConveyor).GetField("_inventoryItems", BindingFlags.NonPublic | BindingFlags.Instance);
            var inventory2Items = (BeltConveyorInventoryItem[])inventoryItemsField.GetValue(belt2);

            //InstanceIdが変化していないことを検証
            Assert.AreEqual(ItemInstanceId, inventory2Items[3].ItemInstanceId);
        }


        private VanillaBeltConveyor CreateOneItemInsertedItem(ItemStackFactory itemsStackFactory)
        {
            var beltConveyor = new VanillaBeltConveyor(UnitTestModBlockId.BeltConveyorId, 10, 1, itemsStackFactory, 4, TimeOfItemEnterToExit);
            
            //リフレクションで_inventoryItemsを取得
            var inventoryItemsField = typeof(VanillaBeltConveyor).GetField("_inventoryItems", BindingFlags.NonPublic | BindingFlags.Instance);
            var inventoryItems = (BeltConveyorInventoryItem[])inventoryItemsField.GetValue(beltConveyor);

            inventoryItems[0] = new BeltConveyorInventoryItem(1, RemainingTime, ItemInstanceId);
            inventoryItems[1] = null;
            inventoryItems[2] = null;
            inventoryItems[3] = null;
            
            return beltConveyor;
        }
    }
}