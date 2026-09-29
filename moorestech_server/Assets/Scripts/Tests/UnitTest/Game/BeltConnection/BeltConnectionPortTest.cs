using System;
using System.Linq;
using Game.Block.Interface;
using Mooresmaster.Model.InventoryConnectsModule;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.BeltConnection
{
    public class BeltConnectionPortTest
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void MissingPortsAndUnrestrictedDirectionsKeepTheirMeaning(int mode)
        {
            var world = new BeltEdgeTestWorld(false, BlockDirection.North);
            var outputs = mode == 0 ? null : new[] { Output(null, mode == 1 ? null : new[] { Vector3Int.forward }) };
            var inputs = mode == 2 ? null : new[] { Input(null, mode == 3 ? null : new[] { Vector3Int.back }) };
            BeltPortTestTemplate.Install(new BeltPortTestTemplate(
                new InventoryConnects(Array.Empty<InputConnectsElement>(), outputs),
                new InventoryConnects(inputs, Array.Empty<OutputConnectsElement>())));
            world.Place("UL", 1);
            world.Place("UR", 1);
            world.AssertEdges(mode == 3 ? new[] { "UL>UR" } : Array.Empty<string>(), "mode " + mode);
            world.Clear();
        }

        [Test]
        public void AllRawOutputAndInputCandidatesAreExamined()
        {
            var world = new BeltEdgeTestWorld(false, BlockDirection.North);
            var compatible = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var incompatible = Guid.Parse("22222222-2222-2222-2222-222222222222");
            var goodOutput = Output(compatible, new[] { Vector3Int.forward });
            var badOutput = Output(incompatible, new[] { Vector3Int.forward });
            var goodInput = Input(compatible, new[] { Vector3Int.back });
            var badInput = Input(incompatible, new[] { Vector3Int.back });
            BeltPortTestTemplate.Install(new BeltPortTestTemplate(
                new InventoryConnects(Array.Empty<InputConnectsElement>(), new[] { goodOutput, badOutput }),
                new InventoryConnects(new[] { badInput, goodInput }, Array.Empty<OutputConnectsElement>())));
            var source = world.Place("UL", 1);
            world.Place("UR", 1);
            world.AssertEdges(new[] { "UL>UR" }, "all candidates");
            var info = BeltEdgeTestWorld.Connector(source).ConnectedTargets.Single().Value;
            Assert.AreSame(goodOutput, info.SelfConnector);
            Assert.AreSame(goodInput, info.TargetConnector);
            world.Clear();
        }

        [Test]
        public void SameTargetPortReplacementIsApplied()
        {
            var world = new BeltEdgeTestWorld(false, BlockDirection.North);
            var outputs = new[] { Output(null, new[] { Vector3Int.forward }) };
            BeltPortTestTemplate.Install(new BeltPortTestTemplate(
                new InventoryConnects(Array.Empty<InputConnectsElement>(), outputs),
                new InventoryConnects(new[] { Input(null, new[] { Vector3Int.back }) }, Array.Empty<OutputConnectsElement>())));
            var source = world.Place("UL", 1);
            world.Place("UR", 1);
            var connector = BeltEdgeTestWorld.Connector(source);
            var mutation = connector.CaptureWorldMutation();
            // 同じtargetでも異なるport実体へ切り替える
            // Switch to a different port instance even when the target stays the same
            outputs[0] = Output(null, new[] { Vector3Int.forward });
            mutation.ApplyAfterMutation();
            Assert.AreSame(outputs[0], connector.ConnectedTargets.Single().Value.SelfConnector);
            world.Clear();
        }

        private static InputConnectsElement Input(Guid? shape, Vector3Int[] directions) =>
            new InputConnectsElement(0, Guid.NewGuid(), shape, Vector3Int.zero, directions);
        private static OutputConnectsElement Output(Guid? shape, Vector3Int[] directions) =>
            new OutputConnectsElement(0, Guid.NewGuid(), shape, Vector3Int.zero, directions);
    }
}
