using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    指摘：プロトコルのテストが無い。プロトコルとして、「アイテム納品を完了させる」ためのプロトコルが必要なのでその実装をしてください。具体的には、アイテムを必要数入れたあと、そのプロトコルを叩き、既存のBaseCampが消え、同じ座標のブロックが変換後のブロックになっていることを確認してください。
    public class BaseCampTest
    {
        [Test]
        public void SimpleSingleItemDeliveryTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var itemStackFactory = ServerContext.ItemStackFactory;
            var blockFactory = ServerContext.BlockFactory;
            
            // ベースキャンプブロックの配置
            指摘：テスト用のブロックIDは moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestModBlockId.cs に定義し、それを使うようにしてください。この旨はCLAUDE.mdに追記してください。
            var baseCampBlockId = MasterHolder.BlockMaster.GetBlockId(new System.Guid("5f8e8f90-0000-0000-0000-000000000001")); // TODO: 実際のベースキャンプブロックIDに変更
            var baseCampBlock = blockFactory.Create(baseCampBlockId, new BlockInstanceId(1), new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, Vector3Int.one));
            
            // ベースキャンプコンポーネントの取得
            var baseCampComponent = baseCampBlock.GetComponent<IBaseCampComponent>(); // TODO: 実際のコンポーネント名に変更
            var baseCampInventory = baseCampBlock.GetComponent<IBlockInventory>();
            
            // 必要なアイテムの設定（テスト用に単一アイテム）
            var requiredItemId = new ItemId(1);
            var requiredAmount = 5;
            
            // アイテムを納品
            var deliveredItem = itemStackFactory.Create(requiredItemId, requiredAmount);
            baseCampInventory.InsertItem(deliveredItem);
            
            // 納品完了の確認
            Assert.IsTrue(baseCampComponent.IsCompleted());
            
            // ブロックが変化したことを確認
            指摘：ブロックの置換はプロトコル側で行い、置換時にコンポーネントは破棄されるため、このAssertは不適切であり不要。GetTransformedBlockIdも不要。これより下のテストも同様なので直して
            Assert.AreNotEqual(baseCampBlockId, baseCampComponent.GetTransformedBlockId());
        }
        
        [Test]
        public void MultipleItemDeliveryTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var itemStackFactory = ServerContext.ItemStackFactory;
            var blockFactory = ServerContext.BlockFactory;
            
            // ベースキャンプブロックの配置
            var baseCampBlockId = MasterHolder.BlockMaster.GetBlockId(new System.Guid("5f8e8f90-0000-0000-0000-000000000002")); // TODO: 複数アイテム要求のベースキャンプブロックID
            var baseCampBlock = blockFactory.Create(baseCampBlockId, new BlockInstanceId(2), new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, Vector3Int.one));
            
            var baseCampComponent = baseCampBlock.GetComponent<IBaseCampComponent>();
            var baseCampInventory = baseCampBlock.GetComponent<IBlockInventory>();
            
            // 複数の必要アイテム
            var requiredItems = new List<(ItemId id, int count)>
            {
                (new ItemId(1), 3),
                (new ItemId(2), 5),
                (new ItemId(3), 2)
            };
            
            // 部分的に納品
            baseCampInventory.InsertItem(itemStackFactory.Create(requiredItems[0].id, requiredItems[0].count));
            Assert.IsFalse(baseCampComponent.IsCompleted());
            
            baseCampInventory.InsertItem(itemStackFactory.Create(requiredItems[1].id, requiredItems[1].count));
            Assert.IsFalse(baseCampComponent.IsCompleted());
            
            // 最後のアイテムを納品
            baseCampInventory.InsertItem(itemStackFactory.Create(requiredItems[2].id, requiredItems[2].count));
            Assert.IsTrue(baseCampComponent.IsCompleted());
        }
        
        [Test]
        public void WrongItemDeliveryTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var itemStackFactory = ServerContext.ItemStackFactory;
            var blockFactory = ServerContext.BlockFactory;
            
            // ベースキャンプブロックの配置
            var baseCampBlockId = MasterHolder.BlockMaster.GetBlockId(new System.Guid("5f8e8f90-0000-0000-0000-000000000001"));
            var baseCampBlock = blockFactory.Create(baseCampBlockId, new BlockInstanceId(3), new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, Vector3Int.one));
            
            var baseCampComponent = baseCampBlock.GetComponent<IBaseCampComponent>();
            var baseCampInventory = baseCampBlock.GetComponent<IBlockInventory>();
            
            // 間違ったアイテムを納品しようとする
            var wrongItemId = new ItemId(999);
            var wrongItem = itemStackFactory.Create(wrongItemId, 10);
            
            var insertedCount = baseCampInventory.InsertItem(wrongItem);
            
            // 間違ったアイテムは受け付けないことを確認
            Assert.AreEqual(0, insertedCount);
            Assert.IsFalse(baseCampComponent.IsCompleted());
        }
        
        [Test]
        public void PartialDeliveryProgressTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var itemStackFactory = ServerContext.ItemStackFactory;
            var blockFactory = ServerContext.BlockFactory;
            
            // ベースキャンプブロックの配置
            var baseCampBlockId = MasterHolder.BlockMaster.GetBlockId(new System.Guid("5f8e8f90-0000-0000-0000-000000000001"));
            var baseCampBlock = blockFactory.Create(baseCampBlockId, new BlockInstanceId(4), new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, Vector3Int.one));
            
            var baseCampComponent = baseCampBlock.GetComponent<IBaseCampComponent>();
            var baseCampInventory = baseCampBlock.GetComponent<IBlockInventory>();
            
            var requiredItemId = new ItemId(1);
            var requiredAmount = 10;
            
            // 部分的に納品
            baseCampInventory.InsertItem(itemStackFactory.Create(requiredItemId, 3));
            Assert.AreEqual(0.3f, baseCampComponent.GetProgress(), 0.01f);
            
            baseCampInventory.InsertItem(itemStackFactory.Create(requiredItemId, 4));
            Assert.AreEqual(0.7f, baseCampComponent.GetProgress(), 0.01f);
            
            baseCampInventory.InsertItem(itemStackFactory.Create(requiredItemId, 3));
            Assert.AreEqual(1.0f, baseCampComponent.GetProgress(), 0.01f);
            Assert.IsTrue(baseCampComponent.IsCompleted());
        }
        
        [Test]
        public void BlockTransformationDetailsTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var itemStackFactory = ServerContext.ItemStackFactory;
            var blockFactory = ServerContext.BlockFactory;
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            var position = new Vector3Int(10, 0, 10);
            var baseCampBlockId = MasterHolder.BlockMaster.GetBlockId(new System.Guid("5f8e8f90-0000-0000-0000-000000000001"));
            
            // ワールドに配置
            worldBlockDatastore.TryAddBlock(baseCampBlockId, position, BlockDirection.North, out var baseCampBlock);
            
            var baseCampComponent = baseCampBlock.GetComponent<IBaseCampComponent>();
            var baseCampInventory = baseCampBlock.GetComponent<IBlockInventory>();
            
            // 必要なアイテムを納品
            var requiredItemId = new ItemId(1);
            var requiredAmount = 5;
            baseCampInventory.InsertItem(itemStackFactory.Create(requiredItemId, requiredAmount));
            
            // ブロック変換が発生したことを確認
            Assert.IsTrue(baseCampComponent.IsCompleted());
            
            // 変換後のブロックがワールドに存在することを確認
            var transformedBlock = worldBlockDatastore.GetBlock(position);
            Assert.IsNotNull(transformedBlock);
            Assert.AreNotEqual(baseCampBlockId, transformedBlock.BlockId);
            Assert.AreEqual(baseCampComponent.GetTransformedBlockId(), transformedBlock.BlockId);
        }
    }
    
    // TODO: 実装時に削除 - 仮のインターフェース定義
    public interface IBaseCampComponent : IBlockComponent
    {
        bool IsCompleted();
        float GetProgress();
        BlockId GetTransformedBlockId();
    }
}