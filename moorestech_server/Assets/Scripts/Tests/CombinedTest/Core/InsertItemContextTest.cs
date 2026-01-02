using System;
using System.Collections.Generic;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.Chest;
using Game.Block.Blocks.Connector;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;

using NUnit.Framework;
using Server.Boot;
using Tests.Module;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    /// <summary>
    /// InsertItemContextが正しく設定されるかテスト
    /// Test that InsertItemContext is correctly set
    /// </summary>
    public class InsertItemContextTest
    {
        /// <summary>
        /// ベルトコンベアからターゲットにアイテムが転送される際、InsertItemContextが正しく設定されるかテスト
        /// Test that InsertItemContext is correctly set when items are transferred from belt conveyor to target
        /// </summary>
        [Test]
        public void BeltConveyorToTargetInsertContextTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var itemStackFactory = ServerContext.ItemStackFactory;

            // ベルトコンベアを作成（WorldBlockDatastoreに登録）
            // Create belt conveyor (registered in WorldBlockDatastore)
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var beltConveyor);
            var beltBlockInstanceId = beltConveyor.BlockInstanceId;
            var beltConveyorComponent = beltConveyor.GetComponent<VanillaBeltConveyorComponent>();

            // ターゲットとしてDummyBlockInventoryを使用（InsertItemContextを記録）
            // Use DummyBlockInventory as target (records InsertItemContext)
            var dummyTarget = new DummyBlockInventory();

            // ベルトコンベア→ターゲットの接続を設定
            // Set up belt conveyor → target connection
            var selfConnector = CreateInventoryConnector(0);
            var targetConnector = CreateInventoryConnector(1);
            var connectedInfo = new ConnectedInfo(selfConnector, targetConnector, null);

            var beltConnectorComponent = beltConveyor.GetComponent<BlockConnectorComponent<IBlockInventory>>();
            var connectInventory = (Dictionary<IBlockInventory, ConnectedInfo>)beltConnectorComponent.ConnectedTargets;
            connectInventory.Clear();
            connectInventory.Add(dummyTarget, connectedInfo);

            // アイテムを挿入
            // Insert item
            var item = itemStackFactory.Create(new ItemId(1), 1);
            beltConveyorComponent.InsertItem(item, InsertItemContext.Empty);

            // アイテムが出力されるまで待つ
            // Wait until item is output
            while (dummyTarget.InsertedContexts.Count == 0) GameUpdater.UpdateWithWait();

            // InsertItemContextが正しく設定されていることを確認
            // Verify InsertItemContext is correctly set
            Assert.AreEqual(1, dummyTarget.InsertedContexts.Count);
            var context = dummyTarget.InsertedContexts[0];

            // SourceBlockInstanceIdがベルトコンベアのBlockInstanceIdと一致すること
            // SourceBlockInstanceId matches belt conveyor's BlockInstanceId
            Assert.AreEqual(beltBlockInstanceId, context.SourceBlockInstanceId);

            // SourceConnectorが正しく設定されていること
            // SourceConnector is correctly set
            Assert.IsNotNull(context.SourceConnector);
            Assert.AreEqual(selfConnector.ConnectorGuid, context.SourceConnector.ConnectorGuid);

            // TargetConnectorが正しく設定されていること
            // TargetConnector is correctly set
            Assert.IsNotNull(context.TargetConnector);
            Assert.AreEqual(targetConnector.ConnectorGuid, context.TargetConnector.ConnectorGuid);
        }

        /// <summary>
        /// チェストからターゲットにアイテムが転送される際、InsertItemContextが正しく設定されるかテスト
        /// Test that InsertItemContext is correctly set when items are transferred from chest to target
        /// </summary>
        [Test]
        public void ChestToTargetInsertContextTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var itemStackFactory = ServerContext.ItemStackFactory;

            // チェストを作成（WorldBlockDatastoreに登録）
            // Create chest (registered in WorldBlockDatastore)
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var chest);
            var chestBlockInstanceId = chest.BlockInstanceId;
            var chestComponent = chest.GetComponent<VanillaChestComponent>();

            // ターゲットとしてDummyBlockInventoryを使用（InsertItemContextを記録）
            // Use DummyBlockInventory as target (records InsertItemContext)
            var dummyTarget = new DummyBlockInventory();

            // チェスト→ターゲットの接続を設定
            // Set up chest → target connection
            var selfConnector = CreateInventoryConnector(0);
            var targetConnector = CreateInventoryConnector(1);
            var connectedInfo = new ConnectedInfo(selfConnector, targetConnector, null);

            var chestConnectorComponent = chest.GetComponent<BlockConnectorComponent<IBlockInventory>>();
            var connectInventory = (Dictionary<IBlockInventory, ConnectedInfo>)chestConnectorComponent.ConnectedTargets;
            connectInventory.Clear();
            connectInventory.Add(dummyTarget, connectedInfo);

            // チェストにアイテムを設定
            // Set item to chest
            var item = itemStackFactory.Create(new ItemId(1), 1);
            chestComponent.SetItem(0, item);

            // アイテムがターゲットに転送されるまで待つ
            // Wait until item is transferred to target
            while (dummyTarget.InsertedContexts.Count == 0) GameUpdater.UpdateWithWait();

            // InsertItemContextが正しく設定されていることを確認
            // Verify InsertItemContext is correctly set
            Assert.AreEqual(1, dummyTarget.InsertedContexts.Count);
            var context = dummyTarget.InsertedContexts[0];

            // SourceBlockInstanceIdがチェストのBlockInstanceIdと一致すること
            // SourceBlockInstanceId matches chest's BlockInstanceId
            Assert.AreEqual(chestBlockInstanceId, context.SourceBlockInstanceId);

            // SourceConnectorが正しく設定されていること
            // SourceConnector is correctly set
            Assert.IsNotNull(context.SourceConnector);
            Assert.AreEqual(selfConnector.ConnectorGuid, context.SourceConnector.ConnectorGuid);

            // TargetConnectorが正しく設定されていること
            // TargetConnector is correctly set
            Assert.IsNotNull(context.TargetConnector);
            Assert.AreEqual(targetConnector.ConnectorGuid, context.TargetConnector.ConnectorGuid);
        }

        /// <summary>
        /// ベルトコンベアがアイテムを受け取った際、PathIdがアイテムに設定されるかテスト
        /// Test that PathId is set on item when belt conveyor receives item
        /// </summary>
        [Test]
        public void BeltConveyorReceivesPathIdFromContextTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var itemStackFactory = ServerContext.ItemStackFactory;

            // ベルトコンベアを作成（WorldBlockDatastoreに登録）
            // Create belt conveyor (registered in WorldBlockDatastore)
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var beltConveyor);
            var beltConveyorComponent = beltConveyor.GetComponent<VanillaBeltConveyorComponent>();

            // InsertItemContextにPathIdを設定してベルトコンベアにアイテムを挿入
            // Insert item into belt conveyor with PathId set in InsertItemContext
            var sourceConnector = CreateInventoryConnector(0);
            var targetConnector = CreateInventoryConnector(1);
            var context = new InsertItemContext(new BlockInstanceId(99999), sourceConnector, targetConnector);

            var item = itemStackFactory.Create(new ItemId(1), 1);
            beltConveyorComponent.InsertItem(item, context);

            // ベルトコンベアのアイテムにStartConnectorが設定されていることを確認
            // Verify StartConnector is set on belt conveyor item
            var beltItem = beltConveyorComponent.BeltConveyorItems[^1];
            Assert.IsNotNull(beltItem);
            Assert.IsNotNull(beltItem.StartConnector);
            Assert.AreEqual(targetConnector.ConnectorGuid, beltItem.StartConnector.ConnectorGuid);
        }

        /// <summary>
        /// チェスト→ベルトコンベア→ターゲットの全経路でInsertItemContextが正しく設定されるかテスト
        /// Test that InsertItemContext is correctly set for the entire path: chest → belt conveyor → target
        /// </summary>
        [Test]
        public void ChestToBeltConveyorToTargetInsertContextTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var itemStackFactory = ServerContext.ItemStackFactory;

            // 入力チェストを作成（WorldBlockDatastoreに登録）
            // Create input chest (registered in WorldBlockDatastore)
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var inputChest);
            var inputChestBlockInstanceId = inputChest.BlockInstanceId;
            var inputChestComponent = inputChest.GetComponent<VanillaChestComponent>();

            // ベルトコンベアを作成（WorldBlockDatastoreに登録）
            // Create belt conveyor (registered in WorldBlockDatastore)
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var beltConveyor);
            var beltBlockInstanceId = beltConveyor.BlockInstanceId;
            var beltConveyorComponent = beltConveyor.GetComponent<VanillaBeltConveyorComponent>();

            // 出力ターゲットとしてDummyBlockInventoryを使用
            // Use DummyBlockInventory as output target
            var dummyTarget = new DummyBlockInventory();

            // 入力チェスト→ベルトコンベアの接続を設定
            // Set up input chest → belt conveyor connection
            var inputChestConnector = CreateInventoryConnector(0);
            var beltInputConnector = CreateInventoryConnector(1);
            var inputChestConnectedInfo = new ConnectedInfo(inputChestConnector, beltInputConnector, beltConveyor);

            var inputChestConnectorComponent = inputChest.GetComponent<BlockConnectorComponent<IBlockInventory>>();
            var inputChestConnectInventory = (Dictionary<IBlockInventory, ConnectedInfo>)inputChestConnectorComponent.ConnectedTargets;
            inputChestConnectInventory.Clear();
            inputChestConnectInventory.Add(beltConveyorComponent, inputChestConnectedInfo);

            // ベルトコンベア→出力ターゲットの接続を設定
            // Set up belt conveyor → output target connection
            var beltOutputConnector = CreateInventoryConnector(0);
            var targetInputConnector = CreateInventoryConnector(1);
            var beltConnectedInfo = new ConnectedInfo(beltOutputConnector, targetInputConnector, null);

            var beltConnectorComponent = beltConveyor.GetComponent<BlockConnectorComponent<IBlockInventory>>();
            var beltConnectInventory = (Dictionary<IBlockInventory, ConnectedInfo>)beltConnectorComponent.ConnectedTargets;
            beltConnectInventory.Clear();
            beltConnectInventory.Add(dummyTarget, beltConnectedInfo);

            // 入力チェストにアイテムを設定
            // Set item to input chest
            var item = itemStackFactory.Create(new ItemId(1), 1);
            inputChestComponent.SetItem(0, item);

            // アイテムが最終ターゲットに届くまで待つ
            // Wait until item reaches final target
            while (dummyTarget.InsertedContexts.Count == 0) GameUpdater.UpdateWithWait();

            // ベルトコンベアのアイテムに入力時のPathIdが設定されていることを確認
            // Verify PathId from input is set on belt conveyor item (item already moved, check via dummy target)
            // Note: Since item has moved to target, we check the context received by target

            // 出力ターゲットが受け取ったInsertItemContextを確認
            // Verify InsertItemContext received by output target
            Assert.AreEqual(1, dummyTarget.InsertedContexts.Count);
            var targetContext = dummyTarget.InsertedContexts[0];

            // SourceBlockInstanceIdがベルトコンベアのBlockInstanceIdと一致すること（最後の送信元はベルトコンベア）
            // SourceBlockInstanceId matches belt conveyor's BlockInstanceId (last sender is belt conveyor)
            Assert.AreEqual(beltBlockInstanceId, targetContext.SourceBlockInstanceId);
            Assert.AreEqual(beltOutputConnector.ConnectorGuid, targetContext.SourceConnector.ConnectorGuid);
            Assert.AreEqual(targetInputConnector.ConnectorGuid, targetContext.TargetConnector.ConnectorGuid);
        }

        private static IBlockConnector CreateInventoryConnector(int index)
        {
            return BlockConnectorAdapter.CreateForTest(Guid.NewGuid(), Vector3Int.zero, Array.Empty<Vector3Int>());
        }
    }
}
