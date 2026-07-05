using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.PlayerInventory.Interface;
using Game.UnlockState;
using Game.UnlockState.States;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Util.Construction;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class PlaceBlockProtocolTest
    {
        private const int PlayerId = 3;

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

            packet.GetPacketResponse(CreatePlaceBlockPayload(ForUnitTestModBlockId.BlockId, (2, 4)), new PacketResponseContext());

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

            packet.GetPacketResponse(CreatePlaceBlockPayload(ForUnitTestModBlockId.BlockId, (10, 0), (11, 0), (12, 0)), new PacketResponseContext());

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
            packet.GetPacketResponse(CreatePlaceBlockPayload(ForUnitTestModBlockId.MachineId, (5, 5)), new PacketResponseContext());

            Assert.IsFalse(ServerContext.WorldBlockDatastore.Exists(new Vector3Int(5, 5)));
        }

        [Test]
        public void requiredItems未定義かつ解放済みなら無償で設置される()
        {
            var (packet, serviceProvider) = CreateServer();
            GetInventory(serviceProvider);

            packet.GetPacketResponse(CreatePlaceBlockPayload(ForUnitTestModBlockId.BeltConveyorId, (6, 6)), new PacketResponseContext());

            Assert.IsTrue(ServerContext.WorldBlockDatastore.Exists(new Vector3Int(6, 6)));
        }

        [Test]
        public void 既存ブロックと重なる場合は素材を消費しない()
        {
            var (packet, serviceProvider) = CreateServer();
            var inventory = GetInventory(serviceProvider);

            SetItem(inventory, 0, Material1Guid, 4);
            SetItem(inventory, 1, Material2Guid, 2);

            packet.GetPacketResponse(CreatePlaceBlockPayload(ForUnitTestModBlockId.BlockId, (7, 7)), new PacketResponseContext());
            packet.GetPacketResponse(CreatePlaceBlockPayload(ForUnitTestModBlockId.BlockId, (7, 7)), new PacketResponseContext());

            Assert.AreEqual(2, GetItemCount(inventory, Material1Guid));
            Assert.AreEqual(1, GetItemCount(inventory, Material2Guid));
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

            packet.GetPacketResponse(CreatePlaceBlockPayload(ForUnitTestModBlockId.ElectricPoleId, (0, 0)), new PacketResponseContext());

            var pole = world.GetBlock(new Vector3Int(0, 0, 0));
            Assert.IsNotNull(pole);
            Assert.IsTrue(pole.GetComponent<IElectricWireConnector>().ContainsWireConnection(machine.GetComponent<IElectricWireConnector>().BlockInstanceId));
            Assert.AreEqual(1, GetItemCount(inventory, PoleMaterialGuid));
            Assert.AreEqual(4, GetItemCount(inventory, WireItemGuid));
        }

        [Test]
        public void セル毎に異なるBlockIdを一括設置できる()
        {
            var (packet, serviceProvider) = CreateServer();
            // 素材付与（歯車ベルト1セット×2）
            // Grant two cost sets of the gear belt family materials
            GrantRequiredItems(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor, 2);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor);

            var placeInfos = new List<PlaceInfo>
            {
                new()
                {
                    Position = new Vector3Int(10, 0, 10), Direction = BlockDirection.North,
                    VerticalDirection = BlockVerticalDirection.Horizontal, BlockId = ForUnitTestModBlockId.GearBeltConveyor,
                },
                new()
                {
                    Position = new Vector3Int(10, 0, 11), Direction = BlockDirection.North,
                    VerticalDirection = BlockVerticalDirection.Up, BlockId = ForUnitTestModBlockId.TestGearBeltConveyorUp,
                },
            };
            packet.GetPacketResponse(CreatePlacePayload(placeInfos), new PacketResponseContext());

            Assert.IsTrue(ServerContext.WorldBlockDatastore.Exists(new Vector3Int(10, 0, 10)));
            Assert.IsTrue(ServerContext.WorldBlockDatastore.Exists(new Vector3Int(10, 0, 11)));
            Assert.AreEqual(ForUnitTestModBlockId.TestGearBeltConveyorUp,
                ServerContext.WorldBlockDatastore.GetBlock(new Vector3Int(10, 0, 11)).BlockId);
        }

        [Test]
        public void バリアントの設置可否はファミリー代表のunlock状態で決まる()
        {
            var (packet, serviceProvider) = CreateServer();
            GrantRequiredItems(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor3, 1);

            // 代表（GearBeltConveyor）が未解放なら長尺バリアントも設置不可
            // A length variant cannot be placed while the family representative is locked
            LockBlock(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor);
            var placeInfos = new List<PlaceInfo>
            {
                new()
                {
                    Position = new Vector3Int(20, 0, 10), Direction = BlockDirection.North,
                    VerticalDirection = BlockVerticalDirection.Horizontal, BlockId = ForUnitTestModBlockId.GearBeltConveyor3,
                },
            };
            packet.GetPacketResponse(CreatePlacePayload(placeInfos), new PacketResponseContext());
            Assert.IsFalse(ServerContext.WorldBlockDatastore.Exists(new Vector3Int(20, 0, 10)));

            // 代表を解放すると長尺バリアントが設置できる
            // Unlocking the representative allows the variant placement
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor);
            packet.GetPacketResponse(CreatePlacePayload(placeInfos), new PacketResponseContext());
            Assert.IsTrue(ServerContext.WorldBlockDatastore.Exists(new Vector3Int(20, 0, 10)));
        }

        #region TestUtil

        private static (PacketResponseCreator packet, ServiceProvider serviceProvider) CreateServer()
        {
            return new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        private static IOpenableInventory GetInventory(ServiceProvider serviceProvider)
        {
            return serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
        }

        private static void SetItem(IOpenableInventory inventory, int slot, Guid itemGuid, int count)
        {
            inventory.SetItem(slot, ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(itemGuid), count));
        }

        private static int GetItemCount(IOpenableInventory inventory, Guid itemGuid)
        {
            var itemId = MasterHolder.ItemMaster.GetItemId(itemGuid);
            var total = 0;
            foreach (var stack in inventory.InventoryItems)
            {
                if (stack.Id != itemId) continue;
                total += stack.Count;
            }
            return total;
        }

        private static byte[] CreatePlaceBlockPayload(BlockId blockId, params (int x, int y)[] positions)
        {
            var placeInfos = new List<PlaceInfo>();
            foreach (var (x, y) in positions)
            {
                placeInfos.Add(new PlaceInfo
                {
                    Position = new Vector3Int(x, y),
                    Direction = BlockDirection.North,
                    VerticalDirection = BlockVerticalDirection.Horizontal,
                    BlockId = blockId,
                });
            }
            return CreatePlacePayload(placeInfos);
        }

        private static byte[] CreatePlacePayload(List<PlaceInfo> placeInfos)
        {
            return MessagePackSerializer.Serialize(new PlaceBlockProtocol.SendPlaceBlockProtocolMessagePack(PlayerId, placeInfos));
        }

        private static void GrantRequiredItems(ServiceProvider serviceProvider, BlockId blockId, int costSets)
        {
            var inventory = GetInventory(serviceProvider);
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            var itemCounts = ConstructionCostService.ToItemCounts(blockMaster.RequiredItems);
            foreach (var (itemId, count) in itemCounts)
            {
                inventory.InsertItem(itemId, count * costSets);
            }
        }

        private static void UnlockBlock(ServiceProvider serviceProvider, BlockId blockId)
        {
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockGuid;
            serviceProvider.GetService<IGameUnlockStateDataController>().UnlockBlock(blockGuid);
        }

        private static void LockBlock(ServiceProvider serviceProvider, BlockId blockId)
        {
            // IGameUnlockStateDataControllerにはUnlockのみ存在するため、Load経由で強制的にロック状態へ書き換える
            // The controller only exposes Unlock, so force the locked state back via a state-load overwrite
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockGuid;
            var controller = serviceProvider.GetService<IGameUnlockStateDataController>();
            controller.LoadUnlockState(new GameUnlockStateJsonObject
            {
                BlockUnlockStateInfos = new List<BlockUnlockStateInfoJsonObject>
                {
                    new() { BlockGuid = blockGuid.ToString(), IsUnlocked = false },
                },
            });
        }

        #endregion
    }
}
