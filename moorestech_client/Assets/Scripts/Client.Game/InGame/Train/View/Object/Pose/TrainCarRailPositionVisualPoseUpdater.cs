using System;
using System.Collections.Generic;
using Client.Game.InGame.Train.View;
using UnityEngine;

namespace Client.Game.InGame.Train.View.Object.Pose
{
    [DisallowMultipleComponent]
    public sealed class TrainCarRailPositionVisualPoseUpdater : MonoBehaviour, ITrainCarPoseUpdater
    {
        [SerializeField] private float frontOffsetRatio;
        [SerializeField] private float rearOffsetRatio = 1f;

        private TrainCarRailPositionVisualPoseUpdater[] _childPoseUpdaters = Array.Empty<TrainCarRailPositionVisualPoseUpdater>();
        private float _modelForwardCenterOffset;
        private bool _isBuilt;

        public bool UpdatePose(TrainCarRailPositionVisualState visualState)
        {
            // Prefab 階層からこの updater tree を初回だけ build する
            // Build this updater tree from the Prefab hierarchy only once
            Build();
            return UpdatePoseRecursive(visualState);
        }

        public bool CollectPoseRequests(TrainCarRailPositionVisualState visualState, TrainCarRailPositionPoseBatch poseBatch)
        {
            // 通常描画では先に全visual spanの端点だけをbatchへ登録する
            // In runtime rendering, register all visual-span endpoints into the batch first
            Build();
            return CollectPoseRequestsRecursive(visualState, poseBatch);
        }

        public bool ApplyBatchedPose(TrainCarRailPositionVisualState visualState, TrainCarRailPositionPoseBatch poseBatch)
        {
            // batch解決済みの端点からTransformだけを更新する
            // Apply Transforms only after the batch has resolved all requested endpoints
            Build();
            return ApplyBatchedPoseRecursive(visualState, poseBatch);
        }

        private void Build()
        {
            if (_isBuilt)
            {
                return;
            }

            // 自身の姿勢補正値と直近の子 updater だけを保持する
            // Cache this updater pose offset and only the nearest child updaters
            _modelForwardCenterOffset = TrainCarRailPositionVisualUtility.ResolveModelForwardCenterOffset(transform);
            _childPoseUpdaters = BuildNearestChildPoseUpdaters();
            for (var i = 0; i < _childPoseUpdaters.Length; i++)
            {
                _childPoseUpdaters[i].Build();
            }
            _isBuilt = true;
        }

        private bool UpdatePoseRecursive(TrainCarRailPositionVisualState parentVisualState)
        {
            if (!TryBuildLocalVisualState(parentVisualState, out var localVisualState))
            {
                return false;
            }

            // 自分の span に姿勢を合わせてから子 updater へ再帰的に分配する
            // Align this span first and then distribute it recursively to child updaters
            if (!TrainCarRailPositionVisualUtility.TryResolvePose(localVisualState, _modelForwardCenterOffset, out var pose))
            {
                return false;
            }
            transform.SetPositionAndRotation(pose.Position, pose.Rotation);

            for (var i = 0; i < _childPoseUpdaters.Length; i++)
            {
                if (!_childPoseUpdaters[i].UpdatePoseRecursive(localVisualState))
                {
                    return false;
                }
            }
            return true;
        }

        private bool CollectPoseRequestsRecursive(TrainCarRailPositionVisualState parentVisualState, TrainCarRailPositionPoseBatch poseBatch)
        {
            if (!TryBuildLocalVisualState(parentVisualState, out var localVisualState))
            {
                return false;
            }

            // このupdater自身のspanを登録してから子spanを再帰的に登録する
            // Register this updater span first, then recursively register child spans
            if (!poseBatch.RequestPose(localVisualState))
            {
                return false;
            }
            for (var i = 0; i < _childPoseUpdaters.Length; i++)
            {
                if (!_childPoseUpdaters[i].CollectPoseRequestsRecursive(localVisualState, poseBatch))
                {
                    return false;
                }
            }
            return true;
        }

        private bool ApplyBatchedPoseRecursive(TrainCarRailPositionVisualState parentVisualState, TrainCarRailPositionPoseBatch poseBatch)
        {
            if (!TryBuildLocalVisualState(parentVisualState, out var localVisualState))
            {
                return false;
            }

            // batchが解決した前後端点を使い、既存と同じTransform更新を行う
            // Use batch-resolved front/rear points and update Transform in the same way as before
            if (!poseBatch.TryGetPose(localVisualState, _modelForwardCenterOffset, out var pose))
            {
                return false;
            }
            transform.SetPositionAndRotation(pose.Position, pose.Rotation);

            for (var i = 0; i < _childPoseUpdaters.Length; i++)
            {
                if (!_childPoseUpdaters[i].ApplyBatchedPoseRecursive(localVisualState, poseBatch))
                {
                    return false;
                }
            }
            return true;
        }

        private bool TryBuildLocalVisualState(TrainCarRailPositionVisualState parentVisualState, out TrainCarRailPositionVisualState localVisualState)
        {
            localVisualState = default;
            if (!TrainCarPartPoseCalculator.TryBuildPartSpanByRatio(
                    parentVisualState.FrontOffset,
                    parentVisualState.RearOffset,
                    frontOffsetRatio,
                    rearOffsetRatio,
                    parentVisualState.IsFacingForward,
                    out var span))
            {
                return false;
            }

            // 親 span 内の比率からこの updater が担当する railposition span を作る
            // Build the railposition span this updater owns from ratios inside the parent span
            localVisualState = TrainCarRailPositionVisualState.Create(
                parentVisualState.RailPosition,
                span.FrontOffset,
                span.RearOffset,
                parentVisualState.IsFacingForward);
            return true;
        }

        private TrainCarRailPositionVisualPoseUpdater[] BuildNearestChildPoseUpdaters()
        {
            var childPoseUpdaters = new List<TrainCarRailPositionVisualPoseUpdater>();
            for (var i = 0; i < transform.childCount; i++)
            {
                CollectNearestChildPoseUpdaters(transform.GetChild(i), childPoseUpdaters);
            }
            return childPoseUpdaters.ToArray();
        }

        private static void CollectNearestChildPoseUpdaters(Transform current, List<TrainCarRailPositionVisualPoseUpdater> childPoseUpdaters)
        {
            if (current.TryGetComponent<TrainCarRailPositionVisualPoseUpdater>(out var childPoseUpdater))
            {
                childPoseUpdaters.Add(childPoseUpdater);
                return;
            }

            // updater を持たない中間 GameObject は透明な階層として子を探索する
            // Treat intermediate GameObjects without an updater as transparent hierarchy nodes
            for (var i = 0; i < current.childCount; i++)
            {
                CollectNearestChildPoseUpdaters(current.GetChild(i), childPoseUpdaters);
            }
        }
    }
}
