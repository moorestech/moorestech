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
    public class GearChainSystemUtilTest : GearChainSystemUtilTestBase
    {

        [Test]
        public void ConnectFailsWhenDistanceTooFar()
        {
            // 離れた位置にブロックを設置する
            // Place blocks at distant positions
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var far = new Vector3Int(30, 0, 0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, far, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            // チェーン接続を試行し、失敗コードを確認する
            // Attempt to connect chain and verify failure code
            var succeeded = GearChainSystemUtil.TryConnect(Vector3Int.zero, far, PlayerId, ConnectToolGuid, out var error);
            Assert.False(succeeded);
            Assert.AreEqual("TooFar", error);
        }

        [Test]
        public void ConnectFailsWithoutChainItem()
        {
            // 接続可能な距離にブロックを設置する
            // Place blocks within range
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var posA = Vector3Int.zero;
            var posB = new Vector3Int(2, 0, 0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, posA, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, posB, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            // チェーンアイテムを持っていない状態で接続を試行する
            // Attempt to connect without chain item
            var connected = GearChainSystemUtil.TryConnect(posA, posB, PlayerId, ConnectToolGuid, out var error);
            Assert.False(connected);
            Assert.AreEqual("NoItem", error);
        }

        [Test]
        public void ConnectFailsWhenChainItemIsNotEnough()
        {
            // 接続距離に対して必要数が不足するケースを用意する
            // Prepare a case where the required chain items exceed inventory
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var posA = Vector3Int.zero;
            var posB = new Vector3Int(3, 0, 0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, posA, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, posB, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            // 必要数より少ないチェーンを渡す
            // Give fewer chain items than required
            var inventory = _serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            inventory.SetItem(0, ServerContext.ItemStackFactory.Create(_chainItemId, 9));

            // 接続を試行し不足エラーを確認する
            // Attempt to connect and confirm shortage error
            var connected = GearChainSystemUtil.TryConnect(posA, posB, PlayerId, ConnectToolGuid, out var error);
            Assert.False(connected);
            Assert.AreEqual("NoItem", error);
            Assert.AreEqual(9, inventory.GetItem(0).Count);
        }

        [Test]
        public void ConnectUpdatesGearConnections()
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
            inventory.SetItem(0, ServerContext.ItemStackFactory.Create(_chainItemId, 15));

            // チェーン接続を実行する
            // Execute chain connection
            var connected = GearChainSystemUtil.TryConnect(posA, posB, PlayerId, ConnectToolGuid, out var connectError);
            Assert.True(connected);
            Assert.IsEmpty(connectError ?? string.Empty);
            Assert.AreEqual(5, CountItem(inventory, _chainItemId));

            // ギア接続が双方向に登録されることを確認する
            // Verify gear connections are registered both ways
            var transformerA = blockA.GetComponent<IGearEnergyTransformer>();
            var transformerB = blockB.GetComponent<IGearEnergyTransformer>();
            Assert.AreEqual(transformerB, transformerA.GetGearConnects().Single().Transformer);
            Assert.AreEqual(transformerA, transformerB.GetGearConnects().Single().Transformer);
        }

        [Test]
        public void ChainPoleAcceptsMultipleConnectionsUntilLimit()
        {
            // 複数のポールを距離内に配置する
            // Place multiple poles within valid distance
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var posA = Vector3Int.zero;
            var posB = new Vector3Int(2, 0, 0);
            var posC = new Vector3Int(-2, 0, 0);
            var posD = new Vector3Int(0, 0, 2);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, posA, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var blockA);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, posB, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var blockB);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, posC, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var blockC);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, posD, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            // チェーンアイテムを上限分プレイヤーに配布する
            // Provide chain items for connection attempts
            var inventory = _serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            inventory.SetItem(0, ServerContext.ItemStackFactory.Create(_chainItemId, 25));

            // 上限まで接続を成功させる
            // Connect until reaching the limit
            var firstConnect = GearChainSystemUtil.TryConnect(posA, posB, PlayerId, ConnectToolGuid, out var firstError);
            var secondConnect = GearChainSystemUtil.TryConnect(posA, posC, PlayerId, ConnectToolGuid, out var secondError);
            Assert.True(firstConnect);
            Assert.True(secondConnect);
            Assert.IsEmpty(firstError ?? string.Empty);
            Assert.IsEmpty(secondError ?? string.Empty);

            // 上限超過の接続を拒否する
            // Reject connection beyond the limit
            var limitConnect = GearChainSystemUtil.TryConnect(posA, posD, PlayerId, ConnectToolGuid, out var limitError);
            Assert.False(limitConnect);
            Assert.AreEqual("ConnectionLimit", limitError);
            Assert.AreEqual(5, inventory.GetItem(0).Count);

            // 接続中のポールが正しく記録されていることを確認する
            // Ensure current connections are tracked correctly
            var poleA = blockA.GetComponent<IGearChainPole>();
            var poleB = blockB.GetComponent<IGearChainPole>();
            var poleC = blockC.GetComponent<IGearChainPole>();
            
            // リフレクションで_chainTargetsの数を取得して検証する
            // Get _chainTargets count via reflection and verify
            var chainTargetsCount = GetChainTargetsCount(poleA as GearChainPoleComponent);
            Assert.AreEqual(2, chainTargetsCount);
            
            Assert.True(poleA.ContainsChainConnection(blockB.BlockInstanceId));
            Assert.True(poleA.ContainsChainConnection(blockC.BlockInstanceId));
            Assert.True(poleB.ContainsChainConnection(blockA.BlockInstanceId));
            Assert.True(poleC.ContainsChainConnection(blockA.BlockInstanceId));
        }
    }
}
