using System;
using System.Collections.Generic;
using Game.Train.RailCalc;
using Game.Train.RailGraph;
using UnityEngine;

namespace Game.Train.Unit
{
    public static class TrainUnitBranchSelector
    {
        private const float BranchSampleTime = 0.15f;
        private const float DirectionEpsilon = 1e-6f;
        private const float CompareEpsilon = 0.0001f;

        public static IRailNode SelectManualBranchNode(IRailNode justPassedNode, IRailNode junctionNode, IReadOnlyList<IRailNode> connectedNodes, TrainUnitBranchCommand branchCommand)
        {
            if (connectedNodes.Count == 0)
            {
                throw new ArgumentException("connectedNodes must contain at least one node.", nameof(connectedNodes));
            }
            if (connectedNodes.Count == 1)
            {
                return connectedNodes[0];
            }

            // 進入方向を基準に候補ノードを左から右へ並べる。
            // Sort candidate nodes from left to right by the incoming travel direction.
            var candidates = BuildBranchCandidates(justPassedNode, junctionNode, connectedNodes);
            var straightIndex = ResolveStraightCandidateIndex(candidates);
            if (branchCommand == TrainUnitBranchCommand.Previous)
            {
                return SelectCandidateByOffset(candidates, straightIndex, -1);
            }

            // D入力だけ右隣を選び、ニュートラルは直進候補を維持する。
            // Only D input selects the right neighbor; neutral keeps the straight candidate.
            if (branchCommand == TrainUnitBranchCommand.Next)
            {
                return SelectCandidateByOffset(candidates, straightIndex, 1);
            }
            return candidates[straightIndex].Node;
        }

        private static List<BranchCandidate> BuildBranchCandidates(IRailNode justPassedNode, IRailNode junctionNode, IReadOnlyList<IRailNode> connectedNodes)
        {
            var incomingDirection = ResolveIncomingDirection(justPassedNode, junctionNode);
            var candidates = new List<BranchCandidate>(connectedNodes.Count);
            for (var i = 0; i < connectedNodes.Count; i++)
            {
                // 分岐先ごとにベジェ曲線を少し進めた位置から視覚上の向きを取る。
                // Sample each outgoing Bezier slightly ahead to derive the visual branch direction.
                candidates.Add(BuildBranchCandidate(incomingDirection, junctionNode, connectedNodes[i]));
            }
            candidates.Sort(CompareBranchCandidate);
            return candidates;
        }

        private static BranchCandidate BuildBranchCandidate(Vector3 incomingDirection, IRailNode junctionNode, IRailNode candidateNode)
        {
            BezierUtility.BuildRenderControlPoints(junctionNode.FrontControlPoint, candidateNode.BackControlPoint, out var p0, out var p1, out var p2, out var p3);
            var sampledPoint = BezierUtility.GetBezierPoint(p0, p1, p2, p3, BranchSampleTime);
            var sampledDirection = sampledPoint - p0;

            // 左右が重なる垂直分岐では水平角を0扱いにし、高さと保存キーで順序を固定する。
            // For vertical-overlapping branches, keep the horizontal angle at zero and stabilize by height and save key.
            var tangentDirection = BezierUtility.GetBezierTangent(p0, p1, p2, p3, BranchSampleTime);
            var outgoingDirection = ResolveOutgoingDirection(incomingDirection, sampledDirection, p3 - p0, tangentDirection);
            var signedAngle = Vector3.SignedAngle(incomingDirection, outgoingDirection, Vector3.up);
            return new BranchCandidate(candidateNode, signedAngle, Mathf.Abs(signedAngle), p3.y - p0.y);
        }

        private static Vector3 ResolveIncomingDirection(IRailNode justPassedNode, IRailNode junctionNode)
        {
            if (justPassedNode != null)
            {
                // 直前セグメントの終端接線を使って、分岐点へ入ってきた向きを取る。
                // Use the previous segment end tangent as the direction entering the junction.
                BezierUtility.BuildRenderControlPoints(justPassedNode.FrontControlPoint, junctionNode.BackControlPoint, out var p0, out var p1, out var p2, out var p3);
                var tangent = BezierUtility.GetBezierTangent(p0, p1, p2, p3, 1f);
                if (TryProjectHorizontal(tangent, out var tangentDirection))
                {
                    return tangentDirection;
                }
            }

            // 直前ノードが無い初期状態では junction 自身の前方向を基準にする。
            // When no previous node exists, use the junction forward control direction.
            if (TryProjectHorizontal(junctionNode.FrontControlPoint.ControlPointPosition, out var controlDirection))
            {
                return controlDirection;
            }
            return Vector3.forward;
        }

