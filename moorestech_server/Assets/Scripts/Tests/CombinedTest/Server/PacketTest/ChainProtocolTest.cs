using System;
using System.Linq;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.UnlockState;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
using Tests.CombinedTest.Server.PacketTest.Event;
using Tests.Module;
using Tests.Module.TestMod;
using UnityEngine;
using Server.Protocol;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class ChainProtocolTest
    {
        private const int PlayerId = 0;
        private static readonly Guid ConnectToolGuid = Guid.Parse("c0000000-0000-0000-0000-000000000003");
        private static readonly Guid ChainMaterialGuid = Guid.Parse("00000000-0000-0000-1234-000000000004");

        [Test]
        public void ConnectChainProtocolRespondsAndBroadcasts()
        {
            // テスト用のサーバーとイベントプロバイダを準備する
            // Prepare server and event provider for tests
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);
            sink.TakeAll();
            var chainItemId = MasterHolder.ItemMaster.GetItemId(ChainMaterialGuid);
            serviceProvider.GetService<IGameUnlockStateDataController>().UnlockConnectTool(ConnectToolGuid);

            // チェーンポールを配置する
            // Place chain poles
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var posA = new Vector3Int(1, 0, 0);
            var posB = new Vector3Int(3, 0, 0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, posA, BlockDirection.North, System.Array.Empty<BlockCreateParam>(), out var blockA);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, posB, BlockDirection.North, System.Array.Empty<BlockCreateParam>(), out var blockB);

            // チェーンアイテムをプレイヤーに付与する
            // Grant chain item to player
            var inventory = serviceProvider.GetService<global::Game.PlayerInventory.Interface.IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            inventory.SetItem(0, ServerContext.ItemStackFactory.Create(chainItemId, 10));

            // 接続プロトコルを送信する
            // Send connect protocol
            var connectBytes = packet.GetPacketResponse(Connect(posA, posB, PlayerId), new PacketResponseContext(null)).First();
            var typedConnect = MessagePackSerializer.Deserialize<GearChainConnectionEditProtocol.GearChainConnectionEditResponse>(connectBytes.ToArray());
            Assert.True(typedConnect.IsSuccess);

            // ブロック状態変更イベントが登録されていることを確認する
            // Ensure block state change event is enqueued
            var events = sink.TakeAll();
            var blockAEventTag = ChangeBlockStateEventPacket.CreateSpecifiedBlockEventTag(blockA.BlockPositionInfo);
            var blockBEventTag = ChangeBlockStateEventPacket.CreateSpecifiedBlockEventTag(blockB.BlockPositionInfo);
            Assert.IsTrue(events.Any(e => e.Tag == blockAEventTag || e.Tag == blockBEventTag), "Block state change event should be published");
        }

        private byte[] Connect(Vector3Int posA, Vector3Int posB, int playerId)
        {
            // 接続要求のメッセージパックを生成する
            // Build connect request message pack
            return MessagePackSerializer.Serialize(GearChainConnectionEditProtocol.GearChainConnectionEditRequest.CreateConnectRequest(posA, posB, playerId, ConnectToolGuid));
        }
    }
}
