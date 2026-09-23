using System;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Component.ConnectJudge;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Game.ConnectOverride
{
    public class BeltConnectionSaveLoadTest
    {
        [TestCase(true)]
        [TestCase(false)]
        public void FourBeltsConvergeAfterLoadAndLowerReconnectsAfterUpperRemoval(bool upperFirst)
        {
            var (_, saveServices) = new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var sourceUpper = new Vector3Int(0, 1, 0);
            var sourceLower = new Vector3Int(0, 0, 0);
            var targetUpper = new Vector3Int(0, 1, 1);
            var targetLower = new Vector3Int(0, 0, 1);
            var order = upperFirst
                ? new[] { (ForUnitTestModBlockId.BeltConveyorId, sourceUpper),
                    (ForUnitTestModBlockId.GearBeltConveyor, targetUpper),
                    (ForUnitTestModBlockId.TestBeltConveyorUp, sourceLower),
                    (ForUnitTestModBlockId.TestGearBeltConveyorDown, targetLower) }
                : new[] { (ForUnitTestModBlockId.TestBeltConveyorUp, sourceLower),
                    (ForUnitTestModBlockId.TestGearBeltConveyorDown, targetLower),
                    (ForUnitTestModBlockId.BeltConveyorId, sourceUpper),
                    (ForUnitTestModBlockId.GearBeltConveyor, targetUpper) };
            foreach (var (id, position) in order)
                Assert.IsTrue(world.TryAddBlock(id, position, BlockDirection.North,
                    Array.Empty<BlockCreateParam>(), out _));
            AssertConnections(sourceUpper, sourceLower, targetUpper, targetLower, false);
            var save = saveServices.GetService<AssembleSaveJsonText>().AssembleSaveJson();

            var (_, loadServices) = new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            (loadServices.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(save);
            AssertConnections(sourceUpper, sourceLower, targetUpper, targetLower, false);

            ServerContext.WorldBlockDatastore.RemoveBlock(sourceUpper, BlockRemoveReason.ManualRemove);
            AssertConnections(sourceUpper, sourceLower, targetUpper, targetLower, true);
        }

        private static void AssertConnections(Vector3Int sourceUpper, Vector3Int sourceLower,
            Vector3Int targetUpper, Vector3Int targetLower, bool upperRemoved)
        {
            var world = ServerContext.WorldBlockDatastore;
            var upperTarget = world.GetBlock(targetUpper).GetComponent<VanillaBeltConveyorComponent>();
            var lowerTarget = world.GetBlock(targetLower).GetComponent<VanillaBeltConveyorComponent>();
            var lower = Connector(world.GetBlock(sourceLower));
            if (upperRemoved)
            {
                Assert.IsTrue(lower.ConnectedTargets.ContainsKey(upperTarget));
                Assert.IsFalse(lower.ConnectedTargets.ContainsKey(lowerTarget));
                return;
            }
            var upper = Connector(world.GetBlock(sourceUpper));
            Assert.IsTrue(upper.ConnectedTargets.ContainsKey(upperTarget));
            Assert.IsFalse(upper.ConnectedTargets.ContainsKey(lowerTarget));
            Assert.IsFalse(lower.ConnectedTargets.ContainsKey(upperTarget));
            Assert.IsFalse(lower.ConnectedTargets.ContainsKey(lowerTarget));
        }

        private static BlockConnectorComponent<IBlockInventory, DefaultConnectJudge> Connector(IBlock block)
        {
            return block.GetComponent<BlockConnectorComponent<IBlockInventory, DefaultConnectJudge>>();
        }
    }
}
