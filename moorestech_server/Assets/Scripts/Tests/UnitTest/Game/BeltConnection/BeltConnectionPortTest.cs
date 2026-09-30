using System;
using System.Linq;
using Game.Block.Blocks.BeltConveyor.Connection;
using Game.Block.Interface;
using Mooresmaster.Model.InventoryConnectsModule;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.BeltConnection
{
    public class BeltConnectionPortTest
    {
        [Test]
        public void MissingOutputsDoNotEmit()
        {
            AssertPorts(new InventoryConnects(Array.Empty<InputConnectsElement>(), null),
                new InventoryConnects(new[] { Input(null, new[] { Vector3Int.back }) }, Array.Empty<OutputConnectsElement>()), false);
        }

        [Test]
        public void UnspecifiedOutputDirectionsDoNotEmit()
        {
            AssertPorts(new InventoryConnects(Array.Empty<InputConnectsElement>(), new[] { Output(null, null) }),
                new InventoryConnects(new[] { Input(null, new[] { Vector3Int.back }) }, Array.Empty<OutputConnectsElement>()), false);
        }

        [Test]
        public void MissingInputsDoNotAccept()
        {
            AssertPorts(new InventoryConnects(Array.Empty<InputConnectsElement>(), new[] { Output(null, new[] { Vector3Int.forward }) }),
                new InventoryConnects(null, Array.Empty<OutputConnectsElement>()), false);
        }

        [Test]
        public void UnspecifiedInputDirectionsAcceptFromEverySide()
        {
            AssertPorts(new InventoryConnects(Array.Empty<InputConnectsElement>(), new[] { Output(null, new[] { Vector3Int.forward }) }),
                new InventoryConnects(new[] { Input(null, null) }, Array.Empty<OutputConnectsElement>()), true);
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
            BeltInventoryConnectionContext.TryGetContext(source, out var context);
            // 同じtargetでも異なるport実体へ切り替える
            // Switch to a different port instance even when the target stays the same
            outputs[0] = Output(null, new[] { Vector3Int.forward });
            context.ApplyOverride(null);
            Assert.AreSame(outputs[0], connector.ConnectedTargets.Single().Value.SelfConnector);
            world.Clear();
        }

        [Test]
        public void LaterOutputConnectsWhenFirstOutputRejectsEveryInput()
        {
            var world = new BeltEdgeTestWorld(false, BlockDirection.North);
            var compatible = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var incompatible = Guid.Parse("22222222-2222-2222-2222-222222222222");
            var rejectedOutput = Output(incompatible, new[] { Vector3Int.forward });
            var acceptedOutput = Output(compatible, new[] { Vector3Int.forward });
            var acceptedInput = Input(compatible, new[] { Vector3Int.back });
            // 先頭出力では成立せず、後続出力だけが入力と適合する
            // Only the later output is compatible with the available input
            BeltPortTestTemplate.Install(new BeltPortTestTemplate(
                new InventoryConnects(Array.Empty<InputConnectsElement>(), new[] { rejectedOutput, acceptedOutput }),
                new InventoryConnects(new[] { acceptedInput }, Array.Empty<OutputConnectsElement>())));
            var source = world.Place("UL", 1);
            world.Place("UR", 1);
            world.AssertEdges(new[] { "UL>UR" }, "later output");
            Assert.AreSame(acceptedOutput, BeltEdgeTestWorld.Connector(source).ConnectedTargets.Single().Value.SelfConnector);
            world.Clear();
        }

        private static void AssertPorts(InventoryConnects sourcePorts, InventoryConnects targetPorts, bool connected)
        {
            var world = new BeltEdgeTestWorld(false, BlockDirection.North);
            BeltPortTestTemplate.Install(new BeltPortTestTemplate(sourcePorts, targetPorts));
            world.Place("UL", 1);
            world.Place("UR", 1);
            world.AssertEdges(connected ? new[] { "UL>UR" } : Array.Empty<string>(), "port definition");
            world.Clear();
        }

        private static InputConnectsElement Input(Guid? shape, Vector3Int[] directions) =>
            new InputConnectsElement(0, Guid.NewGuid(), shape, Vector3Int.zero, directions);
        private static OutputConnectsElement Output(Guid? shape, Vector3Int[] directions) =>
            new OutputConnectsElement(0, Guid.NewGuid(), shape, Vector3Int.zero, directions);
    }
}
