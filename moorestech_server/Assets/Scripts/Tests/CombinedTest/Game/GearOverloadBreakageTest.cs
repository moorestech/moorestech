using System;
using Core.Update;
using Game.Block.Blocks.Gear;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.World.Interface.DataStore;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Game
{
    public class GearOverloadBreakageTest
    {
        [Test]
        public void GearBreaks_WhenOverloaded_RPM()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            var gearPos = Vector3Int.zero;
            world.TryAddBlock(ForUnitTestModBlockId.OverloadTestGear, gearPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var gearBlock);
            
            var generatorPos = new Vector3Int(1, 0, 0);
            world.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, generatorPos, BlockDirection.East, Array.Empty<BlockCreateParam>(), out var generatorBlock);
            var generator = generatorBlock.GetComponent<SimpleGearGeneratorComponent>();

            // RPM Overload (150 > 100)
            generator.SetGenerateRpm(150);
            generator.SetGenerateTorque(20);

            RunGameUpdate(0.5f);

            Assert.IsFalse(world.Exists(gearPos), "Gear should break when overloaded (RPM)");
        }

        [Test]
        public void GearDoesNotBreak_WhenWithinLimits()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            var gearPos = Vector3Int.zero;
            world.TryAddBlock(ForUnitTestModBlockId.OverloadTestGear, gearPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var gearBlock);
            
            var generatorPos = new Vector3Int(1, 0, 0);
            world.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, generatorPos, BlockDirection.East, Array.Empty<BlockCreateParam>(), out var generatorBlock);
            var generator = generatorBlock.GetComponent<SimpleGearGeneratorComponent>();

            // Within Limits (RPM 50 < 100, Torque 20 < 50)
            generator.SetGenerateRpm(50);
            generator.SetGenerateTorque(20);

            RunGameUpdate(0.5f);

            Assert.IsTrue(world.Exists(gearPos), "Gear should not break within limits");
        }

        private void RunGameUpdate(float seconds)
        {
            var start = DateTime.Now;
            while ((DateTime.Now - start).TotalSeconds < seconds)
            {
                GameUpdater.UpdateWithWait();
            }
        }
    }
}
