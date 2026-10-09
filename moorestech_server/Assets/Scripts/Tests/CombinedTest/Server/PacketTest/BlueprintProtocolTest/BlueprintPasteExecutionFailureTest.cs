using System.Linq;
using Core.Master;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Blueprint;
using Game.Construction;
using Game.Context;
using Game.EnergySystem;
using Game.PlacementTarget;
using Game.UnlockState;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Util.Blueprint;
using Server.Protocol.PacketResponse.Util.Blueprint.Planning;
using Server.Protocol.PacketResponse.Util.Construction;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class BlueprintPasteExecutionFailureTest
    {
        [Test]
        public void 判定後に占有された端点へ保存配線を張らないTest()
        {
            using var context = new BlueprintPasteProtocolTestContext(true, false);
            var blueprint = context.Create(ForUnitTestModBlockId.ElectricPoleId, ForUnitTestModBlockId.ElectricPoleId);
            blueprint.Wires.Add(new BlueprintLineJsonObject(0, 1, BlueprintPasteProtocolTestContext.WireGuid));
            context.UnlockLines();
            context.Supply(blueprint, 1);
            var wallet = context.Services.GetRequiredService<ConstructionWalletService>();
            var world = new ServerBlueprintPasteWorld(context.Services.GetRequiredService<PlacementTargetCatalog>(),
                context.Services.GetRequiredService<IGameUnlockStateDataController>(), false);
            var plan = BlueprintPastePlanner.Plan(blueprint, new[] { new BlueprintPasteOrigin(BlueprintPasteProtocolTestContext.Origin, true) },
                0, world, wallet.GetQuery(BlueprintPasteProtocolTestContext.PlayerId), ConstructionMaterialAccounting.TallyHeld(context.Inventory.InventoryItems));

            // 計画の後に別ブロックが同じ端点を占有する
            // Another block occupies an endpoint after the plan was made
            var existing = BlueprintPasteProtocolTestContext.Place(ForUnitTestModBlockId.ElectricPoleId,
                BlueprintPasteProtocolTestContext.Origin).GetComponent<IElectricWireConnector>();
            var result = BlueprintPasteExecutor.Execute(plan, BlueprintPasteProtocolTestContext.PlayerId,
                new BlockCellPlacementExecutor(wallet), context.Inventory);

            Assert.AreEqual(1, result.FailedLineCount);
            Assert.AreEqual(1, result.PlacementFailedCopyCount);
            Assert.AreEqual(0, existing.WireConnections.Count);
            var placed = ServerContext.WorldBlockDatastore.GetBlock(BlueprintPasteProtocolTestContext.Origin + new Vector3Int(3, 0, 0));
            Assert.AreEqual(0, placed.GetComponent<IElectricWireConnector>().WireConnections.Count);
        }

        [Test]
        public void 設置イベントで次の端点が占有されると失敗通知を返すTest()
        {
            using var context = new BlueprintPasteProtocolTestContext(true, false);
            var blueprint = context.Create(ForUnitTestModBlockId.ElectricPoleId, ForUnitTestModBlockId.ElectricPoleId);
            blueprint.Wires.Add(new BlueprintLineJsonObject(0, 1, BlueprintPasteProtocolTestContext.WireGuid));
            context.UnlockLines();
            context.Register(blueprint);
            context.Supply(blueprint, 1);
            using var subscription = ServerContext.WorldBlockUpdateEvent.GetBlockPlaceEvent(BlueprintPasteProtocolTestContext.Origin)
                .Take(1).Subscribe(_ => BlueprintPasteProtocolTestContext.Place(ForUnitTestModBlockId.ElectricPoleId,
                    BlueprintPasteProtocolTestContext.Origin + new Vector3Int(3, 0, 0)));

            context.Paste(blueprint, 0, BlueprintPasteProtocolTestContext.Origin);

            context.AssertDenied(BlueprintFailureReason.PasteLineFailed, 1);
            context.AssertDenied(BlueprintFailureReason.PastePlacementFailed, 1);
        }

        [Test]
        public void 実行中に次セルの資材が消えると無料設置せず不足を通知するTest()
        {
            using var context = new BlueprintPasteProtocolTestContext(true, false);
            var blueprint = context.Create(ForUnitTestModBlockId.BlockId, ForUnitTestModBlockId.BlockId);
            context.Register(blueprint);
            context.Supply(blueprint, 1);

            // 一つ目の確定前に一セル分だけ残して次セルの支払いを失敗させる
            // Leave just the first cell's payment before it commits, making the next cell unaffordable
            using var subscription = ServerContext.WorldBlockUpdateEvent.GetBlockPlaceEvent(BlueprintPasteProtocolTestContext.Origin)
                .Take(1).Subscribe(_ =>
                {
                    for (var slot = 0; slot < context.Inventory.GetSlotSize(); slot++)
                    {
                        var stack = context.Inventory.GetItem(slot);
                        if (stack.Count > 0) context.Inventory.SetItem(slot, stack.Id, stack.Count / 2);
                    }
                });

            context.Paste(blueprint, 0, BlueprintPasteProtocolTestContext.Origin);

            Assert.AreEqual(1, ServerContext.WorldBlockDatastore.BlockMasterDictionary.Count);
            Assert.IsFalse(ServerContext.WorldBlockDatastore.Exists(BlueprintPasteProtocolTestContext.Origin + new Vector3Int(3, 0, 0)));
            Assert.AreEqual(0, context.Inventory.InventoryItems.Sum(item => item.Count));
            context.AssertDenied(BlueprintFailureReason.PasteCostShortage, 1);
            context.AssertDenied(BlueprintFailureReason.PastePlacementFailed, 1);
        }
    }
}
