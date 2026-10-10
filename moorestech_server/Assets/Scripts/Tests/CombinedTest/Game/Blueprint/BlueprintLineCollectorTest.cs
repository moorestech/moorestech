using System;
using Core.Master;
using Game.Block.Interface;
using Game.Blueprint;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.UnlockState;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using Server.Protocol.PacketResponse.Util.GearChain;
using Tests.CombinedTest.Server.PacketTest.GearChain;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game.Blueprint
{
    public class BlueprintLineCollectorTest
    {
        private const int PlayerId = 0;
        private static readonly Guid WireToolGuid = Guid.Parse("c0000000-0000-0000-0000-000000000001");

        [Test]
        public void コピー範囲内の配線だけを重複なく保存するTest()
        {
            var (_, services) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var unlock = services.GetRequiredService<IGameUnlockStateDataController>();
            unlock.UnlockConnectTool(WireToolGuid);
            unlock.UnlockConnectTool(GearChainPoleExtendTestHelper.ConnectToolGuid);

            // 両配線の素材を用意し実接続を通す
            // Supply both line types and exercise real connection paths.
            var inventory = services.GetRequiredService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            var wireItem = MasterHolder.ItemMaster.GetItemId(Guid.Parse("00000000-0000-0000-1234-000000000001"));
            var chainItem = MasterHolder.ItemMaster.GetItemId(Guid.Parse("00000000-0000-0000-1234-000000000004"));
            inventory.SetItem(0, ServerContext.ItemStackFactory.Create(wireItem, 100));
            inventory.SetItem(1, ServerContext.ItemStackFactory.Create(chainItem, 100));

            // 電柱・発電機は範囲内、機械は外へ置く
            // Keep the pole and generator inside, with the machine outside
            var pole = Place(ForUnitTestModBlockId.ElectricPoleId, new Vector3Int(0, 0, 0));
            var generator = Place(ForUnitTestModBlockId.GeneratorId, new Vector3Int(2, 0, 0));
            var machine = Place(ForUnitTestModBlockId.MachineId, new Vector3Int(6, 0, 0));
            ConnectWire(pole, generator);
            ConnectWire(generator, machine);

            // 内部と外部へ伸びるチェーンを1本ずつ張る
            // Create one internal chain and one chain crossing the copy boundary
            var chainA = Place(ForUnitTestModBlockId.GearChainPole, new Vector3Int(0, 0, 5));
            var chainB = Place(ForUnitTestModBlockId.GearChainPole, new Vector3Int(2, 0, 5));
            var chainOutside = Place(ForUnitTestModBlockId.GearChainPole, new Vector3Int(4, 0, 5));
            ConnectChain(chainA, chainB);
            ConnectChain(chainB, chainOutside);

            Assert.IsTrue(BlueprintCreateService.TryCreateFromArea("lines", Vector3Int.zero, new Vector3Int(3, 2, 6), out var blueprint));
            Assert.AreEqual(4, blueprint.Blocks.Count);
            Assert.AreEqual(1, blueprint.Wires.Count);
            Assert.AreEqual(1, blueprint.Chains.Count);
            AssertEndpoints(blueprint, blueprint.Wires[0], pole, generator, WireToolGuid);
            AssertEndpoints(blueprint, blueprint.Chains[0], chainA, chainB, GearChainPoleExtendTestHelper.ConnectToolGuid);

            // 逆順の対象でも端点indexを検証
            // Verify endpoint indices for reversed target order.
            var reversed = BlueprintLineCollector.Collect(new[] { chainB, generator, chainA, pole });
            Assert.AreEqual(1, reversed.wires.Count);
            Assert.AreEqual(1, reversed.chains.Count);
            Assert.AreEqual(1, reversed.wires[0].BlockIndexA);
            Assert.AreEqual(3, reversed.wires[0].BlockIndexB);
            Assert.AreEqual(0, reversed.chains[0].BlockIndexA);
            Assert.AreEqual(2, reversed.chains[0].BlockIndexB);
        }

        private static IBlock Place(BlockId blockId, Vector3Int position)
        {
            Assert.IsTrue(ServerContext.WorldBlockDatastore.TryAddBlock(blockId, position, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block));
            return block;
        }

        private static void ConnectWire(IBlock a, IBlock b)
        {
            Assert.IsTrue(ElectricWireSystemUtil.TryConnect(a.BlockPositionInfo.OriginalPos, b.BlockPositionInfo.OriginalPos,
                PlayerId, WireToolGuid, false, out var reason), reason.ToString());
        }

        private static void ConnectChain(IBlock a, IBlock b)
        {
            Assert.IsTrue(GearChainSystemUtil.TryConnect(a.BlockPositionInfo.OriginalPos, b.BlockPositionInfo.OriginalPos,
                PlayerId, GearChainPoleExtendTestHelper.ConnectToolGuid, out var reason), reason.ToString());
        }

        private static void AssertEndpoints(BlueprintJsonObject blueprint, BlueprintLineJsonObject line, IBlock a, IBlock b, Guid toolGuid)
        {
            Assert.Less(line.BlockIndexA, line.BlockIndexB);
            Assert.AreEqual(toolGuid, line.ConnectToolGuid);
            CollectionAssert.AreEquivalent(new[] { a.BlockPositionInfo.OriginalPos, b.BlockPositionInfo.OriginalPos },
                new[] { blueprint.Blocks[line.BlockIndexA].Offset, blueprint.Blocks[line.BlockIndexB].Offset });
        }
    }
}
