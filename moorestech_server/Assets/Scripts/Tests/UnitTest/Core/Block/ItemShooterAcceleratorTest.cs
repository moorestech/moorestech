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
    public class ItemShooterAcceleratorTest
    {
        // シミュレーション中にアイテムがベルト上に残るようにステップ数を調整
        // Adjust step count so item stays on belt during simulation
        private const int SimulationSteps = 10;
        private int _scenarioOffset;
        private IServiceProvider _serviceProvider;

        [Test]
        public void AcceleratesWhenRequirementsAreMet()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _serviceProvider = serviceProvider;
            var param = (ItemShooterAcceleratorBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ItemShooterAccelerator).BlockParam;

            // 実行: 要求トルク/RPMを満たすシナリオを走行
            // Act: run scenario with required torque/RPM
            var (shooterItem, elapsedSeconds) = RunScenario(param.RequiredRpm, param.RequireTorque, SimulationSteps);

            // 検証: 得られた加速度が設定値と一致すること
            // Assert: effective acceleration matches configured value
            Assert.NotNull(shooterItem);

            var effectiveAcceleration = (shooterItem.CurrentSpeed - 1f) / Math.Max(elapsedSeconds, 0.0001f);
            Assert.That(effectiveAcceleration, Is.EqualTo((float)param.PoweredAcceleration).Within(0.1f));
        }

        [Test]
        public void AccelerationScalesWithSuppliedRpm()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _serviceProvider = serviceProvider;
            var param = (ItemShooterAcceleratorBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ItemShooterAccelerator).BlockParam;

            // 実行: 基準RPMと倍速RPMで比較シナリオを実施
            // Act: simulate baseline and boosted RPM feeds
            var (baseShot, baseElapsed) = RunScenario(param.RequiredRpm, param.RequireTorque, SimulationSteps);
            var (boostedShot, boostedElapsed) = RunScenario(param.RequiredRpm * 2, param.RequireTorque, SimulationSteps);

            // 検証: 加速度が供給RPMに比例して増加すること
            // Assert: acceleration scales with supplied RPM
            var baseAcceleration = (baseShot.CurrentSpeed - 1f) / Math.Max(baseElapsed, 0.0001f);
            var boostedAcceleration = (boostedShot.CurrentSpeed - 1f) / Math.Max(boostedElapsed, 0.0001f);
            var expectedBoostedAcceleration = (float)(param.PoweredAcceleration * Math.Min(param.MaxAccelerationMultiplier, 2d));

            Assert.NotNull(baseShot);
            Assert.NotNull(boostedShot);
            Assert.That(baseAcceleration, Is.EqualTo((float)param.PoweredAcceleration).Within(0.1f));
            Assert.That(boostedAcceleration, Is.EqualTo(expectedBoostedAcceleration).Within(0.15f));
            Assert.That(boostedAcceleration, Is.GreaterThan(baseAcceleration));
        }

        private (ShooterInventoryItem shooterItem, float elapsedSeconds) RunScenario(double rpm, double torque, int steps)
        {
            // シナリオ用のワールド座標とブロック配置
            // Scenario setup: world position and block placement
            var world = ServerContext.WorldBlockDatastore;
            var blockPosition = new Vector3Int(_scenarioOffset * 4, 0, 0);
            _scenarioOffset++;

            world.TryAddBlock(ForUnitTestModBlockId.ItemShooterAccelerator, blockPosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var acceleratorBlock);
            var shooterComponent = acceleratorBlock.GetComponent<ItemShooterComponent>();

            // 歯車ジェネレーターを配置し、RPM/トルクを設定
            // Place gear generator and configure RPM/torque
            var generatorPosition = blockPosition + Vector3Int.right;
            world.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, generatorPosition, BlockDirection.East, Array.Empty<BlockCreateParam>(), out var generatorBlock);
            var generatorComponent = generatorBlock.GetComponent<SimpleGearGeneratorComponent>();
            generatorComponent.SetGenerateRpm((float)rpm);
            generatorComponent.SetGenerateTorque((float)torque);

            // 歯車ネットワークを初期化して電力供給を確立
            // Initialize gear network to establish power supply
            var gearNetworkDatastore = _serviceProvider.GetService<GearNetworkDatastore>();
            var gearNetwork = gearNetworkDatastore.GearNetworks.First().Value;
            gearNetwork.ManualUpdate();

            // 初期アイテムを投入してシミュレーション開始
            // Insert initial item stack to start simulation
            var itemFactory = ServerContext.ItemStackFactory;
            var itemStack = itemFactory.Create(new ItemId(1), 1);
            var remain = shooterComponent.InsertItem(itemStack);
            Assert.AreEqual(ItemMaster.EmptyItemId, remain.Id);

            // アクセラレータコンポーネントを取得
            // Get accelerator component
            var acceleratorComponent = acceleratorBlock.GetComponent<ItemShooterAcceleratorComponent>();

            // 指定ステップ分シミュレーション時間を進行
            // Advance simulation for requested steps
            // 注: 更新順序が重要 - アクセラレータが先に更新される必要がある
            // Note: Update order matters - accelerator must update before shooter
            var elapsedSeconds = 0f;
            var deltaTime = (float)GameUpdater.SecondsPerTick;
            for (var i = 0; i < steps; i++)
            {
                // 1. ギアネットワークを更新して電力を供給
                // 1. Update gear network to supply power
                gearNetwork.ManualUpdate();

                // 2. アクセラレータを先に更新して外部加速度を設定
                // 2. Update accelerator first to set external acceleration
                acceleratorComponent.Update();

                // 3. 1tickずつ進行してシューターを含む全コンポーネントを更新
                // 3. Advance by 1 tick and update all components including shooter
                GameUpdater.AdvanceTicks(1);

                elapsedSeconds += deltaTime;
            }

            // 結果をスナップショット化し、ブロックを片付け
            // Snapshot the result and clean up blocks
            var shooterItem = shooterComponent.BeltConveyorItems[0] as ShooterInventoryItem;
            var snapshot = shooterItem == null
                ? null
                : new ShooterInventoryItem(shooterItem.ItemId, shooterItem.ItemInstanceId, shooterItem.CurrentSpeed, shooterItem.StartConnector, shooterItem.GoalConnector)
                {
                    RemainingPercent = shooterItem.RemainingPercent
                };

            world.RemoveBlock(generatorPosition, BlockRemoveReason.ManualRemove);
            world.RemoveBlock(blockPosition, BlockRemoveReason.ManualRemove);

            return (snapshot, elapsedSeconds);
        }
    }
}
