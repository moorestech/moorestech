using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Blueprint;
using Game.Construction;
using Game.EnergySystem;
using Microsoft.Extensions.DependencyInjection;
using Server.Protocol.PacketResponse.Util.Construction;
using Server.Protocol.PacketResponse.Util.Blueprint.Planning;
using Game.Context;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Util.Blueprint;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class BlueprintPasteResponseTest
    {
        [Test]
        public void 埋まり位置として送ったブロックだけ置かず線も張らない()
        {
            using var context = new BlueprintPasteProtocolTestContext(true, false);
            var blueprint = context.Create(ForUnitTestModBlockId.ElectricPoleId, ForUnitTestModBlockId.ElectricPoleId);
            blueprint.Wires.Add(new BlueprintLineJsonObject(0, 1, BlueprintPasteProtocolTestContext.WireGuid));
            context.UnlockLines();
            context.Register(blueprint);
            context.Supply(blueprint, 1);

            // 一基分の費用だけ減ることを照合
            // Compare each inventory item against the cost of exactly one pole
            var before = context.Inventory.InventoryItems.GroupBy(item => item.Id)
                .ToDictionary(group => group.Key, group => group.Sum(item => item.Count));
            var single = context.Create(ForUnitTestModBlockId.ElectricPoleId);
            var wallet = context.Services.GetRequiredService<ConstructionWalletService>().GetQuery(BlueprintPasteProtocolTestContext.PlayerId);
            var cost = BlueprintPasteCostCalculator.CalcRequiredItems(
                new[] { BlueprintPasteCopyBuilder.BuildUnobstructed(single) }, wallet, false)
                .ToDictionary(item => item.itemId, item => item.count);
            var origin = BlueprintPasteProtocolTestContext.Origin;
            var sunk = origin + new Vector3Int(3, 0, 0);
            var poleSize = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ElectricPoleId).BlockSize;
            var response = context.SendForResponse(BlueprintRequest.CreatePasteRequest(blueprint.BlueprintGuid, 0,
                new List<Vector3Int> { origin }, new List<BlockPositionInfo>
                {
                    new(sunk, BlockDirection.North, poleSize),
                }));

            // 埋まる端点と線は生成せず、応答にも含めない
            // Omit the sunk endpoint and its line from both placement and response
            Assert.IsTrue(response.Success);
            Assert.AreEqual(1, response.PlacedCells.Count);
            Assert.AreEqual(origin, response.PlacedCells.Single().Position.Vector3Int);
            Assert.IsNull(ServerContext.WorldBlockDatastore.GetBlock(sunk));
            var a = ServerContext.WorldBlockDatastore.GetBlock(origin).GetComponent<IElectricWireConnector>();
            Assert.IsEmpty(a.WireConnections);
            foreach (var pair in before)
            {
                cost.TryGetValue(pair.Key, out var consumed);
                Assert.AreEqual(pair.Value - consumed, context.Inventory.InventoryItems.Where(item => item.Id == pair.Key).Sum(item => item.Count));
            }
        }

        [Test]
        public void 埋まり位置が空なら全ブロックを置く()
        {
            using var context = new BlueprintPasteProtocolTestContext(true, false);
            var blueprint = context.Create(ForUnitTestModBlockId.ElectricPoleId, ForUnitTestModBlockId.ElectricPoleId);
            blueprint.Wires.Add(new BlueprintLineJsonObject(0, 1, BlueprintPasteProtocolTestContext.WireGuid));
            context.UnlockLines();
            context.Register(blueprint);
            context.Supply(blueprint, 1);

            // 空の除外列なら両端と保存電線を復元する
            // An empty exclusion list restores both endpoints and the saved wire
            var origin = BlueprintPasteProtocolTestContext.Origin;
            var response = context.SendForResponse(BlueprintRequest.CreatePasteRequest(blueprint.BlueprintGuid, 0,
                new List<Vector3Int> { origin }, new List<BlockPositionInfo>()));
            Assert.IsTrue(response.Success);
            Assert.AreEqual(2, response.PlacedCells.Count);
            var a = ServerContext.WorldBlockDatastore.GetBlock(origin).GetComponent<IElectricWireConnector>();
            var b = ServerContext.WorldBlockDatastore.GetBlock(origin + new Vector3Int(3, 0, 0)).GetComponent<IElectricWireConnector>();
            Assert.IsTrue(a.ContainsWireConnection(b.BlockInstanceId));
            Assert.AreEqual(0, context.Inventory.InventoryItems.Sum(item => item.Count));
        }

        [Test]
        public void 重なり解消後も受信した埋まり足場には置かない()
        {
            using var context = new BlueprintPasteProtocolTestContext(true, false);
            var blueprint = context.Create(ForUnitTestModBlockId.BlockId);
            context.Register(blueprint);
            var origin = BlueprintPasteProtocolTestContext.Origin;
            var size = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BlockId).BlockSize;
            var footprint = new BlockPositionInfo(origin, BlockDirection.North, size);

            // クライアントの重なりが消えても埋まり判定を維持する
            // Retain the client's terrain exclusion after the overlap disappears.
            var response = context.SendForResponse(BlueprintRequest.CreatePasteRequest(blueprint.BlueprintGuid, 0,
                new List<Vector3Int> { origin }, new List<BlockPositionInfo> { footprint }));
            Assert.IsTrue(response.Success);
            Assert.IsEmpty(response.PlacedCells);
            Assert.IsNull(ServerContext.WorldBlockDatastore.GetBlock(origin));
        }

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
                0, new List<Vector3Int> { existingPosition }, new List<BlockPositionInfo>()));
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
            var chunks = BlueprintRequest.CreatePasteRequests(blueprint.BlueprintGuid, 0, origins,
                origins.Select(_ => new List<BlockPositionInfo>()).ToList()).ToList();

            // 64件目の不足を返し、送信側が次チャンクを止められる
            // Report the 64th-copy shortage so the sender can stop the next chunk
            Assert.AreEqual(2, chunks.Count);
            var response = context.SendForResponse(chunks[0]);
            Assert.IsTrue(response.HasCostShortage);
            Assert.AreEqual(63, response.PlacedCells.Count);
            Assert.IsNull(ServerContext.WorldBlockDatastore.GetBlock(origins[64]));
        }

        [Test]
        public void 同じ最小角でも向きか寸法が違えば埋まり除外しない()
        {
            var position = new Vector3Int(10, 20, 30);
            var size = new Vector3Int(2, 1, 1);
            var sunk = new BlockPositionInfo(position, BlockDirection.North, size);
            var world = new ServerBlueprintPasteWorld(null, null, false, new[] { sunk });

            // 向きと寸法が揃う要素だけ飛ばす
            // Skip only the element with the same direction and size used in the terrain check
            Assert.IsTrue(world.IsTerrainSunk(sunk));
            Assert.IsFalse(world.IsTerrainSunk(new BlockPositionInfo(position, BlockDirection.East, size)));
            Assert.IsFalse(world.IsTerrainSunk(new BlockPositionInfo(position, BlockDirection.North, Vector3Int.one)));
        }

        [Test]
        public void 重なるドラッグで同じ最小角の別向き要素は置かれる()
        {
            using var context = new BlueprintPasteProtocolTestContext(true, false);
            var blueprint = context.Create(ForUnitTestModBlockId.ElectricPoleId, ForUnitTestModBlockId.ElectricPoleId);
            blueprint.Blocks[1].Direction = (int)BlockDirection.East;
            context.Register(blueprint);
            context.Supply(blueprint, 2);
            var origin = BlueprintPasteProtocolTestContext.Origin;
            var poleSize = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ElectricPoleId).BlockSize;
            var sunk = new BlockPositionInfo(origin, BlockDirection.North, poleSize);

            // 北向き要素を飛ばしても同位置の東向きは残す
            // Skip the first copy's north-facing element while placing the later east-facing one there
            var response = context.SendForResponse(BlueprintRequest.CreatePasteRequest(blueprint.BlueprintGuid, 0,
                new List<Vector3Int> { origin, origin - new Vector3Int(3, 0, 0) }, new List<BlockPositionInfo> { sunk }));
            Assert.IsTrue(response.Success);
            Assert.AreEqual(3, response.PlacedCells.Count);
            Assert.IsTrue(response.PlacedCells.Any(cell => cell.Position.Vector3Int == origin && cell.Direction == (int)BlockDirection.East));
        }
    }
}
