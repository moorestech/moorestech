using System;
using System.Collections.Generic;
using Client.Game.InGame.Train.View;
using UnityEngine;

namespace Client.Game.InGame.Train.View.Object
{
    [DisallowMultipleComponent]
    public sealed class TrainCarRailPositionVisualApplier : MonoBehaviour
    {
        [SerializeField] private float frontOffset;
        [SerializeField] private float rearOffset = 1f;

        private readonly List<TrainCarRailPositionVisualApplier> _childApplierBuffer = new();
        private TrainCarRailPositionVisualApplier[] _childAppliers = Array.Empty<TrainCarRailPositionVisualApplier>();
        private float _modelForwardCenterOffset;
        private bool _isInitialized;

        public bool ApplyVisualState(TrainCarRailPositionVisualState visualState)
        {
            // Prefab階層から論理子applierを初回だけ解決する
            // Resolve logical child appliers from the Prefab hierarchy only once
            EnsureInitialized();
            return ApplyVisualStateRecursive(visualState);
        }

        private void EnsureInitialized()
        {
            if (_isInitialized)
            {
                return;
            }

            // このapplier自身の姿勢補正値と最近傍の子applierだけを保持する
            // Cache this applier pose offset and only the nearest child appliers
            _modelForwardCenterOffset = TrainCarRailPositionVisualUtility.ResolveModelForwardCenterOffset(transform);
            _childAppliers = ResolveDirectChildAppliers();
            for (var i = 0; i < _childAppliers.Length; i++)
            {
                _childAppliers[i].EnsureInitialized();
            }
            _isInitialized = true;
        }

        private bool ApplyVisualStateRecursive(TrainCarRailPositionVisualState parentVisualState)
        {
            if (!TryBuildLocalVisualState(parentVisualState, out var localVisualState))
            {
                return false;
            }

            // 自分のspanへ姿勢を合わせてから子applierへ再帰的に分配する
            // Align this span first and then distribute it recursively to child appliers
            if (!TrainCarRailPositionVisualUtility.TryResolvePose(localVisualState, _modelForwardCenterOffset, out var pose))
            {
                return false;
            }
            transform.SetPositionAndRotation(pose.Position, pose.Rotation);

            for (var i = 0; i < _childAppliers.Length; i++)
            {
                if (!_childAppliers[i].ApplyVisualStateRecursive(localVisualState))
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
                    frontOffset,
                    rearOffset,
                    parentVisualState.IsFacingForward,
                    out var span))
            {
                return false;
            }

            // 親span内の比率から、このapplierが担当するrailposition spanを作る
            // Build the railposition span this applier owns from ratios inside the parent span
            localVisualState = TrainCarRailPositionVisualState.Create(
                parentVisualState.RailPosition,
                span.FrontOffset,
                span.RearOffset,
                parentVisualState.IsFacingForward);
            return true;
        }

        private TrainCarRailPositionVisualApplier[] ResolveDirectChildAppliers()
        {
            _childApplierBuffer.Clear();
            for (var i = 0; i < transform.childCount; i++)
            {
                CollectNearestChildAppliers(transform.GetChild(i));
            }
            return _childApplierBuffer.ToArray();
        }

        private void CollectNearestChildAppliers(Transform current)
        {
            if (current.TryGetComponent<TrainCarRailPositionVisualApplier>(out var childApplier))
            {
                _childApplierBuffer.Add(childApplier);
                return;
            }

            // applierを持たない中間GameObjectは透明な階層として下へ探索する
            // Treat intermediate GameObjects without an applier as transparent hierarchy nodes
            for (var i = 0; i < current.childCount; i++)
            {
                CollectNearestChildAppliers(current.GetChild(i));
            }
        }
    }
}
