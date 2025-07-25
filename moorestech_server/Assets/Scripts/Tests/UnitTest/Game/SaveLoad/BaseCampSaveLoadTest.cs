using System.Collections.Generic;
using System.Reflection;
using Core.Item.Interface;
using Core.Master;
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
            worldBlockDatastore.TryAddBlock(baseCampBlockId, position, BlockDirection.North, out var baseCampBlock);
            
            var baseCampComponent = baseCampBlock.GetComponent<IBaseCampComponent>();
            var baseCampInventory = baseCampBlock.GetComponent<IBaseCampInventory>();
            
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
            worldBlockDatastore.RemoveBlock(position);
            
            // ロード
            var (_, loadWorldBlockDatastore, _, _, _) = CreateBlockTestModule();
            loadJsonFile.Load(json);
            
            // ロードされたブロックの確認
            var loadedBlock = loadWorldBlockDatastore.GetBlock(position);
            Assert.IsNotNull(loadedBlock);
            Assert.AreEqual(baseCampBlockId, loadedBlock.BlockId);
            Assert.AreEqual(blockInstanceId, loadedBlock.BlockInstanceId);
            
            // 納品状態の確認
            var loadedBaseCampComponent = loadedBlock.GetComponent<IBaseCampComponent>();
            var loadedBaseCampInventory = loadedBlock.GetComponent<IBaseCampInventory>();
            
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
            worldBlockDatastore.TryAddBlock(baseCampBlockId, position, BlockDirection.South, out var baseCampBlock);
            
            var baseCampComponent = baseCampBlock.GetComponent<IBaseCampComponent>();
            var baseCampInventory = baseCampBlock.GetComponent<IBaseCampInventory>();
            
            // 複数アイテムの部分納品
            baseCampInventory.InsertItem(itemStackFactory.Create(new ItemId(1), 2));
            baseCampInventory.InsertItem(itemStackFactory.Create(new ItemId(2), 3));
            baseCampInventory.InsertItem(itemStackFactory.Create(new ItemId(3), 1));
            
            var progress = baseCampComponent.GetProgress();
            var blockInstanceId = baseCampBlock.BlockInstanceId;
            
            // セーブ
            var json = assembleSaveJsonText.AssembleSaveJson();
            
            // ブロックを削除
            worldBlockDatastore.RemoveBlock(position);
            
            // ロード
            var (_, loadWorldBlockDatastore, _, _, _) = CreateBlockTestModule();
            loadJsonFile.Load(json);
            
            // ロードされたブロックの確認
            var loadedBlock = loadWorldBlockDatastore.GetBlock(position);
            Assert.IsNotNull(loadedBlock);
            Assert.AreEqual(BlockDirection.South, loadedBlock.BlockPositionInfo.BlockDirection);
            
            var loadedBaseCampComponent = loadedBlock.GetComponent<IBaseCampComponent>();
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
            worldBlockDatastore.TryAddBlock(baseCampBlockId, position, BlockDirection.East, out var baseCampBlock);
            
            var baseCampComponent = baseCampBlock.GetComponent<IBaseCampComponent>();
            var baseCampInventory = baseCampBlock.GetComponent<IBaseCampInventory>();
            
            // すべてのアイテムを納品（変換直前の状態）
            var requiredItemId = new ItemId(1);
            var requiredAmount = 10;
            baseCampInventory.InsertItem(itemStackFactory.Create(requiredItemId, requiredAmount));
            
            // 完了しているが変換はまだ実行されていない状態を想定
            Assert.IsTrue(baseCampComponent.IsCompleted());
            
            // セーブ
            var json = assembleSaveJsonText.AssembleSaveJson();
            
            // ブロックを削除
            worldBlockDatastore.RemoveBlock(position);
            
            // ロード
            var (_, loadWorldBlockDatastore, _, _, _) = CreateBlockTestModule();
            loadJsonFile.Load(json);
            
            // ロードされたブロックの確認
            var loadedBlock = loadWorldBlockDatastore.GetBlock(position);
            Assert.IsNotNull(loadedBlock);
            
            var loadedBaseCampComponent = loadedBlock.GetComponent<IBaseCampComponent>();
            Assert.IsTrue(loadedBaseCampComponent.IsCompleted());
            Assert.AreEqual(1.0f, loadedBaseCampComponent.GetProgress(), 0.01f);
        }
        
        private List<IItemStack> GetDeliveredItems(IBaseCampInventory inventory)
        {
            // リフレクションを使用して内部の納品済みアイテムリストを取得
            var deliveredItemsField = inventory.GetType()
                .GetField("_deliveredItems", BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (deliveredItemsField != null)
            {
                return deliveredItemsField.GetValue(inventory) as List<IItemStack> ?? new List<IItemStack>();
            }
            
            // 代替方法：公開メソッドがある場合
            return inventory.GetDeliveredItems();
        }
        
        private (IBlockFactory, IWorldBlockDatastore, PlayerInventoryDataStore, AssembleSaveJsonText, WorldLoaderFromJson)
            CreateBlockTestModule()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var blockFactory = ServerContext.BlockFactory;
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var assembleSaveJsonText = serviceProvider.GetService<AssembleSaveJsonText>();
            var playerInventoryDataStore = serviceProvider.GetService<PlayerInventoryDataStore>();
            var loadJsonFile = serviceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson;
            
            return (blockFactory, worldBlockDatastore, playerInventoryDataStore, assembleSaveJsonText, loadJsonFile);
        }
    }
    
    // TODO: 実装時に削除 - 仮のインターフェース定義
    public interface IBaseCampComponent : IBlockComponent
    {
        bool IsCompleted();
        float GetProgress();
    }
    
    public interface IBaseCampInventory : IBlockInventory
    {
        List<IItemStack> GetDeliveredItems();
    }
}