using System;
using Core.Master;
using Game.Context;
using Game.Map;
using Game.Map.Interface.MapObject;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    /// <summary>
    ///     装飾物(None)攻撃をサーバーが拒否することを検証
    ///     Verifies the server rejects an attack on a None decoration
    /// </summary>
    public class MapObjectNotInteractableMiningTest
    {
        private const int PlayerId = 0;

        // テストマスタの装飾物。配置には無いので直接構築する
        // The decoration in the test master; it has no placement, so it is constructed directly
        private static readonly Guid DecorationMapObjectGuid = Guid.Parse("00000000-0000-4444-0000-000000000001");

        [Test]
        public void 装飾物への攻撃はNotInteractableで拒否されHPも減らない()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var miningService = serviceProvider.GetService<MapObjectMiningService>();
            var playerInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var decoration = new VanillaStaticMapObject(100, DecorationMapObjectGuid, false, 10, Vector3.zero);

            // 石の斧を装備していても装飾物は削れない
            // Even with a stone axe equipped, a decoration cannot be worn down
            var axeId = MasterHolder.ItemMaster.GetItemId(Guid.Parse("00000000-0000-0000-1234-000000000001"));
            var equippedItem = ServerContext.ItemStackFactory.Create(axeId, 1);
            var result = miningService.TryAttack(PlayerId, decoration, equippedItem, playerInventory.MainOpenableInventory, out var earnedItems);

            Assert.AreEqual(MiningAttackResult.NotInteractable, result);
            Assert.IsNull(earnedItems);
            Assert.IsFalse(decoration.IsDestroyed);
            Assert.AreEqual(10, decoration.CurrentHp);
        }

        [Test]
        public void MiningProtocol経由の装飾物攻撃は例外なくnullで畳まれる()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var decoration = new VanillaStaticMapObject(101, DecorationMapObjectGuid, false, 10, Vector3.zero);
            ServerContext.MapObjectDatastore.Add(decoration);
            var miningProtocol = new MiningProtocol(serviceProvider);
            var messagePack = MiningProtocol.MiningProtocolMessagePack.CreateMapObjectRequest(PlayerId, decoration.InstanceId);
            var payload = MessagePackSerializer.Serialize(messagePack);

            // PacketResponseCreatorの例外catchを経由せず直接呼び、ArgumentOutOfRangeExceptionが起きないことを確かめる
            // Call directly, bypassing PacketResponseCreator's exception catch, to confirm no ArgumentOutOfRangeException occurs
            ProtocolMessagePackBase response = null;
            Assert.DoesNotThrow(() => response = miningProtocol.GetResponse(payload, new PacketResponseContext(null)));

            Assert.IsNull(response);
            Assert.IsFalse(decoration.IsDestroyed);
            Assert.AreEqual(10, decoration.CurrentHp);
        }
    }
}
