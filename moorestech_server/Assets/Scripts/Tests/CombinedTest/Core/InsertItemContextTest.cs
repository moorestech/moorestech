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
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.InventoryConnectsModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module;
using Tests.Module.TestMod;
using UnityEngine;
using Game.Block.Interface.Component.ConnectJudge;

namespace Tests.CombinedTest.Core
{
    /// <summary>
    /// InsertItemContextが正しく設定されるかテスト
    /// Test that InsertItemContext is correctly set
    /// </summary>
    public class InsertItemContextTest
    {


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
            var connectedInfo = new ConnectedInfo(selfConnector, targetConnector, null, UnityEngine.Vector3Int.zero);

            var chestConnectorComponent = chest.GetComponent<BlockConnectorComponent<IBlockInventory, DefaultConnectJudge>>();
            var connectInventory = (Dictionary<IBlockInventory, ConnectedInfo>)chestConnectorComponent.ConnectedTargets;
            connectInventory.Clear();
            connectInventory.Add(dummyTarget, connectedInfo);

            // チェストにアイテムを設定
            // Set item to chest
            var item = itemStackFactory.Create(new ItemId(1), 1);
            chestComponent.SetItem(0, item);

            // アイテムがターゲットに転送されるまで待つ
            // Wait until item is transferred to target
            while (dummyTarget.InsertedContexts.Count == 0) GameUpdater.UpdateOneTick();

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





        private static IBlockConnector CreateInventoryConnector(int index)
        {
            return new OutputConnectsElement(index, Guid.NewGuid(), null, Vector3Int.zero, Array.Empty<Vector3Int>());
        }
    }
}
