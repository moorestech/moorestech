using System.Collections.Generic;
using Client.Game.InGame.Train.Unit;
using Game.Train.RailCalc;
using Game.Train.RailGraph;
using Game.Train.Unit;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.TrainHUDScreen
{
    public sealed class TrainBranchRoutePreviewController
    {
        private const int BranchRoutePreviewSearchNodeLimit = 3;
        private const int BranchRoutePreviewSamplesPerSegment = 18;
        private const float BranchRoutePreviewHeightOffset = 0.22f;
        private const float BranchRoutePreviewWidth = 0.18f;
        private static readonly Color BranchRoutePreviewColor = new(1f, 0.78f, 0.18f, 0.92f);

        private readonly List<IRailNode> _branchRouteNodes = new();
        private readonly List<IRailNode> _branchCandidateNodes = new();
        private readonly List<Vector3> _branchRoutePoints = new();

        private GameObject _branchRoutePreviewObject;
        private LineRenderer _branchRoutePreviewLine;
        private Material _branchRoutePreviewMaterial;

        public void Update(ClientTrainUnit trainUnit)
        {
            if (!TryBuildBranchRoutePreviewPoints(trainUnit))
            {
                Hide();
                return;
            }

            // 選択ルートの点列をLineRendererへ反映する。
            // Apply selected route points to the LineRenderer.
            EnsureBranchRoutePreviewRenderer();
            _branchRoutePreviewObject.SetActive(true);
            _branchRoutePreviewLine.positionCount = _branchRoutePoints.Count;
            for (var i = 0; i < _branchRoutePoints.Count; i++)
            {
                _branchRoutePreviewLine.SetPosition(i, _branchRoutePoints[i]);
            }
        }

        public void Hide()
        {
            if (_branchRoutePreviewLine == null) return;
            _branchRoutePreviewLine.positionCount = 0;
            _branchRoutePreviewObject.SetActive(false);
        }

        public void Destroy()
        {
            if (_branchRoutePreviewObject != null) UnityEngine.Object.Destroy(_branchRoutePreviewObject);
            if (_branchRoutePreviewMaterial != null) UnityEngine.Object.Destroy(_branchRoutePreviewMaterial);
            _branchRoutePreviewObject = null;
            _branchRoutePreviewLine = null;
            _branchRoutePreviewMaterial = null;
        }

        private bool TryBuildBranchRoutePreviewPoints(ClientTrainUnit trainUnit)
        {
            _branchRouteNodes.Clear();
            _branchRoutePoints.Clear();
            var railPosition = trainUnit?.RailPosition;
            if (railPosition == null) return false;

            // 現在の進行先から3node以内で最初の分岐を探す。
            // Search the first branch within three nodes from the current route.
            var previousNode = railPosition.GetNodeJustPassed();
            var currentNode = railPosition.GetNodeApproaching();
            if (currentNode == null) return false;
            var firstSegmentStart = ResolveFirstSegmentStart(railPosition, previousNode, currentNode);
            if (previousNode != null) _branchRouteNodes.Add(previousNode);
            _branchRouteNodes.Add(currentNode);

            if (!TryAppendRouteToNextBranch(previousNode, currentNode, trainUnit.GetManualBranchSelectionIndex())) return false;

            // 現在位置から分岐先まで、レール曲線に沿ったLineRenderer点列を作る。
            // Build LineRenderer points along the rail curve from current position to the selected branch.
            BuildBranchRoutePoints(firstSegmentStart);
            return _branchRoutePoints.Count >= 2;
        }

        private float ResolveFirstSegmentStart(global::Game.Train.RailPositions.RailPosition railPosition, IRailNode previousNode, IRailNode currentNode)
        {
            if (previousNode == null) return 0f;
            var segmentDistance = previousNode.GetDistanceToNode(currentNode);
            if (segmentDistance <= 0) return 0f;
            return Mathf.Clamp01(1f - railPosition.GetDistanceToNextNode() / (float)segmentDistance);
        }

        private bool TryAppendRouteToNextBranch(IRailNode previousNode, IRailNode currentNode, int branchSelectionIndex)
        {
            for (var nodeIndex = 0; nodeIndex < BranchRoutePreviewSearchNodeLimit; nodeIndex++)
            {
                CopyConnectedNodes(currentNode);
                if (_branchCandidateNodes.Count >= 2)
                {
                    var selectedNode = TrainUnitBranchSelector.SelectManualBranchNode(previousNode, currentNode, _branchCandidateNodes, branchSelectionIndex);
                    _branchRouteNodes.Add(selectedNode);
                    return true;
                }

                // 分岐ではない単一路線だけを先へ進む。
                // Continue only through a single non-branch route.
                if (_branchCandidateNodes.Count != 1) return false;
                var nextNode = _branchCandidateNodes[0];
                if (nextNode == null || nextNode == previousNode) return false;
                previousNode = currentNode;
                currentNode = nextNode;
                _branchRouteNodes.Add(currentNode);
            }
            return false;
        }

        private void CopyConnectedNodes(IRailNode node)
        {
            _branchCandidateNodes.Clear();
            foreach (var connectedNode in node.ConnectedNodes)
            {
                _branchCandidateNodes.Add(connectedNode);
            }
        }

        private void BuildBranchRoutePoints(float firstSegmentStart)
        {
            for (var i = 0; i < _branchRouteNodes.Count - 1; i++)
            {
                var startT = i == 0 ? firstSegmentStart : 0f;
                AppendBezierSegmentPoints(_branchRouteNodes[i], _branchRouteNodes[i + 1], startT);
            }
        }

        private void AppendBezierSegmentPoints(IRailNode fromNode, IRailNode toNode, float startT)
        {
            BezierUtility.BuildRenderControlPoints(fromNode.FrontControlPoint, toNode.BackControlPoint, out var p0, out var p1, out var p2, out var p3);
            for (var i = 0; i <= BranchRoutePreviewSamplesPerSegment; i++)
            {
                if (i == 0 && _branchRoutePoints.Count > 0) continue;
                var t = Mathf.Lerp(startT, 1f, i / (float)BranchRoutePreviewSamplesPerSegment);
                _branchRoutePoints.Add(BezierUtility.GetBezierPoint(p0, p1, p2, p3, t) + Vector3.up * BranchRoutePreviewHeightOffset);
            }
        }

        private void EnsureBranchRoutePreviewRenderer()
        {
            if (_branchRoutePreviewLine != null) return;

            // 警告色の赤ではなく、選択ルートとして読める黄橙のワールドラインを使う。
            // Use an amber world-space line so it reads as route selection rather than danger.
            _branchRoutePreviewObject = new GameObject("Train Branch Route Preview");
            _branchRoutePreviewLine = _branchRoutePreviewObject.AddComponent<LineRenderer>();
            _branchRoutePreviewLine.useWorldSpace = true;
            _branchRoutePreviewLine.startWidth = BranchRoutePreviewWidth;
            _branchRoutePreviewLine.endWidth = BranchRoutePreviewWidth;
            _branchRoutePreviewLine.numCornerVertices = 4;
            _branchRoutePreviewLine.numCapVertices = 4;
            _branchRoutePreviewLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _branchRoutePreviewLine.receiveShadows = false;

            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            _branchRoutePreviewMaterial = new Material(shader);
            _branchRoutePreviewMaterial.color = BranchRoutePreviewColor;
            _branchRoutePreviewLine.material = _branchRoutePreviewMaterial;
            _branchRoutePreviewLine.startColor = BranchRoutePreviewColor;
            _branchRoutePreviewLine.endColor = BranchRoutePreviewColor;
        }
    }
}
