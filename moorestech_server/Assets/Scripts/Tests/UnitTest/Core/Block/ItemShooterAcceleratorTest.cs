using System;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Gear;
using Game.Block.Blocks.ItemShooter;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Gear.Common;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.UnitTest.Core.Block
{
    /// <summary>
    /// アイテムシューターアクセラレータのテスト（tick化により加速機能は廃止）
    /// Item shooter accelerator tests (acceleration disabled after tick conversion)
    /// </summary>
    public class ItemShooterAcceleratorTest
    {
        private int _scenarioOffset;
        private IServiceProvider _serviceProvider;

        /// <summary>
        /// アクセラレータコンポーネントが存在し、Updateが呼べることを確認
        /// Verify accelerator component exists and Update can be called
        /// </summary>
        [Test]
        public void AcceleratorComponentExistsAndUpdates()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _serviceProvider = serviceProvider;
            var param = (ItemShooterAcceleratorBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ItemShooterAccelerator).BlockParam;

            // ワールドセットアップ
            // World setup
            var world = ServerContext.WorldBlockDatastore;
            var blockPosition = new Vector3Int(0, 0, 0);

            world.TryAddBlock(ForUnitTestModBlockId.ItemShooterAccelerator, blockPosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var acceleratorBlock);
            var shooterComponent = acceleratorBlock.GetComponent<ItemShooterComponent>();
            var acceleratorComponent = acceleratorBlock.GetComponent<ItemShooterAcceleratorComponent>();

            // コンポーネントが存在することを確認
            // Verify components exist
            Assert.NotNull(shooterComponent);
            Assert.NotNull(acceleratorComponent);

            // アイテムを投入
            // Insert item
            var itemFactory = ServerContext.ItemStackFactory;
            var itemStack = itemFactory.Create(new ItemId(1), 1);
            var remain = shooterComponent.InsertItem(itemStack);
            Assert.AreEqual(ItemMaster.EmptyItemId, remain.Id);

            // 数tickシミュレーションしてエラーが出ないことを確認
            // Simulate a few ticks and verify no errors
            for (var i = 0; i < 10; i++)
            {
                acceleratorComponent.Update();
                GameUpdater.RunFrames(1);
            }

            // アイテムがまだ存在することを確認（移動中）
            // Verify item still exists (in transit)
            var shooterItem = shooterComponent.BeltConveyorItems[0] as ShooterInventoryItem;
            Assert.NotNull(shooterItem);
            Assert.AreEqual(1, shooterItem.ItemId.AsPrimitive());

            // クリーンアップ
            // Cleanup
            world.RemoveBlock(blockPosition, BlockRemoveReason.ManualRemove);
        }

        /// <summary>
        /// tick化により加速機能は廃止されたことを確認するテスト
        /// Test to confirm acceleration feature is disabled after tick conversion
        /// </summary>
        [Test]
        public void AccelerationIsDisabled()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _serviceProvider = serviceProvider;

            // ワールドセットアップ
            // World setup
            var world = ServerContext.WorldBlockDatastore;
            var blockPosition = new Vector3Int(10, 0, 0);

            world.TryAddBlock(ForUnitTestModBlockId.ItemShooterAccelerator, blockPosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var acceleratorBlock);
            var shooterComponent = acceleratorBlock.GetComponent<ItemShooterComponent>();

            // 歯車ジェネレーターを配置し、RPM/トルクを設定
            // Place gear generator and configure RPM/torque
            var generatorPosition = blockPosition + Vector3Int.right;
            world.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, generatorPosition, BlockDirection.East, Array.Empty<BlockCreateParam>(), out var generatorBlock);
            var generatorComponent = generatorBlock.GetComponent<SimpleGearGeneratorComponent>();
            generatorComponent.SetGenerateRpm(100f);
            generatorComponent.SetGenerateTorque(100f);

            // 歯車ネットワークを初期化
            // Initialize gear network
            var gearNetworkDatastore = _serviceProvider.GetService<GearNetworkDatastore>();
            var gearNetwork = gearNetworkDatastore.GearNetworks.First().Value;
            gearNetwork.ManualUpdate();

            // アイテムを投入
            // Insert item
            var itemFactory = ServerContext.ItemStackFactory;
            var itemStack = itemFactory.Create(new ItemId(1), 1);
            var remain = shooterComponent.InsertItem(itemStack);
            Assert.AreEqual(ItemMaster.EmptyItemId, remain.Id);

            // 初期状態を記録
            // Record initial state
            var initialItem = shooterComponent.BeltConveyorItems[0] as ShooterInventoryItem;
            var initialTotalTicks = initialItem.TotalTicks;

            // 数tickシミュレーション
            // Simulate a few ticks
            var acceleratorComponent = acceleratorBlock.GetComponent<ItemShooterAcceleratorComponent>();
            for (var i = 0; i < 5; i++)
            {
                gearNetwork.ManualUpdate();
                acceleratorComponent.Update();
                GameUpdater.RunFrames(1);
            }

            // tick化により、TotalTicksは変化しない（加速機能が廃止されているため）
            // With tick conversion, TotalTicks should not change (acceleration is disabled)
            var currentItem = shooterComponent.BeltConveyorItems[0] as ShooterInventoryItem;
            Assert.AreEqual(initialTotalTicks, currentItem.TotalTicks);

            // クリーンアップ
            // Cleanup
            world.RemoveBlock(generatorPosition, BlockRemoveReason.ManualRemove);
            world.RemoveBlock(blockPosition, BlockRemoveReason.ManualRemove);
        }
    }
}
