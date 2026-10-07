using System;
using Core.Inventory;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.PlayerInventory.Interface;
using Game.UnlockState;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Util.ElectricWire;
using Server.Protocol.PacketResponse.Util.ElectricWire.Placement;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    /// <summary>
    /// レール式延長プロトコルの正常系テスト。異常系はElectricWireExtendProtocolFailureTest参照
    /// Success-path tests for the rail-style extend protocol; see ElectricWireExtendProtocolFailureTest for failures
    /// </summary>
    public abstract class ElectricWireExtendProtocolTestBase
    {
        protected const int PlayerId = 9;
        protected const int MaterialSlot = 3;
        protected const int WireSlot = 4;
        protected static readonly Guid MaterialGuid = Guid.Parse("00000000-0000-0000-1234-000000000005"); // Test5 (電柱の建設コスト×1)
        protected static readonly Guid ConnectToolGuid = Guid.Parse("c0000000-0000-0000-0000-000000000001");
        protected static readonly Guid LockedConnectToolGuid = Guid.Parse("c0000000-0000-0000-0000-000000000002"); // SetUpで解放しない未解放ツール
        protected static readonly Guid WireItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        protected ServiceProvider _serviceProvider;
        protected PacketResponseCreator _packet;
        protected ItemId _materialItemId;
        protected ItemId _wireItemId;

        [SetUp]
        public void SetUp()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _serviceProvider = serviceProvider;
            _packet = packet;
            _materialItemId = MasterHolder.ItemMaster.GetItemId(MaterialGuid);
            _wireItemId = MasterHolder.ItemMaster.GetItemId(WireItemGuid);
            serviceProvider.GetService<IGameUnlockStateDataController>().UnlockConnectTool(ConnectToolGuid);
        }

        #region TestUtil

        protected IOpenableInventory SetupInventory(int materialCount, int wireCount)
        {
            var inventory = _serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            if (0 < materialCount) inventory.SetItem(MaterialSlot, ServerContext.ItemStackFactory.Create(_materialItemId, materialCount));
            if (0 < wireCount) inventory.SetItem(WireSlot, ServerContext.ItemStackFactory.Create(_wireItemId, wireCount));
            return inventory;
        }

        protected ElectricWireExtendProtocol.ElectricWireExtendResponse SendExtend(Vector3Int fromPos, Vector3Int newPolePos)
        {
            var placeInfo = new PlaceInfo { Position = newPolePos, Direction = BlockDirection.North, VerticalDirection = BlockVerticalDirection.Horizontal };
            var payload = MessagePackSerializer.Serialize(ElectricWireExtendProtocol.ElectricWireExtendRequest.CreateExtendRequest(fromPos, ForUnitTestModBlockId.ElectricPoleId, placeInfo, ConnectToolGuid));
            var responses = _packet.GetPacketResponse(payload, Tests.Util.PlayerIdentity.BoundPacketContext.Bind(PlayerId));
            return MessagePackSerializer.Deserialize<ElectricWireExtendProtocol.ElectricWireExtendResponse>(responses[0]);
        }

        protected ElectricWireExtendProtocol.ElectricWireExtendResponse SendIsolatedPlace(Vector3Int newPolePos)
        {
            var placeInfo = new PlaceInfo { Position = newPolePos, Direction = BlockDirection.North, VerticalDirection = BlockVerticalDirection.Horizontal };
            var payload = MessagePackSerializer.Serialize(ElectricWireExtendProtocol.ElectricWireExtendRequest.CreateIsolatedPlaceRequest(ForUnitTestModBlockId.ElectricPoleId, placeInfo));
            var responses = _packet.GetPacketResponse(payload, Tests.Util.PlayerIdentity.BoundPacketContext.Bind(PlayerId));
            return MessagePackSerializer.Deserialize<ElectricWireExtendProtocol.ElectricWireExtendResponse>(responses[0]);
        }

        protected ElectricWireExtendProtocol.ElectricWireExtendResponse SendConnect(Vector3Int fromPos, Vector3Int toPos, Guid connectToolGuid)
        {
            var payload = MessagePackSerializer.Serialize(ElectricWireExtendProtocol.ElectricWireExtendRequest.CreateConnectRequest(fromPos, toPos, connectToolGuid));
            var responses = _packet.GetPacketResponse(payload, Tests.Util.PlayerIdentity.BoundPacketContext.Bind(PlayerId));
            return MessagePackSerializer.Deserialize<ElectricWireExtendProtocol.ElectricWireExtendResponse>(responses[0]);
        }

        protected ElectricWireExtendProtocol.ElectricWireExtendResponse SendRestoreConnect(Vector3Int fromPos, Vector3Int toPos, Guid connectToolGuid)
        {
            var payload = MessagePackSerializer.Serialize(ElectricWireExtendProtocol.ElectricWireExtendRequest.CreateRestoreConnectRequest(fromPos, toPos, connectToolGuid));
            var responses = _packet.GetPacketResponse(payload, Tests.Util.PlayerIdentity.BoundPacketContext.Bind(PlayerId));
            return MessagePackSerializer.Deserialize<ElectricWireExtendProtocol.ElectricWireExtendResponse>(responses[0]);
        }

        protected static int CountItem(IOpenableInventory inventory, ItemId itemId)
        {
            var total = 0;
            foreach (var itemStack in inventory.InventoryItems)
                if (itemStack.Id == itemId)
                    total += itemStack.Count;
            return total;
        }

        #endregion
    }
}
