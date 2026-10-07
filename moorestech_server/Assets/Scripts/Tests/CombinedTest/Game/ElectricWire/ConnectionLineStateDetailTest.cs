using System;
using System.Linq;
using Core.Master;
using Game.Block.Blocks.ConnectionLine;
using Game.Block.Blocks.ElectricWire;
using Game.Block.Blocks.GearChainPole;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.PlayerIdentity;
using Tests.Util.PlayerIdentity;
using Game.UnlockState;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using Server.Protocol.PacketResponse.Util.GearChain;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game.ElectricWire
{
    // 状態詳細が接続先と種類を運ぶことを検証
    // Verify state details carry each partner and its tool kind
    public class ConnectionLineStateDetailTest
    {
        private int _playerId;
        private static readonly Guid WireToolGuid = Guid.Parse("c0000000-0000-0000-0000-000000000001");
        private static readonly Guid ChainToolGuid = Guid.Parse("c0000000-0000-0000-0000-000000000003");

        [Test]
        public void 電線の状態詳細が接続先と種類を持つ()
        {
            var provider = CreateServer();
            // 本番接続経路で同期対象を作る
            // Build synchronized connections through the production path
            var world = ServerContext.WorldBlockDatastore;
            Assert.IsTrue(world.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var pole));
            Assert.IsTrue(world.TryAddBlock(ForUnitTestModBlockId.GeneratorId, new Vector3Int(2, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var generator));
            GiveItem(provider, "00000000-0000-0000-1234-000000000001");
            Assert.IsTrue(ElectricWireSystemUtil.TryConnect(pole.BlockPositionInfo.OriginalPos, generator.BlockPositionInfo.OriginalPos, _playerId, WireToolGuid, out var error), error.ToString());

            // シリアライズ後も種類と接続先が一致する
            // Partner and tool identity survive serialization
            var detail = ReadDetail<ElectricWireStateDetail>(pole, ElectricWireStateDetail.BlockStateDetailKey);

            Assert.AreEqual(1, detail.Partners.Length);
            Assert.AreEqual(generator.BlockInstanceId.AsPrimitive(), detail.Partners[0].PartnerBlockInstanceId);
            Assert.AreEqual(WireToolGuid, detail.Partners[0].ConnectToolGuid);
            CollectionAssert.AreEqual(new[] { generator.BlockInstanceId }, detail.Partners.Select(p => new BlockInstanceId(p.PartnerBlockInstanceId)).ToArray());
            var reverse = ReadDetail<ElectricWireStateDetail>(generator, ElectricWireStateDetail.BlockStateDetailKey);
            Assert.AreEqual(pole.BlockInstanceId.AsPrimitive(), reverse.Partners.Single().PartnerBlockInstanceId);
            Assert.AreEqual(WireToolGuid, reverse.Partners.Single().ConnectToolGuid);
        }

        [Test]
        public void チェーンの状態詳細が接続先と種類を持つ()
        {
            var provider = CreateServer();
            // 本番接続経路で同期対象を作る
            // Build synchronized connections through the production path
            var world = ServerContext.WorldBlockDatastore;
            Assert.IsTrue(world.TryAddBlock(ForUnitTestModBlockId.GearChainPole, new Vector3Int(1, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var poleA));
            Assert.IsTrue(world.TryAddBlock(ForUnitTestModBlockId.GearChainPole, new Vector3Int(3, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var poleB));
            GiveItem(provider, "00000000-0000-0000-1234-000000000004");
            Assert.IsTrue(GearChainSystemUtil.TryConnect(new Vector3Int(1, 0, 0), new Vector3Int(3, 0, 0), _playerId, ChainToolGuid, out var error), error.ToString());

            // シリアライズ後も種類と接続先が一致する
            // Partner and tool identity survive serialization
            var detail = ReadDetail<GearChainPoleStateDetail>(poleA, GearChainPoleStateDetail.BlockStateDetailKey);

            Assert.AreEqual(1, detail.Partners.Length);
            Assert.AreEqual(poleB.BlockInstanceId.AsPrimitive(), detail.Partners[0].PartnerBlockInstanceId);
            Assert.AreEqual(ChainToolGuid, detail.Partners[0].ConnectToolGuid);
            CollectionAssert.AreEqual(new[] { poleB.BlockInstanceId }, detail.Partners.Select(p => new BlockInstanceId(p.PartnerBlockInstanceId)).ToArray());
            var reverse = ReadDetail<GearChainPoleStateDetail>(poleB, GearChainPoleStateDetail.BlockStateDetailKey);
            Assert.AreEqual(poleA.BlockInstanceId.AsPrimitive(), reverse.Partners.Single().PartnerBlockInstanceId);
            Assert.AreEqual(ChainToolGuid, reverse.Partners.Single().ConnectToolGuid);
        }

        private ServiceProvider CreateServer()
        {
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            // インベントリを使うプレイヤーを登録する
            // Register the player before accessing its inventory
            _playerId = PlayerIdentityTestHelper.Register(provider.GetRequiredService<PlayerIdentityRegistry>(), "steam:1").PlayerId;
            var unlock = provider.GetService<IGameUnlockStateDataController>();
            unlock.UnlockConnectTool(WireToolGuid);
            unlock.UnlockConnectTool(ChainToolGuid);
            return provider;
        }

        private void GiveItem(ServiceProvider provider, string itemGuid)
        {
            var inventory = provider.GetService<IPlayerInventoryDataStore>().GetInventoryData(_playerId).MainOpenableInventory;
            inventory.SetItem(0, ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(Guid.Parse(itemGuid)), 10));
        }

        // 1ブロックが持つ全状態観測から指定キーの詳細を1つ取り出す
        // Pull the detail with the given key out of all state observables on the block
        private static T ReadDetail<T>(IBlock block, string key)
        {
            var detail = block.GetComponents<IBlockStateObservable>()
                .SelectMany(observable => observable.GetBlockStateDetails())
                .Single(d => d.Key == key);
            return MessagePackSerializer.Deserialize<T>(detail.Value);
        }
    }
}
