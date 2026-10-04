using System;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Block.Interface;
using Game.Train.RailGraph;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Game.World.Interface.DataStore;
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
        public void 算出できない種類のレールは撤去せず接続を残す()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var from = TrainTestHelper.PlaceRail(environment, Vector3Int.zero, BlockDirection.North).FrontNode;
            var to = TrainTestHelper.PlaceRail(environment, new Vector3Int(10, 0, 0), BlockDirection.North).BackNode;

            // マスタから失われた種類の保存済み区間を再現する
            // Reproduce a saved segment whose tool type no longer exists in the master
            var unknownTool = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
            var handler = environment.ServiceProvider.GetService<RailConnectionCommandHandler>();
            Assert.IsTrue(handler.TryConnect(from.NodeId, from.Guid, to.NodeId, to.Guid, unknownTool));
            LogAssert.Expect(LogType.Warning, new Regex(@"\[RailRemovalRefund\] removal denied: cost not computable\."));
            LogAssert.Expect(LogType.Log, new Regex(@"\[RemoveBlock\] removal denied: Unknown position="));

            var request = MessagePackSerializer.Serialize(new RemoveBlockProtocolMessagePack(Vector3Int.zero));
            var bytes = environment.PacketResponseCreator.GetPacketResponse(request, BoundPacketContext.Bind(13)).Single();
            var response = MessagePackSerializer.Deserialize<RemoveBlockResponseMessagePack>(bytes.ToArray());

            // 素材を失う撤去を拒否し、ブロックと物理区間を維持する
            // Reject removal that would lose materials and preserve both the block and rail
            Assert.IsFalse(response.Success);
            Assert.AreEqual(RemoveBlockFailureReason.Unknown, response.FailureReason);
            Assert.IsTrue(environment.WorldBlockDatastore.Exists(Vector3Int.zero));
            TrainTestHelper.Node2NodeCheckAndAssert(from, to, "from", "to");
        }
    }
}
