using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
using Tests.Module;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class ChainProtocolTest
    {
        private const int PlayerId = 0;

        [Test]
        public void ConnectChainProtocolRespondsAndBroadcasts()
        {
            // テスト用のサーバーとイベントプロバイダを準備する
            // Prepare server and event provider for tests
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var eventProvider = serviceProvider.GetService<EventProtocolProvider>();
            eventProvider.GetEventBytesList(PlayerId);
            var chainItemId = MasterHolder.ItemMaster.GetItemId(ChainConstants.ChainItemGuid);

            // チェーンポールを配置する
            // Place chain poles
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var posA = new Vector3Int(1, 0, 0);
            var posB = new Vector3Int(3, 0, 0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, posA, BlockDirection.North, System.Array.Empty<BlockCreateParam>(), out _);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, posB, BlockDirection.North, System.Array.Empty<BlockCreateParam>(), out _);

            // チェーンアイテムをプレイヤーに付与する
            // Grant chain item to player
            var inventory = serviceProvider.GetService<global::Game.PlayerInventory.Interface.IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            inventory.SetItem(0, ServerContext.ItemStackFactory.Create(chainItemId, 1));

            // 接続プロトコルを送信する
            // Send connect protocol
            var connectBytes = packet.GetPacketResponse(Connect(posA, posB, PlayerId)).First();
            var typedConnect = MessagePackSerializer.Deserialize<GearChainConnectionEditProtocol.GearChainConnectionEditResponse>(connectBytes.ToArray());
            Assert.True(typedConnect.IsSuccess);

            // ブロードキャストイベントが登録されていることを確認する
            // Ensure broadcast event is enqueued
            var events = eventProvider.GetEventBytesList(PlayerId);
            Assert.IsTrue(events.Any(e => e.Tag == ChainConnectionEventPacket.Tag));

            // 切断プロトコルを送信する
            // Send disconnect protocol
            var disconnectBytes = packet.GetPacketResponse(Disconnect(posA, posB)).First();
            var typedDisconnect = MessagePackSerializer.Deserialize<GearChainConnectionEditProtocol.GearChainConnectionEditResponse>(disconnectBytes.ToArray());
            Assert.True(typedDisconnect.IsSuccess);
        }

        private List<byte> Connect(Vector3Int posA, Vector3Int posB, int playerId)
        {
            // 接続要求のメッセージパックを生成する
            // Build connect request message pack
            return MessagePackSerializer.Serialize(GearChainConnectionEditProtocol.GearChainConnectionEditRequest.CreateConnectRequest(posA, posB, playerId)).ToList();
        }

        private List<byte> Disconnect(Vector3Int posA, Vector3Int posB)
        {
            // 切断要求のメッセージパックを生成する
            // Build disconnect request message pack
            return MessagePackSerializer.Serialize(GearChainConnectionEditProtocol.GearChainConnectionEditRequest.CreateDisconnectRequest(posA, posB)).ToList();
        }
    }
}
