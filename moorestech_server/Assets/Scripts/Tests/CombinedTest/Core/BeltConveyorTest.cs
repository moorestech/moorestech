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
using Game.Gear.Common;
using Mooresmaster.Model.BlockConnectInfoModule;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module;
using Tests.Module.TestMod;
using UnityEngine;
using Random = System.Random;

namespace Tests.CombinedTest.Core
{
    /// <summary>
    ///     コンフィグが変わったらこのテストを変更に応じて変更してください
    /// </summary>
    public class BeltConveyorTest
    {
        //一定個数以上アイテムが入らないテストした後、正しく次に出力されるかのテスト
        [Test]
        public void FullInsertAndChangeConnectorBeltConveyorTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            var beltConveyorParam = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BeltConveyorId).BlockParam as BeltConveyorBlockParam;
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            var random = new Random(4123);
            for (var i = 0; i < 2; i++) //あまり深い意味はないが取りあえずテストは2回実行する
            {
                var id = new ItemId(random.Next(0, 10));
                
                var item = itemStackFactory.Create(id, beltConveyorParam.BeltConveyorItemCount + 1);
                var beltConveyor = ServerContext.BlockFactory.Create(ForUnitTestModBlockId.BeltConveyorId, new BlockInstanceId(int.MaxValue), new BlockPositionInfo(Vector3Int.one, BlockDirection.North, Vector3Int.one));
                var beltConveyorComponent = beltConveyor.GetComponent<VanillaBeltConveyorComponent>();
                
                var endTime = DateTime.Now.AddSeconds(beltConveyorParam.TimeOfItemEnterToExit);
                
                while (DateTime.Now < endTime.AddSeconds(0.1))
                {
                    item = beltConveyorComponent.InsertItem(item, InsertItemContext.Empty);
                    GameUpdater.UpdateOneTick();
                }
                
                Assert.AreEqual(item.Count, 1);
                
                var dummy = new DummyBlockInventory();
                
                var connectInventory = (Dictionary<IBlockInventory, ConnectedInfo>)beltConveyor.GetComponent<BlockConnectorComponent<IBlockInventory>>().ConnectedTargets;
                connectInventory.Add(dummy, new ConnectedInfo());
                GameUpdater.UpdateOneTick();
                
                Assert.AreEqual(itemStackFactory.Create(id, 1).ToString(), dummy.InsertedItems[0].ToString());
            }
        }
        
        //一個のアイテムが入って正しく搬出されるかのテスト
        [Test]
        public void InsertBeltConveyorTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            var beltConveyorParam = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BeltConveyorId).BlockParam as BeltConveyorBlockParam;
            var blockFactory = ServerContext.BlockFactory;
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            
            var id = new ItemId(2);
            const int count = 3;
            var item = itemStackFactory.Create(id, count);
            var dummy = new DummyBlockInventory();
            
            // アイテムを挿入
            var beltConveyor = blockFactory.Create(ForUnitTestModBlockId.BeltConveyorId, new BlockInstanceId(int.MaxValue), new BlockPositionInfo(Vector3Int.one, BlockDirection.North, Vector3Int.one));
            var beltConveyorComponent = beltConveyor.GetComponent<VanillaBeltConveyorComponent>();
            
            var connectInventory = (Dictionary<IBlockInventory, ConnectedInfo>)beltConveyor.GetComponent<BlockConnectorComponent<IBlockInventory>>().ConnectedTargets;
            connectInventory.Add(dummy, new ConnectedInfo());
            
            // 期待されるtick数を計算
            // Calculate expected tick count
            var expectedTicks = (int)(beltConveyorParam.TimeOfItemEnterToExit * GameUpdater.TicksPerSecond);
            var outputItem = beltConveyorComponent.InsertItem(item, InsertItemContext.Empty);

            // tick数でループ制御（タイムアウト付き）
            // Loop controlled by tick count (with timeout)
            var elapsedTicks = 0;
            var maxTicks = expectedTicks + 10; // 余裕を持たせる
            while (!dummy.IsItemExists && elapsedTicks < maxTicks)
            {
                GameUpdater.AdvanceTicks(1);
                elapsedTicks++;
            }

            // 期待したtick数近辺でアイテムが到達したことを確認
            // Verify item arrived around expected tick count
            Assert.True(dummy.IsItemExists, "Item should have been output");
            Assert.True(elapsedTicks <= expectedTicks + 2 && elapsedTicks >= expectedTicks - 2, $"Item should arrive around expected tick count. Expected: {expectedTicks}, Actual: {elapsedTicks}");

            Debug.Log($"Expected ticks: {expectedTicks}, Elapsed ticks: {elapsedTicks}");
            
            Assert.True(outputItem.Equals(itemStackFactory.Create(id, count - 1)));
            var tmp = itemStackFactory.Create(id, 1);
            Debug.Log($"{tmp} {dummy.InsertedItems[0]}");
            Assert.AreEqual(tmp.ToString(), dummy.InsertedItems[0].ToString());
        }
        
        //ベルトコンベアのインベントリをフルにするテスト
        [Test]
        public void FullInsertBeltConveyorTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            var beltConveyorParam = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BeltConveyorId).BlockParam as BeltConveyorBlockParam;
            var blockFactory = ServerContext.BlockFactory;
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            var random = new Random(4123);
            
            var id = new ItemId(random.Next(1, 11));
            var item = itemStackFactory.Create(id, beltConveyorParam.BeltConveyorItemCount + 1);
            var dummy = new DummyBlockInventory(beltConveyorParam.BeltConveyorItemCount);
            var beltConveyor = blockFactory.Create(ForUnitTestModBlockId.BeltConveyorId, new BlockInstanceId(int.MaxValue), new BlockPositionInfo(Vector3Int.one, BlockDirection.North, Vector3Int.one));
            var beltConveyorComponent = beltConveyor.GetComponent<VanillaBeltConveyorComponent>();
            
            var connectInventory = (Dictionary<IBlockInventory, ConnectedInfo>)beltConveyor.GetComponent<BlockConnectorComponent<IBlockInventory>>().ConnectedTargets;
            connectInventory.Add(dummy, new ConnectedInfo());
            
            while (!dummy.IsItemExists)
            {
                item = beltConveyorComponent.InsertItem(item, InsertItemContext.Empty);
                GameUpdater.UpdateOneTick();
            }
            
            Assert.True(item.Equals(itemStackFactory.Create(id, 0)));
            var tmp = itemStackFactory.Create(id, beltConveyorParam.BeltConveyorItemCount);
            Assert.True(dummy.InsertedItems[0].Equals(tmp));
        }
        
        //二つのアイテムが入ったとき、一方しか入らないテスト
        [Test]
        public void Insert2ItemBeltConveyorTest()
        {
            var (_, serviceProvider) =
                new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var blockFactory = ServerContext.BlockFactory;
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            var random = new Random(4123);
            for (var i = 0; i < 2; i++) //あまり深い意味はないが取りあえずテストは2回実行する
            {
                //必要な変数を作成
                var item1 = itemStackFactory.Create(new ItemId(random.Next(1, 11)), random.Next(1, 10));
                var item2 = itemStackFactory.Create(new ItemId(random.Next(1, 11)), random.Next(1, 10));
                
                var beltConveyor = blockFactory.Create(ForUnitTestModBlockId.BeltConveyorId , new BlockInstanceId(int.MaxValue), new BlockPositionInfo(Vector3Int.one, BlockDirection.North, Vector3Int.one));
                var beltConveyorComponent = beltConveyor.GetComponent<VanillaBeltConveyorComponent>();
                
                var item1Out = beltConveyorComponent.InsertItem(item1, InsertItemContext.Empty);
                var item2Out = beltConveyorComponent.InsertItem(item2, InsertItemContext.Empty);
                
                Assert.True(item1Out.Equals(item1.SubItem(1)));
                Assert.True(item2Out.Equals(item2));
            }
        }

        // 歯車ベルトコンベアスプリッタが2方向に分配できるかのテスト
        [Test]
        public void GearBeltConveyorSplitterDistributesToTwoChestsTest()
        {
            // テスト環境を初期化する
            // Initialize test environment
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var itemStackFactory = ServerContext.ItemStackFactory;

            // スプリッター本体とチェストを配置する
            // Place splitter and chests
            var splitterPosition = Vector3Int.zero;
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearBeltConveyorSplitter, splitterPosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var splitterBlock);
            var gearBeltConveyorComponent = splitterBlock.GetComponent<GearBeltConveyorComponent>();
            var sourceChestPosition = new Vector3Int(0, 0, -1);
            var outputChestPositionA = new Vector3Int(0, 0, 1);
            var outputChestPositionB = new Vector3Int(-1, 0, 0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, sourceChestPosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var sourceChestBlock);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, outputChestPositionA, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var outputChestBlockA);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, outputChestPositionB, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var outputChestBlockB);
            var sourceChest = sourceChestBlock.GetComponent<VanillaChestComponent>();
            var outputChestA = outputChestBlockA.GetComponent<VanillaChestComponent>();
            var outputChestB = outputChestBlockB.GetComponent<VanillaChestComponent>();

            // 歯車ネットワークを構築して搬送を有効化する
            // Build gear network to enable transport
            var generatorPosition = new Vector3Int(1, 0, 0);
            var gearPosition = new Vector3Int(2, 0, 0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.InfinityTorqueSimpleGearGenerator, generatorPosition, BlockDirection.East, Array.Empty<BlockCreateParam>(), out var generatorBlock);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, gearPosition, BlockDirection.East, Array.Empty<BlockCreateParam>(), out var gearBlock);

            // 入力チェストにアイテムを投入する
            // Insert items into source chest
            var itemId = new ItemId(1);
            const int itemCount = 40;
            var itemStack = itemStackFactory.Create(itemId, itemCount);
            sourceChest.SetItem(0, itemStack);

            // 両チェストへの分配完了を待つ
            // Wait for distribution to both chests
            var splitterParam = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearBeltConveyorSplitter).BlockParam as GearBeltConveyorBlockParam;
            var startTime = DateTime.Now;
            var timeoutTime = startTime.AddSeconds(20);
            while (DateTime.Now <= timeoutTime && !IsDistributed(outputChestA, outputChestB, itemId, itemCount))
            {
                // スプリッターに歯車エネルギーを供給する
                // Supply gear energy to splitter
                gearBeltConveyorComponent.SupplyPower(new RPM(20), new Torque(splitterParam.RequireTorque), true);
                GameUpdater.UpdateOneTick();
            }
            
            Assert.AreEqual(itemCount / 2, GetItemCount(outputChestB, itemId));
            Assert.AreEqual(itemCount / 2, GetItemCount(outputChestA, itemId));

            #region Internal

            int GetItemCount(VanillaChestComponent chest, ItemId targetItemId)
            {
                // 対象アイテムの合計数を集計する
                // Aggregate total count for target item
                var total = 0;
                foreach (var stack in chest.InventoryItems)
                {
                    if (stack.Id != targetItemId) continue;
                    total += stack.Count;
                }
                return total;
            }

            bool IsDistributed(VanillaChestComponent chestLeft, VanillaChestComponent chestRight, ItemId targetItemId, int insertCount)
            {
                // 左右のチェストが必要数を受け取ったか確認する
                // Check if both chests received required count
                var chestCount = insertCount / 2;
                return GetItemCount(chestLeft, targetItemId) >= chestCount && GetItemCount(chestRight, targetItemId) >= chestCount;
            }

            #endregion
        }
    }
}
