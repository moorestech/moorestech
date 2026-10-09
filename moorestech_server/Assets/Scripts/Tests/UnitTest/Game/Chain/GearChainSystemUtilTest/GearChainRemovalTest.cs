using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core.Master;
using Core.Inventory;
using Game.Block.Blocks.GearChainPole;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Gear.Common;
using Game.PlayerInventory.Interface;
using Game.UnlockState;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Server.Protocol.PacketResponse.Util.GearChain;
using Tests.Module;
using Tests.Module.TestMod;
using UnityEngine;
using static Server.Protocol.PacketResponse.RemoveBlockProtocol;

namespace Tests.UnitTest.Game.Chain
{
    public class GearChainRemovalTest : GearChainSystemUtilTestBase
    {

        [Test]
        public void BlockRemovalDisconnectsChainConnections()
        {
            // 接続可能な距離にブロックを設置する
            // Place blocks within valid distance
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var posA = Vector3Int.zero;
            var posB = new Vector3Int(3, 0, 0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, posA, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var blockA);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, posB, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var blockB);

            // プレイヤーにチェーンアイテムを配布する
            // Give chain item to player inventory
            var inventory = _serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            inventory.SetItem(0, ServerContext.ItemStackFactory.Create(_chainItemId, 10));

            // チェーン接続を実行する
            // Execute chain connection
            var connected = GearChainSystemUtil.TryConnect(posA, posB, PlayerId, ConnectToolGuid, out var connectError);
            Assert.True(connected);
            Assert.AreEqual(GearChainPlacementFailureReason.None, connectError);
            Assert.AreEqual(0, CountItem(inventory, _chainItemId));

            // 接続が双方向に登録されていることを確認する
            // Verify connections are registered both ways
            var poleA = blockA.GetComponent<IGearChainPole>();
            var poleB = blockB.GetComponent<IGearChainPole>();
            Assert.True(poleA.ContainsChainConnection(blockB.BlockInstanceId));
            Assert.True(poleB.ContainsChainConnection(blockA.BlockInstanceId));

            // ブロックBをプロトコル経由で破壊する
            // Destroy block B via protocol
            _packet.GetPacketResponse(CreateRemoveBlockPacket(posB), Tests.Util.PlayerIdentity.BoundPacketContext.Bind(PlayerId));
            Assert.False(worldBlockDatastore.Exists(posB));
            Assert.AreEqual(10, CountItem(inventory, _chainItemId));

            // ブロックAの接続が削除されていることを確認する
            // Verify block A's connection is removed
            Assert.False(poleA.ContainsChainConnection(blockB.BlockInstanceId));
        }

        [Test]
        public void BlockRemovalDisconnectsMultipleChainConnections()
        {
            // 複数のポールを距離内に配置する
            // Place multiple poles within valid distance
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var posA = Vector3Int.zero;
            var posB = new Vector3Int(2, 0, 0);
            var posC = new Vector3Int(-2, 0, 0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, posA, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var blockA);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, posB, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var blockB);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, posC, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var blockC);

            // チェーンアイテムをプレイヤーに配布する
            // Provide chain items to player
            var inventory = _serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            inventory.SetItem(0, ServerContext.ItemStackFactory.Create(_chainItemId, 20));

            // 複数の接続を確立する
            // Establish multiple connections
            var connectAB = GearChainSystemUtil.TryConnect(posA, posB, PlayerId, ConnectToolGuid, out var errorAB);
            var connectAC = GearChainSystemUtil.TryConnect(posA, posC, PlayerId, ConnectToolGuid, out var errorAC);
            Assert.True(connectAB);
            Assert.True(connectAC);
            Assert.AreEqual(GearChainPlacementFailureReason.None, errorAB);
            Assert.AreEqual(GearChainPlacementFailureReason.None, errorAC);
            Assert.AreEqual(0, CountItem(inventory, _chainItemId));

            // 接続が正しく登録されていることを確認する
            // Verify connections are registered correctly
            var poleA = blockA.GetComponent<IGearChainPole>();
            var poleB = blockB.GetComponent<IGearChainPole>();
            var poleC = blockC.GetComponent<IGearChainPole>();
            Assert.True(poleA.ContainsChainConnection(blockB.BlockInstanceId));
            Assert.True(poleA.ContainsChainConnection(blockC.BlockInstanceId));
            Assert.True(poleB.ContainsChainConnection(blockA.BlockInstanceId));
            Assert.True(poleC.ContainsChainConnection(blockA.BlockInstanceId));

            // ブロックAをプロトコル経由で破壊する
            // Destroy block A via protocol
            _packet.GetPacketResponse(CreateRemoveBlockPacket(posA), Tests.Util.PlayerIdentity.BoundPacketContext.Bind(PlayerId));
            Assert.False(worldBlockDatastore.Exists(posA));
            Assert.AreEqual(20, CountItem(inventory, _chainItemId));

            // ブロックBとCの接続が削除されていることを確認する
            // Verify connections are removed from blocks B and C
            Assert.False(poleB.ContainsChainConnection(blockA.BlockInstanceId));
            Assert.False(poleC.ContainsChainConnection(blockA.BlockInstanceId));
        }
    }
}
