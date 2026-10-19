using System.Collections.Generic;
using System.Linq;
using Game.Train.RailGraph;

namespace Game.Train.Unit
{
    /// <summary>
    /// 進行先から3node以内で最初の分岐を探す。サーバーのA/D入力クランプとクライアントの進路プレビューで同じ探索を使い、両者のずれを防ぐ
    /// Finds the first branch within three nodes ahead. Shared by the server A/D clamp and the client route preview so both agree
    /// </summary>
    public static class TrainUnitNextBranchFinder
    {
        private const int SearchNodeLimit = 3;

        // 単一路線だけを辿り、2本以上に分かれるnodeを分岐として返す。辿ったnodeはadvancedNodesへ積む
        // Follow single routes only and return the first node splitting into 2+; nodes walked through go to advancedNodes
        public static bool TryFindNextBranch(IRailNode justPassedNode, IRailNode approachingNode, List<IRailNode> advancedNodes, out NextBranch nextBranch)
        {
            var previousNode = justPassedNode;
            var currentNode = approachingNode;
            for (var nodeIndex = 0; nodeIndex < SearchNodeLimit; nodeIndex++)
            {
                var connectedNodes = currentNode.ConnectedNodes.ToList();
                if (connectedNodes.Count >= 2)
                {
                    nextBranch = new NextBranch(previousNode, currentNode, connectedNodes);
                    return true;
                }

                // 分岐ではない単一路線だけを先へ進む。
                // Continue only through a single non-branch route.
                if (connectedNodes.Count != 1) break;
                var nextNode = connectedNodes[0];
                if (nextNode == null || nextNode == previousNode) break;
                previousNode = currentNode;
                currentNode = nextNode;
                advancedNodes.Add(currentNode);
            }

            nextBranch = default;
            return false;
        }

        public readonly struct NextBranch
        {
            public readonly IRailNode JustPassedNode;
            public readonly IRailNode JunctionNode;
            public readonly IReadOnlyList<IRailNode> CandidateNodes;

            public NextBranch(IRailNode justPassedNode, IRailNode junctionNode, IReadOnlyList<IRailNode> candidateNodes)
            {
                JustPassedNode = justPassedNode;
                JunctionNode = junctionNode;
                CandidateNodes = candidateNodes;
            }
        }
    }
}