        private static Vector3 ResolveOutgoingDirection(Vector3 incomingDirection, Vector3 sampledDirection, Vector3 fullDelta, Vector3 tangentDirection)
        {
            if (TryProjectHorizontal(sampledDirection, out var horizontalSample))
            {
                return horizontalSample;
            }

            // サンプル点で水平成分が出ない場合は、終端差分と接線で補完する。
            // If the sample has no horizontal component, fall back to endpoint delta and tangent.
            if (TryProjectHorizontal(fullDelta, out var horizontalDelta))
            {
                return horizontalDelta;
            }
            if (TryProjectHorizontal(tangentDirection, out var horizontalTangent))
            {
                return horizontalTangent;
            }
            return incomingDirection;
        }

        private static bool TryProjectHorizontal(Vector3 source, out Vector3 direction)
        {
            var horizontal = new Vector3(source.x, 0f, source.z);
            if (horizontal.sqrMagnitude <= DirectionEpsilon)
            {
                direction = Vector3.zero;
                return false;
            }

            // SignedAngle の入力を安定させるため、水平成分だけを正規化する。
            // Normalize only the horizontal component to stabilize SignedAngle input.
            direction = horizontal.normalized;
            return true;
        }

        private static int ResolveStraightCandidateIndex(IReadOnlyList<BranchCandidate> candidates)
        {
            var straightIndex = 0;
            for (var i = 1; i < candidates.Count; i++)
            {
                if (CompareStraightCandidate(candidates[i], candidates[straightIndex]) < 0)
                {
                    straightIndex = i;
                }
            }
            return straightIndex;
        }

        private static int CompareStraightCandidate(BranchCandidate left, BranchCandidate right)
        {
            var angleCompare = CompareFloat(left.AbsoluteAngle, right.AbsoluteAngle);
            if (angleCompare != 0)
            {
                return angleCompare;
            }

            // 直進角が同じなら、表示順と同じ tie-break でサーバー決定を固定する。
            // If straightness ties, reuse display-order tie-breaks for deterministic server selection.
            return CompareBranchCandidate(left, right);
        }

        private static IRailNode SelectCandidateByOffset(IReadOnlyList<BranchCandidate> candidates, int straightIndex, int offset)
        {
            var selectedIndex = straightIndex + offset;
            if (selectedIndex < 0 || selectedIndex >= candidates.Count)
            {
                return candidates[straightIndex].Node;
            }
            return candidates[selectedIndex].Node;
        }

        private static int CompareBranchCandidate(BranchCandidate left, BranchCandidate right)
        {
            var angleCompare = CompareFloat(left.SignedAngle, right.SignedAngle);
            if (angleCompare != 0)
            {
                return angleCompare;
            }

            // 水平角が同じ候補は高さ、永続化キー、最後に実行時IDで決める。
            // Candidates with the same horizontal angle are ordered by height, persistent key, then runtime id.
            var verticalCompare = CompareFloat(left.VerticalDelta, right.VerticalDelta);
            if (verticalCompare != 0)
            {
                return verticalCompare;
            }
            return CompareNodeTieBreak(left.Node, right.Node);
        }

        private static int CompareNodeTieBreak(IRailNode left, IRailNode right)
        {
            var destinationCompare = CompareConnectionDestination(left, right);
            if (destinationCompare != 0)
            {
                return destinationCompare;
            }
            return left.NodeId.CompareTo(right.NodeId);
        }

        private static int CompareConnectionDestination(IRailNode left, IRailNode right)
        {
            var leftDestination = left.ConnectionDestination;
            var rightDestination = right.ConnectionDestination;
            var compare = leftDestination.blockPosition.x.CompareTo(rightDestination.blockPosition.x);
            if (compare != 0) return compare;
            compare = leftDestination.blockPosition.y.CompareTo(rightDestination.blockPosition.y);
            if (compare != 0) return compare;
            compare = leftDestination.blockPosition.z.CompareTo(rightDestination.blockPosition.z);
            if (compare != 0) return compare;
            compare = leftDestination.componentIndex.CompareTo(rightDestination.componentIndex);
            if (compare != 0) return compare;
            return leftDestination.IsFront.CompareTo(rightDestination.IsFront);
        }

        private static int CompareFloat(float left, float right)
        {
            var diff = left - right;
            if (Mathf.Abs(diff) <= CompareEpsilon)
            {
                return 0;
            }
            return diff < 0f ? -1 : 1;
        }

        private readonly struct BranchCandidate
        {
            public readonly IRailNode Node;
            public readonly float SignedAngle;
            public readonly float AbsoluteAngle;
            public readonly float VerticalDelta;

            public BranchCandidate(IRailNode node, float signedAngle, float absoluteAngle, float verticalDelta)
            {
                Node = node;
                SignedAngle = signedAngle;
                AbsoluteAngle = absoluteAngle;
                VerticalDelta = verticalDelta;
            }
        }
    }
}
