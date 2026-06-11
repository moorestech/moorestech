using System;
using System.Collections.Generic;
using Client.Game.InGame.Train.View;
using Client.Game.InGame.Train.View.Object.Pose;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.SaveLoad;
using Game.Train.Unit;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Client.Tests
{
    public class TrainCarPoseCalculatorTest
    {
        [Test]
        public void TryGetPose_ResolvesHeadPose_WhenHeadSegmentHasZeroDistance()
        {
            // 駅同士の重なりを先頭セグメントに置き、後方の実セグメントで姿勢を補完する
            // Place the station overlap at the head segment and resolve pose from the rear real segment.
            var stationAhead = TestRailNode.Create(0, Vector3.zero);
            var stationBehind = TestRailNode.Create(1, Vector3.zero);
            var rear = TestRailNode.Create(2, new Vector3(0f, 0f, -1f));
            stationBehind.ConnectTo(stationAhead, 0);
            rear.ConnectTo(stationBehind, 1024);

            // 0距離セグメントを含むRailPositionをsnapshot復元後と同じ順序で作る
            // Build RailPosition in the same order as a restored snapshot with a zero-length segment.
            var railPosition = new RailPosition(new List<IRailNode> { stationAhead, stationBehind, rear }, 512, 0);
            var resolved = TrainCarPoseCalculator.TryGetPose(railPosition, 0, out var position, out var forward);

            // 先頭が重なりノード上にいても表示姿勢が失敗しないことを確認する
            // Verify the render pose still resolves when the head sits on overlapped nodes.
            Assert.IsTrue(resolved);
            Assert.That(Vector3.Distance(position, Vector3.zero), Is.LessThan(0.001f));
            Assert.That(forward.sqrMagnitude, Is.GreaterThan(0.5f));
        }

        [Test]
        public void TryGetPose_WalksAcrossInternalZeroDistanceSegment()
        {
            // 通常セグメントの後ろに駅重なりを置き、さらに後方セグメントへ距離を進める
            // Put a station overlap behind a normal segment and continue walking to the rear segment.
            var head = TestRailNode.Create(0, new Vector3(0f, 0f, 1f));
            var stationAhead = TestRailNode.Create(1, Vector3.zero);
            var stationBehind = TestRailNode.Create(2, Vector3.zero);
            var rear = TestRailNode.Create(3, new Vector3(0f, 0f, -1f));
            stationAhead.ConnectTo(head, 1024);
            stationBehind.ConnectTo(stationAhead, 0);
            rear.ConnectTo(stationBehind, 1024);

            // 車両後端側が0距離接続を越える位置を指定する
            // Request a pose beyond the zero-length connection on the rear side.
            var railPosition = new RailPosition(new List<IRailNode> { head, stationAhead, stationBehind, rear }, 1536, 0);
            var resolved = TrainCarPoseCalculator.TryGetPose(railPosition, 1280, out var position, out var forward);

            // 0距離接続で停止扱いにならず、後方の実セグメント上で姿勢が得られることを確認する
            // Verify the zero-length connection does not stop pose resolution before the rear segment.
            Assert.IsTrue(resolved);
            Assert.IsFalse(float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z));
            Assert.That(forward.sqrMagnitude, Is.GreaterThan(0.5f));
        }

        [Test]
        public void TryBuildPartSpan_ReflectsModelFrontCoordinates_WhenCarFacesBackward()
        {
            // reverse 後も model front 側の Engine が物理的な同じ側に残るよう span を反映する
            // Reflect spans so the model-front Engine remains on the same physical side after reverse.
            var engineLength = 13;
            var tenderLength = 7;
            var carFrontOffset = 0;
            var carRearOffset = 20;

            // forward では rail head 側から Engine/Tender の順に並ぶ
            // In forward mode, Engine/Tender are ordered from the rail head.
            Assert.IsTrue(TrainCarPartPoseCalculator.TryBuildPartSpan(carFrontOffset, carRearOffset, 0, engineLength, true, out var forwardEngine));
            Assert.IsTrue(TrainCarPartPoseCalculator.TryBuildPartSpan(carFrontOffset, carRearOffset, engineLength, tenderLength, true, out var forwardTender));

            // backward では rail head は入れ替わるが、Engine/Tender の物理側は入れ替わらない
            // In backward mode, the rail head swaps but Engine/Tender physical sides do not.
            Assert.IsTrue(TrainCarPartPoseCalculator.TryBuildPartSpan(carFrontOffset, carRearOffset, 0, engineLength, false, out var backwardEngine));
            Assert.IsTrue(TrainCarPartPoseCalculator.TryBuildPartSpan(carFrontOffset, carRearOffset, engineLength, tenderLength, false, out var backwardTender));
            Assert.AreEqual(0, forwardEngine.FrontOffset);
            Assert.AreEqual(13, forwardEngine.RearOffset);
            Assert.AreEqual(13, forwardTender.FrontOffset);
            Assert.AreEqual(20, forwardTender.RearOffset);
            Assert.AreEqual(7, backwardEngine.FrontOffset);
            Assert.AreEqual(20, backwardEngine.RearOffset);
            Assert.AreEqual(0, backwardTender.FrontOffset);
            Assert.AreEqual(7, backwardTender.RearOffset);
        }

        [Test]
        public void TryBuildPartSpanByRatio_AllowsOverlappingSiblingRatios()
        {
            // overlapする兄弟spanは互いに独立した描画範囲として許可する
            // Allow overlapping sibling spans as independent visual ranges.
            Assert.IsTrue(TrainCarPartPoseCalculator.TryBuildPartSpanByRatio(0, 20, 0f, 0.75f, true, out var frontPart));
            Assert.IsTrue(TrainCarPartPoseCalculator.TryBuildPartSpanByRatio(0, 20, 0.5f, 1f, true, out var rearPart));

            // 兄弟同士のgapやoverlapはここでは正規化せず、指定値をそのまま反映する
            // Do not normalize sibling gaps or overlaps here; reflect each requested range as-is.
            Assert.AreEqual(0, frontPart.FrontOffset);
            Assert.AreEqual(15, frontPart.RearOffset);
            Assert.AreEqual(10, rearPart.FrontOffset);
            Assert.AreEqual(20, rearPart.RearOffset);
        }

        [Test]
        public void TryBuildPartSpanByRatio_ComposesNestedRanges()
        {
            // 親span内の比率から子spanを作り、さらにその中の比率で孫spanを作る
            // Build a child span from ratios inside the parent, then a grandchild span inside it.
            Assert.IsTrue(TrainCarPartPoseCalculator.TryBuildPartSpanByRatio(0, 20, 0f, 0.5f, true, out var childPart));
            Assert.IsTrue(TrainCarPartPoseCalculator.TryBuildPartSpanByRatio(childPart.FrontOffset, childPart.RearOffset, 0.5f, 1f, true, out var grandChildPart));

            // 再帰適用では0-0.5の子内0.5-1.0が車両全体の0.25-0.5になる
            // Recursive application maps child 0.5-1.0 inside 0-0.5 to whole-car 0.25-0.5.
            Assert.AreEqual(0, childPart.FrontOffset);
            Assert.AreEqual(10, childPart.RearOffset);
            Assert.AreEqual(5, grandChildPart.FrontOffset);
            Assert.AreEqual(10, grandChildPart.RearOffset);
        }

        [Test]
        public void BuildRotation_KeepsModelVisualDirection_WhenRailHeadSwapsOnReverse()
        {
            // reverse では rail head だけが入れ替わり、モデル見た目の向きは変えない
            // On reverse, only the rail head swaps while the model visual direction stays unchanged.
            var forwardRotation = TrainCarPoseCalculator.BuildRotation(Vector3.forward, true);
            var backwardRotation = TrainCarPoseCalculator.BuildRotation(Vector3.back, false);

            // rail forward と facing 反転が相殺され、余計な反転が入らないことを確認する
            // Verify rail-forward and facing inversion cancel without applying an extra visual flip.
            Assert.That(Quaternion.Angle(forwardRotation, backwardRotation), Is.LessThan(0.001f));
        }

        [Test]
        public void ResolveModelForwardCenterOffset_ProjectsBoundsOntoCorrectedLocalForwardAxis()
        {
            // モデル長手方向がlocal Xの車両で、Z位置に引っ張られないことを確認する
            // Verify a local-X-forward train model is not offset by its local Z position.
            var root = new GameObject("TrainCarPoseCalculatorTestRoot");
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(-7f, 0f, 3f);

            // renderer中心をlocal forward軸へ射影し、pivotから中心までの補正量を得る
            // Project the renderer center onto the local forward axis to get the pivot-center offset.
            var offset = TrainCarRailPositionVisualUtility.ResolveModelForwardCenterOffset(root.transform);
            UnityEngine.Object.DestroyImmediate(root);

            // ModelYawOffsetDegrees=-90ではlocal forwardが+Xなので、期待値はZではなくXになる
            // With ModelYawOffsetDegrees=-90, local forward is +X, so the expected value is X, not Z.
            Assert.That(offset, Is.EqualTo(-7f).Within(0.001f));
        }

        [Test]
        public void LocomotiveVisualPoseUpdater_KeepsEngineAndTenderConnected_OnStraightRail()
        {
            // 実Prefabを直線レール上に配置し、Engine/Tender接続部の見た目gapを測る
            // Place the real Prefab on straight rail and measure the visual gap at the Engine/Tender joint.
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AddressableResources/Train/Locomotive.prefab");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var updated = false;
            var jointGap = float.PositiveInfinity;

            // 実controller経由で描画姿勢を解決し、本番と同じ再帰applierを通す
            // Resolve the visual pose through the real controller so the recursive applier path is covered.
            var head = TestRailNode.Create(100, new Vector3(0f, 0f, 20f));
            var rear = TestRailNode.Create(101, Vector3.zero);
            rear.ConnectTo(head, TrainLengthConverter.ToRailUnits(20));
            var railPosition = new RailPosition(new List<IRailNode> { head, rear }, TrainLengthConverter.ToRailUnits(20), 0);
            var poseUpdater = instance.GetComponent<TrainCarRailPositionVisualPoseUpdater>();
            updated = poseUpdater.UpdatePose(TrainCarRailPositionVisualState.Create(railPosition, 0, TrainLengthConverter.ToRailUnits(20), true));

            // 進行方向へrenderer boundsを射影し、Engine後端とTender前端の距離を比較する
            // Project renderer bounds along the travel direction and compare the Engine rear with the Tender front.
            var engine = instance.transform.Find("VisualRoot/Engine");
            var tender = instance.transform.Find("VisualRoot/Tender");
            var modelForward = TrainCarPoseCalculator.ResolveModelForward(engine.rotation).normalized;
            var engineRange = ProjectRendererBounds(engine, modelForward);
            var tenderRange = ProjectRendererBounds(tender, modelForward);
            jointGap = engineRange.Min - tenderRange.Max;
            UnityEngine.Object.DestroyImmediate(instance);

            // 正のgapは分離、負のgapは軽いoverlapとして扱う
            // A positive gap means separation, while a negative gap means a small visual overlap.
            Assert.IsTrue(updated);
            Assert.That(jointGap, Is.LessThan(0.25f));
            Assert.That(jointGap, Is.GreaterThan(-1.25f));
        }

        private static ProjectionRange ProjectRendererBounds(Transform root, Vector3 axis)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var min = float.PositiveInfinity;
            var max = float.NegativeInfinity;

            // world AABBの各cornerを進行方向へ射影し、見た目の前後端を得る
            // Project each world AABB corner onto the travel axis to get visual front/rear ends.
            for (var i = 0; i < renderers.Length; i++)
            {
                var bounds = renderers[i].bounds;
                Accumulate(bounds.min.x, bounds.min.y, bounds.min.z);
                Accumulate(bounds.min.x, bounds.min.y, bounds.max.z);
                Accumulate(bounds.min.x, bounds.max.y, bounds.min.z);
                Accumulate(bounds.min.x, bounds.max.y, bounds.max.z);
                Accumulate(bounds.max.x, bounds.min.y, bounds.min.z);
                Accumulate(bounds.max.x, bounds.min.y, bounds.max.z);
                Accumulate(bounds.max.x, bounds.max.y, bounds.min.z);
                Accumulate(bounds.max.x, bounds.max.y, bounds.max.z);
            }

            return new ProjectionRange(min, max);

            #region Internal

            void Accumulate(float x, float y, float z)
            {
                var projected = Vector3.Dot(new Vector3(x, y, z), axis);
                min = Mathf.Min(min, projected);
                max = Mathf.Max(max, projected);
            }

            #endregion
        }

        private readonly struct ProjectionRange
        {
            public readonly float Min;
            public readonly float Max;

            public ProjectionRange(float min, float max)
            {
                Min = min;
                Max = max;
            }
        }

        private sealed class TestRailNode : IRailNode
        {
            private readonly Dictionary<IRailNode, int> _distances = new();

            private TestRailNode(int nodeId, Vector3 position)
            {
                // テスト用RailNodeの識別子と制御点を固定する
                // Initialize deterministic identifiers and control points for tests.
                NodeId = nodeId;
                NodeGuid = Guid.NewGuid();
                ConnectionDestination = new ConnectionDestination(new Vector3Int(nodeId, 0, 0), 0, true);
                FrontControlPoint = new RailControlPoint(position, Vector3.forward);
                BackControlPoint = new RailControlPoint(position, Vector3.forward);
                StationRef = new StationReference();
            }

            public int NodeId { get; }
            public int OppositeNodeId => -1;
            public IRailNode OppositeNode => null;
            public ConnectionDestination ConnectionDestination { get; }
            public Guid NodeGuid { get; }
            public IRailGraphProvider GraphProvider => null;
            public StationReference StationRef { get; }
            public RailControlPoint FrontControlPoint { get; }
            public RailControlPoint BackControlPoint { get; }
            public IEnumerable<IRailNode> ConnectedNodes => _distances.Keys;
            public IEnumerable<(IRailNode node, int distance)> ConnectedNodesWithDistance
            {
                get
                {
                    foreach (var pair in _distances)
                    {
                        yield return (pair.Key, pair.Value);
                    }
                }
            }

            public static TestRailNode Create(int nodeId, Vector3 position)
            {
                return new TestRailNode(nodeId, position);
            }

            public void ConnectTo(IRailNode node, int distance)
            {
                _distances[node] = distance;
            }

            public int GetDistanceToNode(IRailNode node, bool useFindPath)
            {
                return _distances.TryGetValue(node, out var distance) ? distance : -1;
            }
        }
    }
}
