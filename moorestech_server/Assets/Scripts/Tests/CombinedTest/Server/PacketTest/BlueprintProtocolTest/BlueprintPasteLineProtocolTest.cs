using System.Linq;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Blueprint;
using Game.Context;
using Game.EnergySystem;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class BlueprintPasteLineProtocolTest
    {
        [TestCase(0)]
        [TestCase(1)]
        public void 保存電線を回転後の端点に復元し周囲へ自動配線しないTest(int rotation)
        {
            using var context = new BlueprintPasteProtocolTestContext(true, false);
            var blueprint = context.Create(ForUnitTestModBlockId.ElectricPoleId, ForUnitTestModBlockId.ElectricPoleId);
            blueprint.Wires.Add(new BlueprintLineJsonObject(0, 1, BlueprintPasteProtocolTestContext.WireGuid));
            context.UnlockLines();
            context.Register(blueprint);
            context.Supply(blueprint, 1);
            var outside = BlueprintPasteProtocolTestContext.Place(ForUnitTestModBlockId.ElectricPoleId,
                BlueprintPasteProtocolTestContext.Origin + new Vector3Int(-2, 0, 0)).GetComponent<IElectricWireConnector>();

            context.Paste(blueprint, rotation, BlueprintPasteProtocolTestContext.Origin);

            // 北向きの二端点は回転後にX列からZ列へ移る
            // The north-facing endpoints rotate from an X row to a Z row
            var a = Connector(BlueprintPasteProtocolTestContext.Origin);
            var b = Connector(BlueprintPasteProtocolTestContext.Origin + (rotation == 0 ? new Vector3Int(3, 0, 0) : new Vector3Int(0, 0, 3)));
            Assert.IsTrue(a.ContainsWireConnection(b.BlockInstanceId));
            Assert.IsTrue(b.ContainsWireConnection(a.BlockInstanceId));
            Assert.AreEqual(1, a.WireConnections.Count);
            Assert.AreEqual(1, b.WireConnections.Count);
            Assert.AreEqual(0, outside.WireConnections.Count);
            Assert.AreEqual(0, context.Inventory.InventoryItems.Sum(item => item.Count));
        }

        [Test]
        public void 重なった端点の線だけを省略し残りの保存配線を復元するTest()
        {
            using var context = new BlueprintPasteProtocolTestContext(true, false);
            var blueprint = context.Create(ForUnitTestModBlockId.ElectricPoleId, ForUnitTestModBlockId.ElectricPoleId,
                ForUnitTestModBlockId.ElectricPoleId);
            blueprint.Wires.Add(new BlueprintLineJsonObject(0, 1, BlueprintPasteProtocolTestContext.WireGuid));
            blueprint.Wires.Add(new BlueprintLineJsonObject(1, 2, BlueprintPasteProtocolTestContext.WireGuid));
            context.UnlockLines();
            context.Register(blueprint);
            context.Supply(blueprint, 1);
            var existing = BlueprintPasteProtocolTestContext.Place(ForUnitTestModBlockId.ElectricPoleId,
                BlueprintPasteProtocolTestContext.Origin).GetComponent<IElectricWireConnector>();

            context.Paste(blueprint, 0, BlueprintPasteProtocolTestContext.Origin);

            var b = Connector(BlueprintPasteProtocolTestContext.Origin + new Vector3Int(3, 0, 0));
            var c = Connector(BlueprintPasteProtocolTestContext.Origin + new Vector3Int(6, 0, 0));
            Assert.AreEqual(0, existing.WireConnections.Count);
            Assert.IsTrue(b.ContainsWireConnection(c.BlockInstanceId));
            Assert.AreEqual(1, b.WireConnections.Count);
            Assert.AreEqual(3, ServerContext.WorldBlockDatastore.BlockMasterDictionary.Count);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void 保存チェーンは無料設置でも素材を消費するTest(bool freePlacement)
        {
            using var context = new BlueprintPasteProtocolTestContext(true, freePlacement);
            var blueprint = context.Create(ForUnitTestModBlockId.GearChainPole, ForUnitTestModBlockId.GearChainPole);
            blueprint.Chains.Add(new BlueprintLineJsonObject(0, 1, BlueprintPasteProtocolTestContext.ChainGuid));
            context.UnlockLines();
            context.Register(blueprint);
            context.Supply(blueprint, 1);
            var before = context.Inventory.InventoryItems.Sum(item => item.Count);

            context.Paste(blueprint, 0, BlueprintPasteProtocolTestContext.Origin);

            var a = ServerContext.WorldBlockDatastore.GetBlock(BlueprintPasteProtocolTestContext.Origin).GetComponent<IGearChainPole>();
            var b = ServerContext.WorldBlockDatastore.GetBlock(BlueprintPasteProtocolTestContext.Origin + new Vector3Int(3, 0, 0)).GetComponent<IGearChainPole>();
            Assert.IsTrue(a.TryGetChainConnectionRecord(b.BlockInstanceId, out var record));
            Assert.Greater(record.TotalCount, 0);
            Assert.Less(context.Inventory.InventoryItems.Sum(item => item.Count), before);
        }

        [Test]
        public void 無料設置でも未解放線種を含むBPは丸ごと拒否するTest()
        {
            using var context = new BlueprintPasteProtocolTestContext(true, true);
            var blueprint = context.Create(ForUnitTestModBlockId.ElectricPoleId, ForUnitTestModBlockId.ElectricPoleId);
            blueprint.Wires.Add(new BlueprintLineJsonObject(0, 1, BlueprintPasteProtocolTestContext.WireGuid));
            context.Register(blueprint);

            context.Paste(blueprint, 0, BlueprintPasteProtocolTestContext.Origin);

            Assert.AreEqual(0, ServerContext.WorldBlockDatastore.BlockMasterDictionary.Count);
            context.AssertDenied(BlueprintFailureReason.PasteNotUnlocked, 1);
        }

        [Test]
        public void 無料設置はブロックと電線だけを支払い免除するTest()
        {
            using var context = new BlueprintPasteProtocolTestContext(true, true);
            var blueprint = context.Create(ForUnitTestModBlockId.ElectricPoleId, ForUnitTestModBlockId.ElectricPoleId);
            blueprint.Wires.Add(new BlueprintLineJsonObject(0, 1, BlueprintPasteProtocolTestContext.WireGuid));
            context.UnlockLines();
            context.Register(blueprint);

            context.Paste(blueprint, 0, BlueprintPasteProtocolTestContext.Origin);

            var a = Connector(BlueprintPasteProtocolTestContext.Origin);
            Assert.AreEqual(1, a.WireConnections.Count);
            Assert.AreEqual(0, a.WireConnections.Values.Single().Record.TotalCount);
            Assert.AreEqual(0, context.Inventory.InventoryItems.Sum(item => item.Count));
        }

        [Test]
        public void 電線代だけ不足してもブロックを一つも置かないTest()
        {
            using var context = new BlueprintPasteProtocolTestContext(true, false);
            var blueprint = context.Create(ForUnitTestModBlockId.ElectricPoleId, ForUnitTestModBlockId.ElectricPoleId);
            blueprint.Wires.Add(new BlueprintLineJsonObject(0, 1, BlueprintPasteProtocolTestContext.WireGuid));
            context.UnlockLines();
            context.Register(blueprint);

            // ブロック代だけを満たし、線代の不足で全体を拒否する
            // Cover block costs only and reject the whole copy for missing wire costs
            PlaceBlockProtocolTestSupport.GrantRequiredItems(context.Services, ForUnitTestModBlockId.ElectricPoleId, 2);
            context.Paste(blueprint, 0, BlueprintPasteProtocolTestContext.Origin);

            Assert.AreEqual(0, ServerContext.WorldBlockDatastore.BlockMasterDictionary.Count);
            context.AssertDenied(BlueprintFailureReason.PasteCostShortage, 1);
        }

        [Test]
        public void 無料設置でもチェーン代不足なら全体を拒否するTest()
        {
            using var context = new BlueprintPasteProtocolTestContext(true, true);
            var blueprint = context.Create(ForUnitTestModBlockId.GearChainPole, ForUnitTestModBlockId.GearChainPole);
            blueprint.Chains.Add(new BlueprintLineJsonObject(0, 1, BlueprintPasteProtocolTestContext.ChainGuid));
            context.UnlockLines();
            context.Register(blueprint);

            context.Paste(blueprint, 0, BlueprintPasteProtocolTestContext.Origin);

            Assert.AreEqual(0, ServerContext.WorldBlockDatastore.BlockMasterDictionary.Count);
            context.AssertDenied(BlueprintFailureReason.PasteCostShortage, 1);
        }

        private static IElectricWireConnector Connector(Vector3Int position)
        {
            return ServerContext.WorldBlockDatastore.GetBlock(position).GetComponent<IElectricWireConnector>();
        }
    }
}
