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
    public abstract class RemoveBlockProtocolTestBase
    {
        protected const int MachineBlockId = 1;
        protected const int PlayerId = 1;
        
        
        protected byte[] RemoveBlock(Vector3Int pos)
        {
            return MessagePackSerializer.Serialize(new RemoveBlockProtocolMessagePack(pos));
        }

        protected static RemoveBlockResponseMessagePack GetRemoveBlockResponse(List<byte[]> responsePackets)
        {
            Assert.AreEqual(1, responsePackets.Count);
            return MessagePackSerializer.Deserialize<RemoveBlockResponseMessagePack>(responsePackets[0]);
        }

        protected static void ConnectBidirectional(RailComponent from, RailComponent to, int distance)
        {
            // 表裏の方向ペアを接続し、通常のレール接続と同じ形にする
            // Connect both directional pairs to mirror normal rail connection shape.
            from.FrontNode.ConnectNode(to.FrontNode, distance);
            to.FrontNode.ConnectNode(from.FrontNode, distance);
            to.BackNode.ConnectNode(from.BackNode, distance);
            from.BackNode.ConnectNode(to.BackNode, distance);
        }

        protected static TrainUnit CreateTrainOnNode(TrainTestEnvironment environment, IRailNode node)
        {
            var (trainCar, _) = TrainTestCarFactory.CreateTrainCarWithItemContainer(0, 0, 1, 0, true);
            var railPosition = new RailPosition(new List<IRailNode> { node }, trainCar.Length, 0);

            // TrainUnit生成時にTrainRailPositionManagerへRailPositionが登録される
            // TrainUnit construction registers the RailPosition with TrainRailPositionManager.
            return new TrainUnit(
                railPosition,
                new List<TrainCar> { trainCar },
                environment.GetTrainRailPositionManager(),
                environment.GetTrainDiagramManager());
        }
    }
}
