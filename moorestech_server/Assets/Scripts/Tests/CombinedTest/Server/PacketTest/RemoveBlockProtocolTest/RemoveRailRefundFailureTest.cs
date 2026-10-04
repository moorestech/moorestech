using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Core.Item;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.Train.RailGraph;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using Tests.Util;
using Tests.Util.PlayerIdentity;
using UnityEngine;
using UnityEngine.TestTools;
using static Server.Protocol.PacketResponse.RemoveBlockProtocol;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class RemoveRailRefundFailureTest
    {
        [Test]
        public void 算出できない種類のレールは警告して返却せずブロックを撤去する()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var inventory = environment.ServiceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(13).MainOpenableInventory;
            var from = TrainTestHelper.PlaceRail(environment, Vector3Int.zero, BlockDirection.North).FrontNode;
            var to = TrainTestHelper.PlaceRail(environment, new Vector3Int(10, 0, 0), BlockDirection.North).BackNode;

            // 未接続の同種ブロックを撤去し、建設素材の返却を基準にする
            // Remove an unconnected block of the same type to establish the construction refund baseline
            var baselinePosition = new Vector3Int(30, 0, 0);
            TrainTestHelper.PlaceRail(environment, baselinePosition, BlockDirection.North);
            var baselineRefund = RemoveAndMeasureRefund(baselinePosition);
            Assert.IsNotEmpty(baselineRefund, "The baseline must include the block's construction refund.");

            // マスタから失われた種類の保存済み区間を再現する
            // Reproduce a saved segment whose tool type no longer exists in the master
            var unknownTool = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
            var handler = environment.ServiceProvider.GetService<RailConnectionCommandHandler>();
            Assert.IsTrue(handler.TryConnect(from.NodeId, from.Guid, to.NodeId, to.Guid, unknownTool));
            LogAssert.Expect(LogType.Warning, new Regex(@"\[RailRemovalRefund\] refund skipped: cost not computable\."));
            var refund = RemoveAndMeasureRefund(Vector3Int.zero);

            // ブロック分だけ返り、撤去先のブロックとレール区間が消える
            // Only the block refund remains, and the removed block and its rail segment disappear
            CollectionAssert.AreEquivalent(baselineRefund, refund);
            Assert.IsFalse(environment.WorldBlockDatastore.Exists(Vector3Int.zero));
            Assert.IsTrue(environment.WorldBlockDatastore.Exists(new Vector3Int(10, 0, 0)));
            Assert.IsFalse(environment.GetRailGraphDatastore().TryGetRailSegmentType(from.NodeId, to.NodeId, out _));

            #region Internal

            Dictionary<ItemId, int> RemoveAndMeasureRefund(Vector3Int position)
            {
                // 空のインベントリで全アイテムの返却量を測定する
                // Measure every refunded item in an empty inventory
                for (var i = 0; i < inventory.GetSlotSize(); i++) inventory.SetItem(i, ServerContext.ItemStackFactory.CreatEmpty());
                var request = MessagePackSerializer.Serialize(new RemoveBlockProtocolMessagePack(position));
                var bytes = environment.PacketResponseCreator.GetPacketResponse(request, BoundPacketContext.Bind(13)).Single();
                var response = MessagePackSerializer.Deserialize<RemoveBlockResponseMessagePack>(bytes.ToArray());
                Assert.IsTrue(response.Success, response.FailureReason.ToString());
                Assert.AreEqual(RemoveBlockFailureReason.None, response.FailureReason);
                return inventory.InventoryItems.Where(stack => stack.Count > 0)
                    .GroupBy(stack => stack.Id).ToDictionary(group => group.Key, group => group.Sum(stack => stack.Count));
            }

            #endregion
        }
    }
}
