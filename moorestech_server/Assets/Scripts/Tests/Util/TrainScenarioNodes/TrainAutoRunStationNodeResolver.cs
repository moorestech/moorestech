using System.Collections.Generic;
using System.Linq;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Train.RailGraph;
using NUnit.Framework;

namespace Tests.Util
{
    // 自動運転シナリオで使う駅の入出力ノードを抽出する
    // Extract station entry and exit nodes for auto-run scenarios
    public static class TrainAutoRunStationNodeResolver
    {
        public static StationNodeSet ExtractStationNodes(IBlock stationBlock, IReadOnlyList<RailComponent> railComponents)
        {
            var nodeInfos = railComponents
                .SelectMany(component => new[]
                {
                    (Node: component.FrontNode, IsFront: true),
                    (Node: component.BackNode, IsFront: false)
                })
                .Where(info => info.Node != null)
                .ToList();

            var exitFront = nodeInfos
                .FirstOrDefault(info => info.IsFront && info.Node.StationRef.NodeRole == StationNodeRole.Exit)
                .Node;
            Assert.IsNotNull(exitFront, "Station exit (front) node not found");

            var entryFront = nodeInfos
                .FirstOrDefault(info => info.IsFront && info.Node.StationRef.NodeRole == StationNodeRole.Entry)
                .Node;
            Assert.IsNotNull(entryFront, "Station entry (front) node not found");

            var exitBack = nodeInfos
                .FirstOrDefault(info => !info.IsFront && info.Node.StationRef.NodeRole == StationNodeRole.Exit)
                .Node;
            Assert.IsNotNull(exitBack, "Station exit (back) node not found");

            var entryBack = nodeInfos
                .FirstOrDefault(info => !info.IsFront && info.Node.StationRef.NodeRole == StationNodeRole.Entry)
                .Node;
            Assert.IsNotNull(entryBack, "Station entry (back) node not found");

            var segmentLength = entryFront!.GetDistanceToNode(exitFront!);
            Assert.Greater(segmentLength, 0, "Station segment length must be positive");
            var blockLength = stationBlock.BlockPositionInfo.BlockSize.z;
            Assert.Greater(blockLength, 0, "Station block size Z must be positive");
            return new StationNodeSet(exitFront!, entryFront!, exitBack!, entryBack!, segmentLength, blockLength);
        }
    }
}
