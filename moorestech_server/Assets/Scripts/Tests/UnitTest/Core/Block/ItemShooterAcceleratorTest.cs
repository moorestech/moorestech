using System;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Gear;
using Game.Block.Blocks.ItemShooter;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
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
        private const int SimulationSteps = 120;
        private int _scenarioOffset;

        [Test]
        public void AcceleratesWhenRequirementsAreMet()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var param = (ItemShooterAcceleratorBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ItemShooterAccelerator).BlockParam;

            var (shooterItem, elapsedSeconds) = RunScenario(param.RequiredRpm, param.RequireTorque, SimulationSteps);

            Assert.NotNull(shooterItem);

            var effectiveAcceleration = (shooterItem.CurrentSpeed - 1f) / Math.Max(elapsedSeconds, 0.0001f);
            Assert.That(effectiveAcceleration, Is.EqualTo((float)param.PoweredAcceleration).Within(0.1f));
        }

        [Test]
        public void AccelerationScalesWithSuppliedRpm()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var param = (ItemShooterAcceleratorBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ItemShooterAccelerator).BlockParam;

            var (baseShot, baseElapsed) = RunScenario(param.RequiredRpm, param.RequireTorque, SimulationSteps);
            var (boostedShot, boostedElapsed) = RunScenario(param.RequiredRpm * 2, param.RequireTorque, SimulationSteps);

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
            var world = ServerContext.WorldBlockDatastore;
            var blockPosition = new Vector3Int(_scenarioOffset * 4, 0, 0);
            _scenarioOffset++;

            world.TryAddBlock(ForUnitTestModBlockId.ItemShooterAccelerator, blockPosition, BlockDirection.North, out var acceleratorBlock);
            var shooterComponent = acceleratorBlock.GetComponent<ItemShooterComponent>();

            var generatorPosition = blockPosition + Vector3Int.right;
            world.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, generatorPosition, BlockDirection.East, out var generatorBlock);
            var generatorComponent = generatorBlock.GetComponent<SimpleGearGeneratorComponent>();
            generatorComponent.SetGenerateRpm((float)rpm);
            generatorComponent.SetGenerateTorque((float)torque);

            var itemFactory = ServerContext.ItemStackFactory;
            var itemStack = itemFactory.Create(new ItemId(1), 1);
            var remain = shooterComponent.InsertItem(itemStack);
            Assert.AreEqual(ItemMaster.EmptyItemId, remain.Id);

            var elapsedSeconds = 0f;
            for (var i = 0; i < steps; i++)
            {
                GameUpdater.Update();
                elapsedSeconds += (float)GameUpdater.UpdateSecondTime;
            }

            var shooterItem = shooterComponent.BeltConveyorItems[0] as ShooterInventoryItem;
            var snapshot = shooterItem == null
                ? null
                : new ShooterInventoryItem(shooterItem.ItemId, shooterItem.ItemInstanceId, shooterItem.CurrentSpeed)
                {
                    RemainingPercent = shooterItem.RemainingPercent
                };

            world.RemoveBlock(generatorPosition);
            world.RemoveBlock(blockPosition);

            return (snapshot, elapsedSeconds);
        }
    }
}
