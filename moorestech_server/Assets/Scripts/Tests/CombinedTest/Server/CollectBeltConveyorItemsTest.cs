using System;
using System.Collections.Generic;
using System.Reflection;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Entity.Interface;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.BlockConnectInfoModule;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Util;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server
{
    public class CollectBeltConveyorItemsTest
    {
        //4秒で入って出るベルトコンベアで残り1秒で出る時の座標が正しいかどうかをテストする
        private const double RemainingTime = 0.5;
        private const int ItemInstanceId = 100;
        
        private readonly List<Vector2Int> _plusPlayerCoordinate = new() { new Vector2Int(0, 0) };
        
        /// <summary>
        ///     各方向に向いたベルトコンベア内のアイテムの位置が正しいかどうかをチェックする
        /// </summary>
        [Test]
        public void BlockDirectionItemPositionTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldDataStore = ServerContext.WorldBlockDatastore;
            var entityFactory = serviceProvider.GetService<IEntityFactory>();
            
            //x,yがともにプラスの時のテスト 
            
            //北向きに設置
            CreateOneItemInsertedItem(new Vector3Int(0, 0, 0), BlockDirection.North, worldDataStore);
            
            //エンティティを取得
            var itemEntity = CollectBeltConveyorItems.CollectItemFromWorld(entityFactory, Vector3.zero, float.MaxValue)[0];
            //座標を検証
            const float defaultY = CollectBeltConveyorItems.DefaultBeltConveyorHeight;
            Assert.AreEqual(0.5, itemEntity.Position.x); //0,0に設置してベルトコンベアの中心にアイテムがあるため、Z座標は0.5となる 
            Assert.AreEqual(defaultY, itemEntity.Position.y); //2次元座標から3次元座標に変換されているため、Y座標は0となる
            Assert.AreEqual(0.75, itemEntity.Position.z); //4秒のベルトコンベアで残り1秒の時の座標のため、1の3/4の位置にある
            //エンティティを検証
            Assert.AreEqual(ItemInstanceId, itemEntity.InstanceId.AsPrimitive());
            Assert.AreEqual(VanillaEntityType.VanillaItem, itemEntity.EntityType);
            
            
            //東向きに設置
            worldDataStore.RemoveBlock(new Vector3Int(0, 0, 0), BlockRemoveReason.ManualRemove);
            CreateOneItemInsertedItem(new Vector3Int(0, 0, 0), BlockDirection.East, worldDataStore);
            itemEntity = CollectBeltConveyorItems.CollectItemFromWorld(entityFactory, Vector3.zero, float.MaxValue)[0];
            //座標を検証
            Assert.AreEqual(0.75, itemEntity.Position.x);
            Assert.AreEqual(defaultY, itemEntity.Position.y);
            Assert.AreEqual(0.5, itemEntity.Position.z);
            
            
            //ブロックを削除し南向きに設置
            worldDataStore.RemoveBlock(new Vector3Int(0, 0, 0), BlockRemoveReason.ManualRemove);
            CreateOneItemInsertedItem(new Vector3Int(0, 0, 0), BlockDirection.South, worldDataStore);
            itemEntity = CollectBeltConveyorItems.CollectItemFromWorld(entityFactory, Vector3.zero, float.MaxValue)[0];
            //座標を検証
            Assert.AreEqual(0.5, itemEntity.Position.x);
            Assert.AreEqual(defaultY, itemEntity.Position.y);
            Assert.AreEqual(0.25, itemEntity.Position.z);
            
            
            //ブロックを削除し西向きに設置
            worldDataStore.RemoveBlock(new Vector3Int(0, 0, 0), BlockRemoveReason.ManualRemove);
            CreateOneItemInsertedItem(new Vector3Int(0, 0, 0), BlockDirection.West, worldDataStore);
            itemEntity = CollectBeltConveyorItems.CollectItemFromWorld(entityFactory, Vector3.zero, float.MaxValue)[0];
            //ブロックを削除し座標を検証
            Assert.AreEqual(0.25, itemEntity.Position.x);
            Assert.AreEqual(defaultY, itemEntity.Position.y);
            Assert.AreEqual(0.5, itemEntity.Position.z);
            
            
            //x、yがマイナスであるときのテスト
            //北向きに設
            worldDataStore.RemoveBlock(new Vector3Int(0, 0, 0), BlockRemoveReason.ManualRemove);
            CreateOneItemInsertedItem(new Vector3Int(-1, 0, -1), BlockDirection.North, worldDataStore);
            
            //エンティティを取得
            itemEntity = CollectBeltConveyorItems.CollectItemFromWorld(entityFactory, Vector3.zero, float.MaxValue)[0];
            //座標を検証
            Assert.AreEqual(-0.5, itemEntity.Position.x);
            Assert.AreEqual(defaultY, itemEntity.Position.y);
            Assert.AreEqual(-0.25, itemEntity.Position.z); //ブロックの座標がマイナスなので-1を原点として3/4の値である-0.25となる
            
            
            //東向きに設置
            worldDataStore.RemoveBlock(new Vector3Int(-1, 0, -1), BlockRemoveReason.ManualRemove);
            CreateOneItemInsertedItem(new Vector3Int(-1, 0, -1), BlockDirection.East, worldDataStore);
            itemEntity = CollectBeltConveyorItems.CollectItemFromWorld(entityFactory, Vector3.zero, float.MaxValue)[0];
            //座標を検証
            Assert.AreEqual(-0.25, itemEntity.Position.x);
            Assert.AreEqual(defaultY, itemEntity.Position.y);
            Assert.AreEqual(-0.5, itemEntity.Position.z);
            
            
            //ブロックを削除し南向きに設置
            worldDataStore.RemoveBlock(new Vector3Int(-1, 0, -1), BlockRemoveReason.ManualRemove);
            CreateOneItemInsertedItem(new Vector3Int(-1, 0, -1), BlockDirection.South, worldDataStore);
            itemEntity = CollectBeltConveyorItems.CollectItemFromWorld(entityFactory, Vector3.zero, float.MaxValue)[0];
            //座標を検証
            Assert.AreEqual(-0.5, itemEntity.Position.x);
            Assert.AreEqual(defaultY, itemEntity.Position.y);
            Assert.AreEqual(-0.75, itemEntity.Position.z);
            
            
            //ブロックを削除し西向きに設置
            worldDataStore.RemoveBlock(new Vector3Int(-1, 0, -1), BlockRemoveReason.ManualRemove);
            CreateOneItemInsertedItem(new Vector3Int(-1, 0, -1), BlockDirection.West, worldDataStore);
            itemEntity = CollectBeltConveyorItems.CollectItemFromWorld(entityFactory, Vector3.zero, float.MaxValue)[0];
            //ブロックを削除し座標を検証
            Assert.AreEqual(-0.75, itemEntity.Position.x);
            Assert.AreEqual(defaultY, itemEntity.Position.y);
            Assert.AreEqual(-0.5, itemEntity.Position.z);
        }
        
        
        /// <summary>
        ///     ベルトコンベアから別のベルトコンベアに移ってもInstanceIdは変化しないことをテスト
        /// </summary>
        [Test]
        public void ItemInstanceIdTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            var worldDataStore = ServerContext.WorldBlockDatastore;
            
            worldDataStore.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var belt2);
            //二つのベルトコンベアを繋がるように設置
            
            var belt1 = CreateOneItemInsertedItem(new Vector3Int(0, 0, 0), BlockDirection.North, worldDataStore);
            
            // tick数でアップデートする（RemainingTime * 1.1秒間）
            // Update for RemainingTime * 1.1 seconds (controlled by tick count)
            var updateTicks = (int)(RemainingTime * 1.1 * GameUpdater.TicksPerSecond);
            for (var i = 0; i < updateTicks; i++) GameUpdater.AdvanceTicks(1);
            
            //ベルトコンベアからアイテムを取得
            var inventoryItemsField = typeof(VanillaBeltConveyorComponent).GetField("_inventoryItems", BindingFlags.NonPublic | BindingFlags.Instance);
            var inventory2Items = (VanillaBeltConveyorInventoryItem[])inventoryItemsField.GetValue(belt2.GetComponent<VanillaBeltConveyorComponent>());
            
            //InstanceIdが変化していないことを検証
            Assert.AreEqual(ItemInstanceId, inventory2Items[3].ItemInstanceId.AsPrimitive());
        }

        /// <summary>
        /// ベルト上アイテムのStartConnector/GoalConnectorがエンティティデータにConnectorGuidとして含まれることをテスト
        /// Test that StartConnector/GoalConnector of belt item are included as ConnectorGuid in entity data
        /// </summary>
        [Test]
        public void ConnectorGuidInEntityDataTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldDataStore = ServerContext.WorldBlockDatastore;
            var entityFactory = serviceProvider.GetService<IEntityFactory>();

            // 固定GuidでConnectorを作成
            // Create connectors with fixed Guids
            var sourceGuid = Guid.NewGuid();
            var goalGuid = Guid.NewGuid();
            var sourceConnector = new BlockConnectInfoElement(0, "Inventory", sourceGuid, Vector3Int.zero, Array.Empty<Vector3Int>(), null);
            var goalConnector = new BlockConnectInfoElement(1, "Inventory", goalGuid, Vector3Int.zero, Array.Empty<Vector3Int>(), null);

            // ベルトコンベアを作成してアイテムを設定
            // Create belt conveyor and set item
            var beltConveyor = CreateOneItemInsertedItemWithConnectors(new Vector3Int(0, 0, 0), BlockDirection.North, worldDataStore, sourceConnector, goalConnector);

            // エンティティを収集
            // Collect entities
            var entities = CollectBeltConveyorItems.CollectItemFromWorld(entityFactory, Vector3.zero, float.MaxValue);
            Assert.AreEqual(1, entities.Count);

            // エンティティデータをデシリアライズしてConnectorGuidを検証
            // Deserialize entity data and verify ConnectorGuid
            var entityData = entities[0].GetEntityData();
            var messagePack = MessagePackSerializer.Deserialize<BeltConveyorItemEntityStateMessagePack>(entityData);

            Assert.IsNotNull(messagePack.SourceConnectorGuid, "SourceConnectorGuidはnullであるべきではない / SourceConnectorGuid should not be null");
            Assert.IsNotNull(messagePack.GoalConnectorGuid, "GoalConnectorGuidはnullであるべきではない / GoalConnectorGuid should not be null");
            Assert.AreEqual(sourceGuid, messagePack.SourceConnectorGuid.Value, "SourceConnectorGuidが一致すべき / SourceConnectorGuid should match");
            Assert.AreEqual(goalGuid, messagePack.GoalConnectorGuid.Value, "GoalConnectorGuidが一致すべき / GoalConnectorGuid should match");
            
            // 進捗割合とブロック座標が送られることを検証
            // Verify progress ratio and block position are sent
            Assert.AreEqual(0.75f, messagePack.RemainingPercent, 0.0001f, "RemainingPercentが進捗割合として一致すべき / RemainingPercent should match progress ratio");
            Assert.AreEqual(0, messagePack.BlockPosX, "BlockPosXが一致すべき / BlockPosX should match");
            Assert.AreEqual(0, messagePack.BlockPosY, "BlockPosYが一致すべき / BlockPosY should match");
            Assert.AreEqual(0, messagePack.BlockPosZ, "BlockPosZが一致すべき / BlockPosZ should match");
        }

        /// <summary>
        /// StartConnector/GoalConnectorがnullの場合、ConnectorGuidもnullになることをテスト
        /// Test that when StartConnector/GoalConnector are null, ConnectorGuid is also null
        /// </summary>
        [Test]
        public void NullConnectorGuidInEntityDataTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldDataStore = ServerContext.WorldBlockDatastore;
            var entityFactory = serviceProvider.GetService<IEntityFactory>();

            // コネクターなしでベルトコンベアを作成
            // Create belt conveyor without connectors
            CreateOneItemInsertedItem(new Vector3Int(0, 0, 0), BlockDirection.North, worldDataStore);

            // エンティティを収集
            // Collect entities
            var entities = CollectBeltConveyorItems.CollectItemFromWorld(entityFactory, Vector3.zero, float.MaxValue);
            Assert.AreEqual(1, entities.Count);

            // エンティティデータをデシリアライズしてConnectorGuidがnullであることを検証
            // Deserialize entity data and verify ConnectorGuid is null
            var entityData = entities[0].GetEntityData();
            var messagePack = MessagePackSerializer.Deserialize<BeltConveyorItemEntityStateMessagePack>(entityData);

            Assert.IsNull(messagePack.SourceConnectorGuid, "SourceConnectorGuidはnullであるべき / SourceConnectorGuid should be null");
            Assert.IsNull(messagePack.GoalConnectorGuid, "GoalConnectorGuidはnullであるべき / GoalConnectorGuid should be null");
        }


        private IBlock CreateOneItemInsertedItem(Vector3Int pos, BlockDirection blockDirection, IWorldBlockDatastore datastore)
        {
            datastore.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, pos, blockDirection, Array.Empty<BlockCreateParam>(), out var beltConveyor);
            var beltConveyorComponent = beltConveyor.GetComponent<VanillaBeltConveyorComponent>();

            // リフレクションで_inventoryItemsと_ticksOfItemEnterToExitを取得
            // Get _inventoryItems and _ticksOfItemEnterToExit via reflection
            var inventoryItemsField = typeof(VanillaBeltConveyorComponent).GetField("_inventoryItems", BindingFlags.NonPublic | BindingFlags.Instance);
            var inventoryItems = (VanillaBeltConveyorInventoryItem[])inventoryItemsField.GetValue(beltConveyorComponent);
            var ticksField = typeof(VanillaBeltConveyorComponent).GetField("_ticksOfItemEnterToExit", BindingFlags.NonPublic | BindingFlags.Instance);
            var totalTicks = (uint)ticksField.GetValue(beltConveyorComponent);

            // 残り25%（進捗75%）を設定
            // Set 25% remaining (75% progress)
            var remainingTicks = (uint)(totalTicks * 0.25);
            inventoryItems[0] = new VanillaBeltConveyorInventoryItem(new ItemId(1), new ItemInstanceId(ItemInstanceId), null, null, totalTicks)
            {
                RemainingTicks = remainingTicks
            };
            inventoryItems[1] = null;
            inventoryItems[2] = null;
            inventoryItems[3] = null;

            return beltConveyor;
        }

        private IBlock CreateOneItemInsertedItemWithConnectors(Vector3Int pos, BlockDirection blockDirection, IWorldBlockDatastore datastore, BlockConnectInfoElement startConnector, BlockConnectInfoElement goalConnector)
        {
            datastore.TryAddBlock(ForUnitTestModBlockId.BeltConveyorId, pos, blockDirection, Array.Empty<BlockCreateParam>(), out var beltConveyor);
            var beltConveyorComponent = beltConveyor.GetComponent<VanillaBeltConveyorComponent>();

            // リフレクションで_inventoryItemsと_ticksOfItemEnterToExitを取得
            // Get _inventoryItems and _ticksOfItemEnterToExit via reflection
            var inventoryItemsField = typeof(VanillaBeltConveyorComponent).GetField("_inventoryItems", BindingFlags.NonPublic | BindingFlags.Instance);
            var inventoryItems = (VanillaBeltConveyorInventoryItem[])inventoryItemsField.GetValue(beltConveyorComponent);
            var ticksField = typeof(VanillaBeltConveyorComponent).GetField("_ticksOfItemEnterToExit", BindingFlags.NonPublic | BindingFlags.Instance);
            var totalTicks = (uint)ticksField.GetValue(beltConveyorComponent);

            // 残り25%（進捗75%）を設定
            // Set 25% remaining (75% progress)
            var remainingTicks = (uint)(totalTicks * 0.25);
            inventoryItems[0] = new VanillaBeltConveyorInventoryItem(new ItemId(1), new ItemInstanceId(ItemInstanceId), startConnector, goalConnector, totalTicks)
            {
                RemainingTicks = remainingTicks
            };
            inventoryItems[1] = null;
            inventoryItems[2] = null;
            inventoryItems[3] = null;

            return beltConveyor;
        }
    }
}
