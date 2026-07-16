using System;
using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.World.Interface.DataStore;
using NUnit.Framework;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.CombinedTest.Server.PacketTest.PlaceBlockProtocolTestSupport;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class PlaceBlockProtocolTest
    {
        private static readonly Guid Material1Guid = Guid.Parse("00000000-0000-0000-1234-000000000003"); // Test3(コスト×2)
        private static readonly Guid Material2Guid = Guid.Parse("00000000-0000-0000-1234-000000000004"); // Test4(コスト×1)
        private static readonly Guid PoleMaterialGuid = Guid.Parse("00000000-0000-0000-1234-000000000005"); // Test5 (電柱コスト×1)
        private static readonly Guid WireItemGuid = Guid.Parse("00000000-0000-0000-4649-000000000001"); // TestElectricWire

        [Test]
        public void 建設コストを消費して設置される()
        {
            var (packet, serviceProvider) = CreateServer();
            var inventory = GetInventory(serviceProvider);

            SetItem(inventory, 0, Material1Guid, 5);
            SetItem(inventory, 1, Material2Guid, 3);

            packet.GetPacketResponseForTest(CreatePlaceBlockPayload(ForUnitTestModBlockId.BlockId, (2, 4)), new PacketResponseContext());

            Assert.AreEqual(ForUnitTestModBlockId.BlockId, ServerContext.WorldBlockDatastore.GetBlock(new Vector3Int(2, 4)).BlockId);
            Assert.AreEqual(3, GetItemCount(inventory, Material1Guid));
            Assert.AreEqual(2, GetItemCount(inventory, Material2Guid));
        }

        [Test]
        public void 素材不足のセルはスキップされ賄える分だけ設置される()
        {
            var (packet, serviceProvider) = CreateServer();
            var inventory = GetInventory(serviceProvider);

            // コストはセルあたりTest3×2+Test4×1。素材は2セル分しかない
            // Cost per cell is Test3 x2 + Test4 x1; materials cover only two cells
            SetItem(inventory, 0, Material1Guid, 5);
            SetItem(inventory, 1, Material2Guid, 2);

            packet.GetPacketResponseForTest(CreatePlaceBlockPayload(ForUnitTestModBlockId.BlockId, (10, 0), (11, 0), (12, 0)), new PacketResponseContext());

            var world = ServerContext.WorldBlockDatastore;
            Assert.IsTrue(world.Exists(new Vector3Int(10, 0)));
            Assert.IsTrue(world.Exists(new Vector3Int(11, 0)));
            Assert.IsFalse(world.Exists(new Vector3Int(12, 0)));
            Assert.AreEqual(1, GetItemCount(inventory, Material1Guid));
            Assert.AreEqual(0, GetItemCount(inventory, Material2Guid));
        }

        [Test]
        public void 未解放ブロックは設置されない()
        {
            var (packet, serviceProvider) = CreateServer();
            GetInventory(serviceProvider);

            // TestElectricMachineはinitialUnlocked未設定（=ロック中）かつコスト未定義
            // TestElectricMachine is locked (no initialUnlocked) and has no cost
            packet.GetPacketResponseForTest(CreatePlaceBlockPayload(ForUnitTestModBlockId.MachineId, (5, 5)), new PacketResponseContext());

            Assert.IsFalse(ServerContext.WorldBlockDatastore.Exists(new Vector3Int(5, 5)));
        }

        [Test]
        public void requiredItems未定義かつ解放済みなら無償で設置される()
        {
            var (packet, serviceProvider) = CreateServer();
            GetInventory(serviceProvider);

            packet.GetPacketResponseForTest(CreatePlaceBlockPayload(ForUnitTestModBlockId.BeltConveyorId, (6, 6)), new PacketResponseContext());

            Assert.IsTrue(ServerContext.WorldBlockDatastore.Exists(new Vector3Int(6, 6)));
        }

        [Test]
        public void 既存ブロックと重なる場合は素材を消費しない()
        {
            var (packet, serviceProvider) = CreateServer();
            var inventory = GetInventory(serviceProvider);

            SetItem(inventory, 0, Material1Guid, 4);
            SetItem(inventory, 1, Material2Guid, 2);

            packet.GetPacketResponseForTest(CreatePlaceBlockPayload(ForUnitTestModBlockId.BlockId, (7, 7)), new PacketResponseContext());
            packet.GetPacketResponseForTest(CreatePlaceBlockPayload(ForUnitTestModBlockId.BlockId, (7, 7)), new PacketResponseContext());

            Assert.AreEqual(2, GetItemCount(inventory, Material1Guid));
            Assert.AreEqual(1, GetItemCount(inventory, Material2Guid));
        }

        [Test]
        public void 長尺ベルトは全セルを占有しコスト1セットで設置される()
        {
            var (packet, serviceProvider) = CreateServer();
            GrantRequiredItems(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor3, 1);
            // バリアントの設置可否はファミリー代表のunlock状態で決まる
            // Variant placement is gated by the family representative's unlock state
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor);

            var placeInfos = new List<PlaceInfo>
            {
                new()
                {
                    Position = new Vector3Int(30, 0, 10), Direction = BlockDirection.North,
                    VerticalDirection = BlockVerticalDirection.Horizontal, BlockId = ForUnitTestModBlockId.GearBeltConveyor3,
                },
            };
            packet.GetPacketResponseForTest(CreatePlacePayload(placeInfos), new PacketResponseContext());

            // 3セル全て同一ブロックとして占有される
            // All three cells are occupied by the same block entity
            var block = ServerContext.WorldBlockDatastore.GetBlock(new Vector3Int(30, 0, 10));
            Assert.IsNotNull(block);
            Assert.AreEqual(block, ServerContext.WorldBlockDatastore.GetBlock(new Vector3Int(30, 0, 12)));

            // コストは1セットのみ消費（素材残0）
            // Exactly one cost set consumed (no materials remain)
            AssertInventoryEmptyOfRequiredItems(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor3);
        }

        [Test]
        public void 電柱設置で自動接続の電線と建設コストが同時に消費される()
        {
            var (packet, serviceProvider) = CreateServer();
            var world = ServerContext.WorldBlockDatastore;

            // 距離1に機械→電柱設置
            // Pre-place an unconnected machine at distance 1, then place a pole via the protocol
            world.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(1, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machine);

            var inventory = GetInventory(serviceProvider);
            SetItem(inventory, 0, PoleMaterialGuid, 2);
            SetItem(inventory, 1, WireItemGuid, 5);

            packet.GetPacketResponseForTest(CreatePlaceBlockPayload(ForUnitTestModBlockId.ElectricPoleId, (0, 0)), new PacketResponseContext());

            var pole = world.GetBlock(new Vector3Int(0, 0, 0));
            Assert.IsNotNull(pole);
            Assert.IsTrue(pole.GetComponent<IElectricWireConnector>().ContainsWireConnection(machine.GetComponent<IElectricWireConnector>().BlockInstanceId));
            Assert.AreEqual(1, GetItemCount(inventory, PoleMaterialGuid));
            Assert.AreEqual(4, GetItemCount(inventory, WireItemGuid));
        }
    }
}
