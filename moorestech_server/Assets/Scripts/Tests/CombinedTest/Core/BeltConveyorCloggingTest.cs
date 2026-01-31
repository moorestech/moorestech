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

namespace Tests.CombinedTest.Core
{
    public class BeltConveyorCloggingTest
    {
        [Test]
        public void InsertRejectedWhenSingleOutputBlockedTest()
        {
            // 依存関係を初期化する
            // Initialize dependencies
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;

            // ベルトと詰まり先を接続する
            // Connect belt and blocked output
            var (beltConveyorComponent, connectedTargets) = CreateBeltConveyor();
            var blockedInventory = new ConfigurableBlockInventory(1, 1, false, true);
            AddTarget(connectedTargets, blockedInventory, 0);

            // 挿入が拒否されることを確認する
            // Verify insertion is rejected
            var item = itemStackFactory.Create(new ItemId(1), 1);
            var output = beltConveyorComponent.InsertItem(item, InsertItemContext.Empty);
            Assert.True(output.Equals(item));
            Assert.AreEqual(0, blockedInventory.GetInsertedItemCount());
        }

        [Test]
        public void InsertAcceptedWhenOneOutputAvailableTest()
        {
            // 依存関係とベルトを初期化する
            // Initialize dependencies and belt
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;
            var beltConveyorParam = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BeltConveyorId).BlockParam as BeltConveyorBlockParam;

            // 詰まり先と空き先を接続する
            // Connect blocked and available outputs
            var (beltConveyorComponent, connectedTargets) = CreateBeltConveyor();
            var blockedInventory = new ConfigurableBlockInventory(1, 1, false, true);
            var openInventory = new ConfigurableBlockInventory(1, 10, true, false);
            AddTarget(connectedTargets, blockedInventory, 0);
            AddTarget(connectedTargets, openInventory, 1);

            // 挿入と搬出が成功することを確認する
            // Verify insertion and output success
            var item = itemStackFactory.Create(new ItemId(2), 1);
            var output = beltConveyorComponent.InsertItem(item, InsertItemContext.Empty);
            Assert.True(output.Equals(itemStackFactory.CreatEmpty()));
            UpdateUntil(() => openInventory.GetInsertedItemCount() == 1, TimeSpan.FromSeconds(beltConveyorParam.TimeOfItemEnterToExit * 1.5));
            Assert.AreEqual(0, blockedInventory.GetInsertedItemCount());
        }

        [Test]
        public void InsertRejectedWhenAllOutputsBlockedTest()
        {
            // 依存関係とベルトを初期化する
            // Initialize dependencies and belt
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;

            // 全接続先が詰まっている状態を作る
            // Set all outputs to blocked
            var (beltConveyorComponent, connectedTargets) = CreateBeltConveyor();
            var blockedInventoryA = new ConfigurableBlockInventory(1, 1, false, true);
            var blockedInventoryB = new ConfigurableBlockInventory(1, 1, false, true);
            AddTarget(connectedTargets, blockedInventoryA, 0);
            AddTarget(connectedTargets, blockedInventoryB, 1);

            // 挿入が拒否されることを確認する
            // Verify insertion is rejected
            var item = itemStackFactory.Create(new ItemId(3), 1);
            var output = beltConveyorComponent.InsertItem(item, InsertItemContext.Empty);
            Assert.True(output.Equals(item));
            Assert.AreEqual(0, blockedInventoryA.GetInsertedItemCount());
            Assert.AreEqual(0, blockedInventoryB.GetInsertedItemCount());
        }

        [Test]
        public void OutputRerouteWhenGoalBlockedAtOutputTimeTest()
        {
            // 依存関係とベルトを初期化する
            // Initialize dependencies and belt
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;
            var beltConveyorParam = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BeltConveyorId).BlockParam as BeltConveyorBlockParam;

