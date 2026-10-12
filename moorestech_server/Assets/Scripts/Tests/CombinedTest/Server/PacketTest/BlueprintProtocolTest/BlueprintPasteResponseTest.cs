using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Game.Context;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class BlueprintPasteResponseTest
    {
        [Test]
        public void 応答は実際に置いた個体だけを返すTest()
        {
            using var context = new BlueprintPasteProtocolTestContext(true, false);
            var blueprint = context.Create(ForUnitTestModBlockId.BlockId, ForUnitTestModBlockId.BlockId);
            context.Register(blueprint);
            context.Supply(blueprint, 1);
            var existingPosition = BlueprintPasteProtocolTestContext.Origin;
            var existing = BlueprintPasteProtocolTestContext.Place(ForUnitTestModBlockId.BlockId, existingPosition);

            // 既存セルは応答に含めず、新規セルの個体IDを返す
            // Exclude an existing cell and return the new cell's instance ID
            var response = context.SendForResponse(BlueprintRequest.CreatePasteRequest(blueprint.BlueprintGuid,
                0, new List<Vector3Int> { existingPosition }));
            Assert.IsTrue(response.Success);
            Assert.IsFalse(response.HasCostShortage);
            Assert.AreEqual(1, response.PlacedCells.Count);
            var placed = response.PlacedCells.Single();
            var added = ServerContext.WorldBlockDatastore.GetBlock(placed.Position.Vector3Int);
            Assert.AreEqual(existingPosition + new Vector3Int(3, 0, 0), placed.Position.Vector3Int);
            Assert.AreEqual(added.BlockInstanceId.AsPrimitive(), placed.BlockInstanceId);
            Assert.AreNotEqual(existing.BlockInstanceId.AsPrimitive(), placed.BlockInstanceId);
            Assert.AreEqual((int)BlockDirection.North, placed.Direction);
        }

        [Test]
        public void 分割境界の不足は先行応答に明示されるTest()
        {
            using var context = new BlueprintPasteProtocolTestContext(true, false);
            var blueprint = context.Create(ForUnitTestModBlockId.BlockId);
            context.Register(blueprint);
            context.Supply(blueprint, 63);
            var origins = Enumerable.Range(0, 65)
                .Select(index => new Vector3Int(10 + index, 0, 10)).ToList();
            var chunks = BlueprintRequest.CreatePasteRequests(blueprint.BlueprintGuid, 0, origins).ToList();

            // 64件目の不足を返し、送信側が次チャンクを止められる
            // Report the 64th-copy shortage so the sender can stop the next chunk
            Assert.AreEqual(2, chunks.Count);
            var response = context.SendForResponse(chunks[0]);
            Assert.IsTrue(response.HasCostShortage);
            Assert.AreEqual(63, response.PlacedCells.Count);
            Assert.IsNull(ServerContext.WorldBlockDatastore.GetBlock(origins[64]));
        }
    }
}
