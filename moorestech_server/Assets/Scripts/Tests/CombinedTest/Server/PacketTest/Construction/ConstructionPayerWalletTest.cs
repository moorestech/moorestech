using System;
using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Master;
using Game.Block.Interface;
using Game.Construction;
using Game.PlayerInventory.Interface;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Event.EventReceive;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.CombinedTest.Server.PacketTest.Event;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.CombinedTest.Server.PacketTest.PlaceBlockProtocolTestSupport;

namespace Tests.CombinedTest.Server.PacketTest.Construction
{
    /// <summary>
    /// 財布の持ち主は撤去者ではなく設置して支払った人であることを検証する
    /// Verifies the wallet belongs to whoever placed and paid for the block, not to whoever removes it
    /// </summary>
    public class ConstructionPayerWalletTest
    {
        private const int PayerPlayerId = 11;
        private const int RemoverPlayerId = 12;
        private static readonly Guid Material1Guid = Guid.Parse("00000000-0000-0000-1234-000000000003");
        private static readonly Guid Material2Guid = Guid.Parse("00000000-0000-0000-1234-000000000004");
        private static readonly Vector3Int PlacePosition = new(10, 0);

        [Test]
        public void 別プレイヤーが撤去しても財布は設置者へ戻り返却物は撤去者へ渡る()
        {
            var (packet, serviceProvider) = CreateServer();
            var belt = ForUnitTestModBlockId.GearBeltConveyor;
            UnlockBlock(serviceProvider, belt);
            var payerInventory = GetPlayerInventory(serviceProvider, PayerPlayerId);
            var removerInventory = GetPlayerInventory(serviceProvider, RemoverPlayerId);
            SetItem(payerInventory, 0, Material1Guid, 1);
            SetItem(payerInventory, 1, Material2Guid, 1);
            var payerSink = EventTestUtil.RegisterCaptureSink(serviceProvider, PayerPlayerId);
            var removerSink = EventTestUtil.RegisterCaptureSink(serviceProvider, RemoverPlayerId);
            var lookup = serviceProvider.GetService<IRemainingPlacementCountLookup>();

            Place(packet, PayerPlayerId, belt);

            // 設置で減るのは設置者の財布だけで、通知も設置者へ1通だけ届く
            // Only the payer's wallet moves, and the single notification goes to the payer alone
            Assert.AreEqual(2, lookup.GetRemainingCount(PayerPlayerId, belt));
            Assert.AreEqual(0, lookup.GetRemainingCount(RemoverPlayerId, belt));
            Assert.AreEqual(new[] { 2 }, TakeRemainingCounts(payerSink));
            Assert.IsEmpty(TakeRemainingCounts(removerSink));

            Remove(packet, RemoverPlayerId);

            // 撤去+1でNに達し設置者の財布が凝縮、撤去者の財布は動かない
            // The return reaches one set's worth and condenses the payer's wallet; the remover's wallet never moves
            Assert.AreEqual(0, lookup.GetRemainingCount(PayerPlayerId, belt));
            Assert.AreEqual(0, lookup.GetRemainingCount(RemoverPlayerId, belt));
            Assert.AreEqual(new[] { 0 }, TakeRemainingCounts(payerSink));
            Assert.IsEmpty(TakeRemainingCounts(removerSink));

            // 凝縮返却の素材は撤去した人のインベントリへ入る
            // The condensed refund lands in the inventory of whoever removed the block
            Assert.AreEqual(1, GetItemCount(removerInventory, Material1Guid));
            Assert.AreEqual(1, GetItemCount(removerInventory, Material2Guid));
            Assert.AreEqual(0, GetItemCount(payerInventory, Material1Guid));
        }

        [Test]
        public void セーブロードをまたいでも課金元の財布へ戻る()
        {
            var (packet, serviceProvider) = CreateServer();
            var belt = ForUnitTestModBlockId.GearBeltConveyor;
            UnlockBlock(serviceProvider, belt);
            SetItem(GetPlayerInventory(serviceProvider, PayerPlayerId), 0, Material1Guid, 1);
            SetItem(GetPlayerInventory(serviceProvider, PayerPlayerId), 1, Material2Guid, 1);
            Place(packet, PayerPlayerId, belt);
            var saveJson = serviceProvider.GetService<AssembleSaveJsonText>().AssembleSaveJson();

            var (loadedPacket, loadedServiceProvider) = CreateServer();
            (loadedServiceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(saveJson);
            var loadedLookup = loadedServiceProvider.GetService<IRemainingPlacementCountLookup>();
            Assert.AreEqual(2, loadedLookup.GetRemainingCount(PayerPlayerId, belt));

            Remove(loadedPacket, RemoverPlayerId);

            // 課金元の記録がロードされているので、別プレイヤーの撤去でも設置者の財布が凝縮する
            // The payer record survives the load, so a stranger's removal still condenses the placer's wallet
            Assert.AreEqual(0, loadedLookup.GetRemainingCount(PayerPlayerId, belt));
            Assert.AreEqual(0, loadedLookup.GetRemainingCount(RemoverPlayerId, belt));
            Assert.AreEqual(1, GetItemCount(GetPlayerInventory(loadedServiceProvider, RemoverPlayerId), Material1Guid));
        }

        private static void Place(PacketResponseCreator packet, int playerId, BlockId blockId)
        {
            var placeInfos = new List<PlaceInfo>
            {
                new()
                {
                    Position = PlacePosition,
                    Direction = BlockDirection.North,
                    VerticalDirection = BlockVerticalDirection.Horizontal,
                    BlockId = blockId,
                },
            };
            var payload = MessagePackSerializer.Serialize(new PlaceBlockProtocol.SendPlaceBlockProtocolMessagePack(playerId, placeInfos));
            packet.GetPacketResponse(payload, new PacketResponseContext(null));
        }

        private static void Remove(PacketResponseCreator packet, int playerId)
        {
            var payload = MessagePackSerializer.Serialize(new RemoveBlockProtocol.RemoveBlockProtocolMessagePack(playerId, PlacePosition));
            packet.GetPacketResponse(payload, new PacketResponseContext(null));
        }

        private static IOpenableInventory GetPlayerInventory(ServiceProvider serviceProvider, int playerId)
        {
            return serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;
        }

        private static int[] TakeRemainingCounts(CapturedEventSink sink)
        {
            return sink.TakeAll()
                .Where(e => e.Tag == RemainingPlacementCountChangedEventPacket.EventTag)
                .Select(e => MessagePackSerializer.Deserialize<RemainingPlacementCountChangedEventPacket.RemainingPlacementCountMessagePack>(e.Payload).RemainingCount)
                .ToArray();
        }
    }
}