            // 目標先は受け入れ可と判定しつつ搬出で拒否させる
            // Make goal pass InsertionCheck but reject on output
            var (beltConveyorComponent, connectedTargets) = CreateBeltConveyor();
            var blockedOnOutputInventory = new ConfigurableBlockInventory(1, 10, true, true);
            var fallbackInventory = new ConfigurableBlockInventory(1, 10, false, false);
            AddTarget(connectedTargets, blockedOnOutputInventory, 0);
            AddTarget(connectedTargets, fallbackInventory, 1);

            // 挿入後に受け入れ先を有効化し、リルートを確認する
            // Enable fallback after insert and verify reroute
            var item = itemStackFactory.Create(new ItemId(4), 1);
            var output = beltConveyorComponent.InsertItem(item, InsertItemContext.Empty);
            fallbackInventory.SetAllowInsertionCheck(true);
            Assert.True(output.Equals(itemStackFactory.CreatEmpty()));
            UpdateUntil(() => fallbackInventory.GetInsertedItemCount() == 1, TimeSpan.FromSeconds(beltConveyorParam.TimeOfItemEnterToExit * 1.5));
            Assert.AreEqual(0, blockedOnOutputInventory.GetInsertedItemCount());
        }

        [Test]
        public void InsertAllowedAfterBlockedBecomesAvailableTest()
        {
            // 依存関係とベルトを初期化する
            // Initialize dependencies and belt
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;
            var beltConveyorParam = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BeltConveyorId).BlockParam as BeltConveyorBlockParam;

            // 最初は挿入不可、後で挿入可能にする
            // Start blocked and allow insertion later
            var (beltConveyorComponent, connectedTargets) = CreateBeltConveyor();
            var dynamicInventory = new ConfigurableBlockInventory(1, 10, false, false);
            AddTarget(connectedTargets, dynamicInventory, 0);

            // 初回は拒否され、二回目で通ることを確認する
            // Verify first insert rejected and second accepted
            var item = itemStackFactory.Create(new ItemId(5), 1);
            var outputRejected = beltConveyorComponent.InsertItem(item, InsertItemContext.Empty);
            dynamicInventory.SetAllowInsertionCheck(true);
            var outputAccepted = beltConveyorComponent.InsertItem(item, InsertItemContext.Empty);
            Assert.True(outputRejected.Equals(item));
            Assert.True(outputAccepted.Equals(itemStackFactory.CreatEmpty()));
            UpdateUntil(() => dynamicInventory.GetInsertedItemCount() == 1, TimeSpan.FromSeconds(beltConveyorParam.TimeOfItemEnterToExit * 1.5));
        }

        [Test]
        public void RoundRobinSkipsBlockedDestinationTest()
        {
            // 依存関係とベルトを初期化する
            // Initialize dependencies and belt
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;
            var beltConveyorParam = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BeltConveyorId).BlockParam as BeltConveyorBlockParam;

            // 一つだけ詰まりを混ぜて接続する
            // Connect with one blocked output
            var (beltConveyorComponent, connectedTargets) = CreateBeltConveyor();
            var blockedInventory = new ConfigurableBlockInventory(1, 1, false, true);
            var openInventoryA = new ConfigurableBlockInventory(1, 10, true, false);
            var openInventoryB = new ConfigurableBlockInventory(1, 10, true, false);
            AddTarget(connectedTargets, blockedInventory, 0);
            AddTarget(connectedTargets, openInventoryA, 1);
            AddTarget(connectedTargets, openInventoryB, 2);

            // 2回挿入して詰まり先が選ばれないことを確認する
            // Insert twice and confirm blocked output is skipped
            var firstOutput = beltConveyorComponent.InsertItem(itemStackFactory.Create(new ItemId(6), 1), InsertItemContext.Empty);
            UpdateUntil(() => openInventoryA.GetInsertedItemCount() + openInventoryB.GetInsertedItemCount() >= 1, TimeSpan.FromSeconds(beltConveyorParam.TimeOfItemEnterToExit * 1.5));
            var secondOutput = beltConveyorComponent.InsertItem(itemStackFactory.Create(new ItemId(7), 1), InsertItemContext.Empty);
            UpdateUntil(() => openInventoryA.GetInsertedItemCount() + openInventoryB.GetInsertedItemCount() >= 2, TimeSpan.FromSeconds(beltConveyorParam.TimeOfItemEnterToExit * 2.5));
            Assert.True(firstOutput.Equals(itemStackFactory.CreatEmpty()));
            Assert.True(secondOutput.Equals(itemStackFactory.CreatEmpty()));
            Assert.AreEqual(0, blockedInventory.GetInsertedItemCount());
            Assert.AreEqual(1, openInventoryA.GetInsertedItemCount());
            Assert.AreEqual(1, openInventoryB.GetInsertedItemCount());
        }

        [Test]
        public void ContinuousFlowDoesNotStopWithPartialBlockageTest()
        {
            // 依存関係とベルトを初期化する
            // Initialize dependencies and belt
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;
            var beltConveyorParam = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BeltConveyorId).BlockParam as BeltConveyorBlockParam;

            // 詰まり先と空き先を接続する
            // Connect blocked and available outputs
            var (beltConveyorComponent, connectedTargets) = CreateBeltConveyor();
            var blockedInventory = new ConfigurableBlockInventory(1, 1, false, true);
            var openInventory = new ConfigurableBlockInventory(1, 10, true, false);
            AddTarget(connectedTargets, blockedInventory, 0);
            AddTarget(connectedTargets, openInventory, 1);

            // 連続投入でも停止しないことを確認する
            // Verify continuous flow without stopping
            for (var i = 0; i < 3; i++)
            {
                var output = beltConveyorComponent.InsertItem(itemStackFactory.Create(new ItemId(8), 1), InsertItemContext.Empty);
                Assert.True(output.Equals(itemStackFactory.CreatEmpty()));
            }
            UpdateUntil(() => openInventory.GetInsertedItemCount() == 3, TimeSpan.FromSeconds(beltConveyorParam.TimeOfItemEnterToExit * 3.5));
            Assert.AreEqual(0, blockedInventory.GetInsertedItemCount());
        }

        [Test]
        public void GoalConnectorFallbackWhenDisconnectedTest()
        {
            // 依存関係とベルトを初期化する
            // Initialize dependencies and belt
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var beltConveyorParam = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BeltConveyorId).BlockParam as BeltConveyorBlockParam;
            var itemStackFactory = ServerContext.ItemStackFactory;

            // 2つの接続先を作成してGoalConnectorを保持する
            // Create two outputs and keep GoalConnector
            var (beltConveyorComponent, connectedTargets) = CreateBeltConveyor();
            beltConveyorComponent.SetTicksOfItemEnterToExit(GameUpdater.SecondsToTicks(beltConveyorParam.TimeOfItemEnterToExit * 10));
            var openInventoryA = new ConfigurableBlockInventory(1, 10, true, false);
            var openInventoryB = new ConfigurableBlockInventory(1, 10, true, false);
            var connectorA = AddTarget(connectedTargets, openInventoryA, 0);
            var connectorB = AddTarget(connectedTargets, openInventoryB, 1);
            var output = beltConveyorComponent.InsertItem(itemStackFactory.Create(new ItemId(9), 1), InsertItemContext.Empty);
            Assert.True(output.Equals(itemStackFactory.CreatEmpty()));

            // 接続を外してGoalConnectorが更新されることを確認する
            // Remove connection and verify GoalConnector updates
            connectedTargets.Remove(openInventoryA);
            GameUpdater.UpdateWithWait();
            var beltItem = beltConveyorComponent.BeltConveyorItems[^1];
            Assert.AreEqual(connectorB.ConnectorGuid, beltItem.GoalConnector.ConnectorGuid);
        }

        [TestCase("BeltConveyorId")]
        [TestCase("GearBeltConveyor")]
        public void ItemsKeepSpacingWhenCloggedTest(string blockIdName)
        {
            // テストを実行する（blockIdの取得はDI初期化後に行う）
            // Execute test (blockId retrieval is done after DI initialization)
            ExecuteItemSpacingTest(blockIdName, beltCount: 4, expectedItemCount: 16);
        }

        private void ExecuteItemSpacingTest(string blockIdName, int beltCount, int expectedItemCount)
        {
            // 依存関係を初期化する
            // Initialize dependencies
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var itemStackFactory = ServerContext.ItemStackFactory;

            // DI初期化後にBlockIdを取得する
            // Get BlockId after DI initialization
            var beltConveyorBlockId = GetBlockIdByName(blockIdName);
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(beltConveyorBlockId);
            var isGearBeltConveyor = blockMaster.BlockType == "GearBeltConveyor";

            #region Internal - GetBlockIdByName

            BlockId GetBlockIdByName(string name)
            {
                return name switch
                {
                    "BeltConveyorId" => ForUnitTestModBlockId.BeltConveyorId,
                    "GearBeltConveyor" => ForUnitTestModBlockId.GearBeltConveyor,
                    _ => throw new ArgumentException($"Unknown block ID name: {name}")
                };
            }

            #endregion

            // ベルトコンベアを配置する
            // Place belt conveyors
            var beltComponents = new List<VanillaBeltConveyorComponent>();
            var beltBlocks = new List<IBlock>();
            for (var i = 0; i < beltCount; i++)
            {
                var beltPosition = new Vector3Int(0, 0, i);
                worldBlockDatastore.TryAddBlock(beltConveyorBlockId, beltPosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var beltBlock);
                var beltComponent = beltBlock.GetComponent<VanillaBeltConveyorComponent>();
                beltComponents.Add(beltComponent);
                beltBlocks.Add(beltBlock);
            }

            // 歯車ベルトコンベアの場合は各ベルトに無限トルクジェネレータを配置する
            // Place infinite torque generator next to each gear belt conveyor
            GearBeltConveyorBlockParam gearBeltParam = null;
            if (isGearBeltConveyor)
            {
                for (var i = 0; i < beltCount; i++)
                {
                    var generatorPosition = new Vector3Int(1, 0, i);
                    worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.InfinityTorqueSimpleGearGenerator, generatorPosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
                }
                gearBeltParam = blockMaster.BlockParam as GearBeltConveyorBlockParam;
            }

            // 最後のベルトコンベアの出力先として詰まるインベントリを設定する
            // Set blocked inventory as output of last belt conveyor
            var lastBelt = beltComponents[^1];
            var blockedInventory = new ConfigurableBlockInventory(1, 10, true, true);
            var lastBeltBlock = worldBlockDatastore.GetBlock(new Vector3Int(0, 0, beltCount - 1));
            var connectedTargets = (Dictionary<IBlockInventory, ConnectedInfo>)lastBeltBlock.GetComponent<BlockConnectorComponent<IBlockInventory>>().ConnectedTargets;
            connectedTargets.Clear();
            AddTarget(connectedTargets, blockedInventory, 0);

            // タイムアウト時間を計算する（ブロックタイプに応じて異なる）
            // Calculate timeout based on block type
            var transportTime = CalculateTransportTime(blockMaster, isGearBeltConveyor);

            // 最初のベルトコンベアにアイテムを挿入する
            // Insert items into first belt conveyor
            var itemId = new ItemId(1);
            var firstBelt = beltComponents[0];
            var insertedCount = 0;

            // 歯車ベルトコンベアの場合、搬送時間を初期化する
            // Initialize gear belt conveyor transport time before inserting items
            if (isGearBeltConveyor)
            {
                for (var i = 0; i < 5; i++)
                {
                    SupplyPowerIfNeeded();
                    GameUpdater.UpdateWithWait();
                }
            }

            // すべてのアイテムを挿入し、ベルトコンベアで移動させる
            // Insert all items and transport on belt conveyor
            var timeout = TimeSpan.FromSeconds(transportTime * beltCount * 10);
            var startTime = DateTime.Now;
            while (insertedCount < expectedItemCount && DateTime.Now - startTime < timeout)
            {
                // 歯車ベルトコンベアの場合は電力を供給する
                // Supply power for gear belt conveyor
                SupplyPowerIfNeeded();

                // アイテムを挿入を試みる
                // Try to insert item
                var item = itemStackFactory.Create(itemId, 1);
                var remaining = firstBelt.InsertItem(item, InsertItemContext.Empty);
                if (remaining.Count == 0)
                {
                    insertedCount++;
                }

                GameUpdater.UpdateWithWait();
            }

            // 挿入されたアイテム数で検証を行う
            // Use actual inserted count for verification
            var actualExpectedItems = insertedCount;

            // 全アイテムが詰まるまで待つ
            // Wait until all items are clogged
            var clogTimeout = TimeSpan.FromSeconds(transportTime * beltCount * 5);
            UpdateUntilWithPowerLocal(() =>
            {
                var totalItemCount = 0;
                foreach (var belt in beltComponents)
                {
                    totalItemCount += System.Linq.Enumerable.Count(belt.BeltConveyorItems, x => x != null);
                }
                var frontItem = beltComponents[^1].BeltConveyorItems[0];
                return totalItemCount == actualExpectedItems && frontItem != null && frontItem.RemainingTicks == 0;
            }, clogTimeout);

            // 期待したアイテム数がベルトに乗っていることを確認する
            // Assert that all 16 items are on belts
            Assert.AreEqual(expectedItemCount, actualExpectedItems, $"Expected {expectedItemCount} items but only {actualExpectedItems} were inserted. This indicates a belt clogging bug.");

            // さらに待機して詰まり状態を安定させる
            // Wait additional time to stabilize clogged state
            var additionalTime = TimeSpan.FromSeconds(transportTime * 2);
            startTime = DateTime.Now;
            while (DateTime.Now - startTime < additionalTime)
            {
                SupplyPowerIfNeeded();
                GameUpdater.UpdateWithWait();
            }

            #region Internal - SupplyPower

            void SupplyPowerIfNeeded()
            {
                if (!isGearBeltConveyor) return;
                foreach (var block in beltBlocks)
                {
                    var gearComp = block.GetComponent<GearBeltConveyorComponent>();
                    gearComp.SupplyPower(new RPM(10), new Torque(gearBeltParam.RequireTorque), true);
                }
            }

            void UpdateUntilWithPowerLocal(Func<bool> condition, TimeSpan localTimeout)
            {
                var endTime = DateTime.Now.Add(localTimeout);
                while (!condition())
                {
                    if (DateTime.Now > endTime) Assert.Fail("Timeout waiting for belt conveyor condition.");
                    SupplyPowerIfNeeded();
                    GameUpdater.AdvanceTicks(1);
                }
            }

            #endregion

            #region Internal

            float CalculateTransportTime(BlockMasterElement master, bool isGear)
            {
                if (isGear)
                {
                    // 歯車ベルトコンベア: duration = 1 / (rpm * torqueRate * beltConveyorSpeed)
                    // Gear belt conveyor: duration = 1 / (rpm * torqueRate * beltConveyorSpeed)
                    var gearBeltParam = master.BlockParam as GearBeltConveyorBlockParam;
                    const int generatorRpm = 10; // InfinityTorqueSimpleGearGeneratorのRPM
                    const float torqueRate = 1f;
                    return 1f / (generatorRpm * torqueRate * gearBeltParam.BeltConveyorSpeed);
                }
                else
                {
                    // 通常ベルトコンベア
                    // Normal belt conveyor
                    var beltParam = master.BlockParam as BeltConveyorBlockParam;
                    return beltParam.TimeOfItemEnterToExit;
                }
            }

            #endregion
            
            // ベルトコンベア上のアイテム数を確認する
            // Verify item count on belt conveyors
            var totalItemCount = 0;
            foreach (var belt in beltComponents)
            {
                totalItemCount += System.Linq.Enumerable.Count(belt.BeltConveyorItems, x => x != null);
            }
            Assert.AreEqual(expectedItemCount, totalItemCount, $"Should have {expectedItemCount} items on belts");
            
            // 各アイテムの間隔を検証する（0.25の間隔）
            // Verify item spacing (0.25 interval)
            VerifyItemSpacing(beltComponents, expectedItemCount);
        }
        
        private void VerifyItemSpacing(List<VanillaBeltConveyorComponent> beltComponents, int expectedItemCount)
        {
            var itemsPerBelt = expectedItemCount / beltComponents.Count;
            var tolerance = 0.15; // 15%の許容誤差
            
            // 各ベルトコンベアのアイテム間隔を検証する
            // Verify item spacing on each belt conveyor
            for (var beltIndex = 0; beltIndex < beltComponents.Count; beltIndex++)
            {
                var belt = beltComponents[beltIndex];
                var items = belt.BeltConveyorItems;
                var totalTicks = items[0]?.TotalTicks ?? items[1]?.TotalTicks ?? 0;
                
                // 各スロットのアイテムが存在することを確認する
                // Verify items exist in each slot
                for (var slotIndex = 0; slotIndex < itemsPerBelt; slotIndex++)
                {
                    Assert.IsNotNull(items[slotIndex], $"Belt {beltIndex}, slot {slotIndex} should have an item");
                }
                
                // 間隔を検証する
                // Verify spacing
                var expectedInterval = (double)totalTicks / itemsPerBelt;
                
                for (var slotIndex = 0; slotIndex < itemsPerBelt; slotIndex++)
                {
                    var item = items[slotIndex];
                    var expectedRemainingTicks = expectedInterval * slotIndex;
                    var actualRemainingTicks = (double)item.RemainingTicks;
                    var toleranceTicks = expectedInterval * tolerance;
                    
                    // 最後のベルトの先頭アイテムは0
                    // Front item of last belt should be 0
                    if (beltIndex == beltComponents.Count - 1 && slotIndex == 0)
                    {
                        Assert.AreEqual(0u, item.RemainingTicks, $"Belt {beltIndex}, slot {slotIndex}: front item should have RemainingTicks=0");
                    }
                    else
                    {
                        Assert.That(actualRemainingTicks, Is.InRange(expectedRemainingTicks - toleranceTicks, expectedRemainingTicks + toleranceTicks),
                            $"Belt {beltIndex}, slot {slotIndex}: expected RemainingTicks ≈ {expectedRemainingTicks}, but was {actualRemainingTicks}");
                    }
                }
            }
        }

        private static (VanillaBeltConveyorComponent Component, Dictionary<IBlockInventory, ConnectedInfo> ConnectedTargets) CreateBeltConveyor()
        {
            var blockFactory = ServerContext.BlockFactory;
            var beltConveyor = blockFactory.Create(ForUnitTestModBlockId.BeltConveyorId, new BlockInstanceId(int.MaxValue), new BlockPositionInfo(Vector3Int.one, BlockDirection.North, Vector3Int.one));
            var beltConveyorComponent = beltConveyor.GetComponent<VanillaBeltConveyorComponent>();
            var connectedTargets = (Dictionary<IBlockInventory, ConnectedInfo>)beltConveyor.GetComponent<BlockConnectorComponent<IBlockInventory>>().ConnectedTargets;
            return (beltConveyorComponent, connectedTargets);
        }

        private static BlockConnectInfoElement AddTarget(Dictionary<IBlockInventory, ConnectedInfo> connectedTargets, IBlockInventory inventory, int index)
        {
            var selfConnector = CreateConnector(index);
            var targetConnector = CreateConnector(index + 100);
            connectedTargets.Add(inventory, new ConnectedInfo(selfConnector, targetConnector, null));
            return selfConnector;
        }

        private static BlockConnectInfoElement CreateConnector(int index)
        {
            return new BlockConnectInfoElement(index, "Inventory", Guid.NewGuid(), Vector3Int.zero, Array.Empty<Vector3Int>(), null);
        }

        private static void UpdateUntil(Func<bool> condition, TimeSpan timeout)
        {
            var endTime = DateTime.Now.Add(timeout);
            while (!condition())
            {
                if (DateTime.Now > endTime) Assert.Fail("Timeout waiting for belt conveyor condition.");
                GameUpdater.UpdateWithWait();
            }
        }

    }
}
