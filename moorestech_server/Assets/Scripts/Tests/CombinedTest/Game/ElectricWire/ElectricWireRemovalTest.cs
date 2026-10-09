using static Tests.CombinedTest.Game.Helpers.ElectricWireSaveLoadTestHelpers;
using System;
using System.Linq;
using Core.Inventory;
using Core.Update;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.PlayerInventory.Interface;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.UnlockState;
using Microsoft.Extensions.DependencyInjection;
using Game.PlayerIdentity;
using NUnit.Framework;
using Tests.Util.PlayerIdentity;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.ElectricWire;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.Util.EnergySystem.ElectricNetworkReflectionTestUtil;
using static Tests.Module.TestMod.ForUnitTestModBlockId;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;

namespace Tests.CombinedTest.Game.ElectricWire
{
    public class ElectricWireRemovalTest
    {
        private static readonly Guid ConnectToolGuid = Guid.Parse("c0000000-0000-0000-0000-000000000001");
        private static readonly Guid WireItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        // 3ブロックを鎖状に接続後、中央を撤去するとセグメントが分割され、
        // 相手側のWireConnectionsがクリアされ、GetRefundItemsに距離分の電線コストが含まれることを検証
        // After chaining three blocks, removing the middle splits the segment, clears the partners'
        // WireConnections, and GetRefundItems reports the wire cost proportional to each removed connection's distance
        [Test]
        public void ブロック撤去でワイヤーが切れ電線が返却される()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerId = PlayerIdentityTestHelper.Register(serviceProvider.GetRequiredService<PlayerIdentityRegistry>(), "steam:1").PlayerId;
            var wireItemId = MasterHolder.ItemMaster.GetItemId(WireItemGuid);
            serviceProvider.GetService<IGameUnlockStateDataController>().UnlockConnectTool(ConnectToolGuid);

            var posA = Pos(0, 0);
            var posB = Pos(3, 0);
            var posC = Pos(6, 0);

            ServerContext.WorldBlockDatastore.TryAddBlock(ElectricPoleId, posA, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var blockA);
            ServerContext.WorldBlockDatastore.TryAddBlock(ElectricPoleId, posB, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var blockB);
            ServerContext.WorldBlockDatastore.TryAddBlock(ElectricPoleId, posC, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var blockC);

            var inventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;
            inventory.SetItem(0, ServerContext.ItemStackFactory.Create(wireItemId, 10));

            Assert.IsTrue(ElectricWireSystemUtil.TryConnect(posA, posB, playerId, ConnectToolGuid, false, out var errorA), errorA.ToString());
            Assert.IsTrue(ElectricWireSystemUtil.TryConnect(posB, posC, playerId, ConnectToolGuid, false, out var errorB), errorB.ToString());

            // トポロジ反映のため1tick進める
            // Advance one tick for the topology flush
            GameUpdater.UpdateOneTick();

            var networkDatastore = serviceProvider.GetService<IElectricWireNetworkLookup>();
            Assert.AreEqual(1, GetSegmentCount(networkDatastore));

            var connectorA = blockA.GetComponent<IElectricWireConnector>();
            var connectorB = blockB.GetComponent<IElectricWireConnector>();
            var connectorC = blockC.GetComponent<IElectricWireConnector>();

            // 配置座標(0,0)-(3,0)-(6,0)より各接続の距離は3。consumptionPerLength=1なので電線コストは3本ずつ
            // Blocks at (0,0)-(3,0)-(6,0) put each connection at distance 3; with consumptionPerLength=1 each wire costs 3
            var costToA = connectorB.WireConnections[connectorA.BlockInstanceId].Cost;
            var costToC = connectorB.WireConnections[connectorC.BlockInstanceId].Cost;
            Assert.AreEqual(3, costToA.TotalCount);
            Assert.AreEqual(3, costToC.TotalCount);

            var refundItems = blockB.GetComponent<IGetRefundItemsInfo>().GetRefundItems();
            Assert.AreEqual(2, refundItems.Count);
            Assert.IsTrue(refundItems.All(item => item.Id == wireItemId));
            Assert.AreEqual(6, refundItems.Sum(item => item.Count));

            ServerContext.WorldBlockDatastore.RemoveBlock(posB, BlockRemoveReason.ManualRemove);

            // 撤去に伴うトポロジ反映のため1tick進める
            // Advance one tick so the removal's topology flush is applied
            GameUpdater.UpdateOneTick();

            // 相手側のWireConnectionsから中央ブロックが消えていること
            // The middle block is gone from both partners' WireConnections
            Assert.IsFalse(connectorA.ContainsWireConnection(connectorB.BlockInstanceId));
            Assert.IsFalse(connectorC.ContainsWireConnection(connectorB.BlockInstanceId));

            // セグメントがA単独・C単独の2つに分かれていること
            // The segment splits into two: A alone and C alone
            Assert.AreEqual(2, GetSegmentCount(networkDatastore));
            Assert.IsTrue(networkDatastore.TryGetEnergySegment(connectorA.BlockInstanceId, out var segmentA));
            Assert.IsTrue(networkDatastore.TryGetEnergySegment(connectorC.BlockInstanceId, out var segmentC));
            Assert.AreNotSame(segmentA, segmentC);
        }
    }
}
