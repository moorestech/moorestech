using System.Collections.Generic;
using System.Reflection;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.BaseCamp;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.PlayerInventory;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using System;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class BaseCampSaveLoadTest
    {
        [Test]
        public void PartialDeliverySaveLoadTest()
        {
            var (blockFactory, worldBlockDatastore, _, assembleSaveJsonText, loadJsonFile) = CreateBlockTestModule();
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            // ベースキャンプブロックの配置
            var baseCampBlockId = ForUnitTestModBlockId.BaseCamp1;
            var position = new Vector3Int(10, 0, 10);
            worldBlockDatastore.TryAddBlock(baseCampBlockId, position, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var baseCampBlock);
            
            var baseCampComponent = baseCampBlock.GetComponent<BaseCampComponent>();
            var baseCampInventory = baseCampBlock.GetComponent<IBlockInventory>();
            
            Assert.IsNotNull(baseCampComponent, "BaseCampComponent should not be null");
            Assert.IsNotNull(baseCampInventory, "IBlockInventory should not be null");
            
            // 部分的にアイテムを納品
            var requiredItemId = new ItemId(1);
            baseCampInventory.InsertItem(itemStackFactory.Create(requiredItemId, 3));
            
            // 納品されたアイテムと進捗を記録
            var deliveredItems = GetDeliveredItems(baseCampInventory);
            var progress = baseCampComponent.GetProgress();
            var blockInstanceId = baseCampBlock.BlockInstanceId;
            
            // セーブ
            var json = assembleSaveJsonText.AssembleSaveJson();
            Debug.Log($"Saved JSON: {json}");
            
            // ブロックを削除
            worldBlockDatastore.RemoveBlock(position, BlockRemoveReason.ManualRemove);
            
            // ロード
            Debug.Log("Creating new test module for loading...");
            var (_, loadWorldBlockDatastore, _, _, loadJsonFileForLoad) = CreateBlockTestModule();
            Debug.Log("Loading JSON...");
            loadJsonFileForLoad.Load(json);
            Debug.Log("JSON loaded, checking for block...");
            
            // ロードされたブロックの確認
            Debug.Log($"Trying to get block at position {position}");
            var loadedBlock = loadWorldBlockDatastore.GetBlock(position);
            Assert.IsNotNull(loadedBlock, $"Block at position {position} should be loaded");
            Assert.AreEqual(baseCampBlockId, loadedBlock.BlockId);
            Assert.AreEqual(blockInstanceId, loadedBlock.BlockInstanceId);
            
            // 納品状態の確認
            var loadedBaseCampComponent = loadedBlock.GetComponent<BaseCampComponent>();
            var loadedBaseCampInventory = loadedBlock.GetComponent<IBlockInventory>();
            
            Assert.AreEqual(progress, loadedBaseCampComponent.GetProgress(), 0.01f);
            
            var loadedDeliveredItems = GetDeliveredItems(loadedBaseCampInventory);
            Assert.AreEqual(deliveredItems.Count, loadedDeliveredItems.Count);
            for (int i = 0; i < deliveredItems.Count; i++)
            {
                Assert.AreEqual(deliveredItems[i].Id, loadedDeliveredItems[i].Id);
                Assert.AreEqual(deliveredItems[i].Count, loadedDeliveredItems[i].Count);
            }
        }
        
        [Test]
        public void MultipleItemsPartialDeliverySaveLoadTest()
        {
            var (blockFactory, worldBlockDatastore, _, assembleSaveJsonText, loadJsonFile) = CreateBlockTestModule();
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            // 複数アイテム要求のベースキャンプブロック配置
            var baseCampBlockId = ForUnitTestModBlockId.BaseCamp2;
            var position = new Vector3Int(5, 0, 5);
            worldBlockDatastore.TryAddBlock(baseCampBlockId, position, BlockDirection.South, Array.Empty<BlockCreateParam>(), out var baseCampBlock);
            
            var baseCampComponent = baseCampBlock.GetComponent<BaseCampComponent>();
            var baseCampInventory = baseCampBlock.GetComponent<IBlockInventory>();
            
            // 複数アイテムの部分納品
            baseCampInventory.InsertItem(itemStackFactory.Create(new ItemId(1), 2));
            baseCampInventory.InsertItem(itemStackFactory.Create(new ItemId(2), 3));
            baseCampInventory.InsertItem(itemStackFactory.Create(new ItemId(3), 1));
            
            var progress = baseCampComponent.GetProgress();
            var blockInstanceId = baseCampBlock.BlockInstanceId;
            
            // セーブ
            var json = assembleSaveJsonText.AssembleSaveJson();
            
            // ブロックを削除
            worldBlockDatastore.RemoveBlock(position, BlockRemoveReason.ManualRemove);
            
            // ロード
            var (_, loadWorldBlockDatastore, _, _, loadJsonFileForLoad) = CreateBlockTestModule();
            loadJsonFileForLoad.Load(json);
            
            // ロードされたブロックの確認
            var loadedBlock = loadWorldBlockDatastore.GetBlock(position);
            Assert.IsNotNull(loadedBlock);
            Assert.AreEqual(BlockDirection.South, loadedBlock.BlockPositionInfo.BlockDirection);
            
            var loadedBaseCampComponent = loadedBlock.GetComponent<BaseCampComponent>();
            Assert.AreEqual(progress, loadedBaseCampComponent.GetProgress(), 0.01f);
            Assert.IsFalse(loadedBaseCampComponent.IsCompleted());
        }
        
        [Test]
        public void CompletedButNotTransformedSaveLoadTest()
        {
            var (blockFactory, worldBlockDatastore, _, assembleSaveJsonText, loadJsonFile) = CreateBlockTestModule();
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            // ベースキャンプブロックの配置
            var baseCampBlockId = ForUnitTestModBlockId.BaseCamp1;
            var position = new Vector3Int(0, 0, 0);
            worldBlockDatastore.TryAddBlock(baseCampBlockId, position, BlockDirection.East, Array.Empty<BlockCreateParam>(), out var baseCampBlock);
            
            var baseCampComponent = baseCampBlock.GetComponent<BaseCampComponent>();
            var baseCampInventory = baseCampBlock.GetComponent<IBlockInventory>();
            
            // すべてのアイテムを納品（変換直前の状態）
            var requiredItemId = new ItemId(1);
            var requiredAmount = 10;
            baseCampInventory.InsertItem(itemStackFactory.Create(requiredItemId, requiredAmount));
            
            // 完了しているが変換はまだ実行されていない状態を想定
            Assert.IsTrue(baseCampComponent.IsCompleted());
            
            // セーブ
            var json = assembleSaveJsonText.AssembleSaveJson();
            
            // ブロックを削除
            worldBlockDatastore.RemoveBlock(position, BlockRemoveReason.ManualRemove);
            
            // ロード
            var (_, loadWorldBlockDatastore, _, _, loadJsonFileForLoad) = CreateBlockTestModule();
            loadJsonFileForLoad.Load(json);
            
            // ロードされたブロックの確認
            var loadedBlock = loadWorldBlockDatastore.GetBlock(position);
            Assert.IsNotNull(loadedBlock);
            
            var loadedBaseCampComponent = loadedBlock.GetComponent<BaseCampComponent>();
            Assert.IsTrue(loadedBaseCampComponent.IsCompleted());
            Assert.AreEqual(1.0f, loadedBaseCampComponent.GetProgress(), 0.01f);
        }
        
        private List<IItemStack> GetDeliveredItems(IBlockInventory inventory)
        {
            // インベントリから全アイテムを取得（空でないものだけ）
            var items = new List<IItemStack>();
            for (int i = 0; i < inventory.GetSlotSize(); i++)
            {
                var item = inventory.GetItem(i);
                if (item != null && item.Id != ItemMaster.EmptyItemId)
                {
                    items.Add(item);
                }
            }
            return items;
        }
        
        private (IBlockFactory, IWorldBlockDatastore, PlayerInventoryDataStore, AssembleSaveJsonText, WorldLoaderFromJson)
            CreateBlockTestModule()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            var blockFactory = ServerContext.BlockFactory;
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var assembleSaveJsonText = serviceProvider.GetService<AssembleSaveJsonText>();
            var playerInventoryDataStore = serviceProvider.GetService<PlayerInventoryDataStore>();
            var loadJsonFile = serviceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson;
            
            return (blockFactory, worldBlockDatastore, playerInventoryDataStore, assembleSaveJsonText, loadJsonFile);
        }
    }
}
