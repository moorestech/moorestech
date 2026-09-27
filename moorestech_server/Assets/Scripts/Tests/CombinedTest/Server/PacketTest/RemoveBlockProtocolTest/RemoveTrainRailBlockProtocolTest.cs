using System.Collections.Generic;
using System.Linq;
using Core.Item;
using Core.Master;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using static Server.Protocol.PacketResponse.RemoveBlockProtocol;
using System;
using Server.Protocol;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class RemoveTrainRailBlockProtocolTest : RemoveBlockProtocolTestBase
    {

        [Test]
        public void TrainRailBlockInUseByTrainCannotBeRemovedTest()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var worldBlock = environment.WorldBlockDatastore;
            var railPos = new Vector3Int(0, 0, 0);

            // 列車が保持するノードを含む橋脚を準備する
            // Prepare a pier whose node is held by a train position.
            var railA = TrainTestHelper.PlaceRail(environment, railPos, BlockDirection.East, out _);
            var railB = TrainTestHelper.PlaceRail(environment, new Vector3Int(1, 0, 0), BlockDirection.East);
            ConnectBidirectional(railA, railB, 100);

            // RailPositionを登録して手動削除ガードの監視対象にする
            // Register a RailPosition so the manual removal guard can observe it.
            CreateTrainOnNode(environment, railA.FrontNode);
            var response = GetRemoveBlockResponse(environment.PacketResponseCreator.GetPacketResponse(RemoveBlock(railPos), Tests.Util.BoundPacketContext.Bind(PlayerId)));
            Assert.False(response.Success);
            Assert.AreEqual(RemoveBlockFailureReason.NodeInUseByTrain, response.FailureReason);

            // ブロックとレール接続が残り、橋脚削除で列車位置が壊れないことを確認する
            // Verify the block and rail connection remain so train position is preserved.
            Assert.True(worldBlock.Exists(railPos));
            Assert.AreNotEqual(-1, railA.FrontNode.GetDistanceToNode(railB.FrontNode));
        }

        [Test]
        public void TrainRailBlockWithoutTrainCanBeRemovedTest()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var worldBlock = environment.WorldBlockDatastore;
            var railPos = new Vector3Int(0, 0, 0);

            // 列車に使われていない橋脚は通常どおり削除できる
            // A pier unused by trains can still be removed normally.
            TrainTestHelper.PlaceRail(environment, railPos, BlockDirection.East);
            var response = GetRemoveBlockResponse(environment.PacketResponseCreator.GetPacketResponse(RemoveBlock(railPos), Tests.Util.BoundPacketContext.Bind(PlayerId)));
            Assert.True(response.Success);
            Assert.AreEqual(RemoveBlockFailureReason.None, response.FailureReason);

            Assert.False(worldBlock.Exists(railPos));
        }
    }
}
