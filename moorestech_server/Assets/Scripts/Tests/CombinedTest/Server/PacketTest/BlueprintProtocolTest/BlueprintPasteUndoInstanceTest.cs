using System;
using Game.Block.Interface;
using Game.Context;
using Game.World.Interface.DataStore;
using MessagePack;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class BlueprintPasteUndoInstanceTest
    {
        [Test]
        public void 同型ブロックに差し替わったらUndo撤去を拒否するTest()
        {
            using var context = new BlueprintPasteProtocolTestContext(true, true);
            var position = BlueprintPasteProtocolTestContext.Origin;
            var original = BlueprintPasteProtocolTestContext.Place(ForUnitTestModBlockId.BlockId, position);
            var originalId = original.BlockInstanceId;
            ServerContext.WorldBlockDatastore.RemoveBlock(position, BlockRemoveReason.ManualRemove);
            var replacement = BlueprintPasteProtocolTestContext.Place(ForUnitTestModBlockId.BlockId, position);
            var protocol = new RemoveBlockProtocol(context.Services);

            // 同座標・同型でも個体IDの違いをサーバーで拒否する
            // Reject a different instance on the server even at the same position and type
            var request = new RemoveBlockProtocol.RemoveBlockProtocolMessagePack(position, originalId);
            var response = (RemoveBlockProtocol.RemoveBlockResponseMessagePack)protocol.GetResponse(
                MessagePackSerializer.Serialize(request), BlueprintPasteProtocolTestContext.PlayerId);
            Assert.IsFalse(response.Success);
            Assert.AreEqual(RemoveBlockProtocol.RemoveBlockFailureReason.InstanceChanged, response.FailureReason);
            Assert.AreSame(replacement, ServerContext.WorldBlockDatastore.GetBlock(position));
        }

        [Test]
        public void 記録された個体IDならUndo撤去するTest()
        {
            using var context = new BlueprintPasteProtocolTestContext(true, true);
            var position = BlueprintPasteProtocolTestContext.Origin;
            var block = BlueprintPasteProtocolTestContext.Place(ForUnitTestModBlockId.BlockId, position);
            var request = new RemoveBlockProtocol.RemoveBlockProtocolMessagePack(position, block.BlockInstanceId);
            var protocol = new RemoveBlockProtocol(context.Services);

            var response = (RemoveBlockProtocol.RemoveBlockResponseMessagePack)protocol.GetResponse(
                MessagePackSerializer.Serialize(request), BlueprintPasteProtocolTestContext.PlayerId);
            Assert.IsTrue(response.Success);
            Assert.IsFalse(ServerContext.WorldBlockDatastore.Exists(position));
        }
    }
}
