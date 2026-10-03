using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Game.InGame.Map.MapObject
{
    /// <summary>
    ///     CullingGroupの距離bandを時間予算付きRenderer更新へ変換する
    ///     Converts CullingGroup distance bands into frame-budgeted Renderer updates
    /// </summary>
    internal sealed class MapObjectDistanceVisibilityController
    {
        private const float RestoreDistance = 10f;
        private const float HideDistance = 20f;
        private const double FrameBudgetMilliseconds = 1.0;
        private static readonly float[] DistanceBands = { RestoreDistance, HideDistance };

        private readonly CancellationToken _cancellationToken;
        private readonly CullingGroup _cullingGroup = new();
        private readonly BoundingSphere[] _boundingSpheres;
        private readonly MapObjectRendererVisibility[] _visibilityByIndex;
        private readonly bool[] _pendingVisibilityByIndex;
        private readonly bool[] _hasPendingVisibilityByIndex;
        private readonly bool[] _isQueuedByIndex;
        private readonly Queue<int> _pendingIndices = new();
        private Camera _camera;
        private int _registeredCount;
        private bool _isProcessing;
        private bool _isShutdown;

        public MapObjectDistanceVisibilityController(int capacity, CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
            _boundingSpheres = new BoundingSphere[capacity];
            _visibilityByIndex = new MapObjectRendererVisibility[capacity];
            _pendingVisibilityByIndex = new bool[capacity];
            _hasPendingVisibilityByIndex = new bool[capacity];
            _isQueuedByIndex = new bool[capacity];

            // native側へ固定長配列を一度だけ渡し、登録数だけを後から伸ばす
            // Give native code one fixed array and grow only the active sphere count
            _cullingGroup.SetBoundingDistances(DistanceBands);
            _cullingGroup.SetBoundingSpheres(_boundingSpheres);
            _cullingGroup.SetBoundingSphereCount(0);
            _cullingGroup.onStateChanged = OnStateChanged;
        }

        public void SetCamera(Camera camera)
        {
            if (_isShutdown) return;
            _camera = camera;
            _cullingGroup.targetCamera = camera;
            if (camera == null) return;
            _cullingGroup.SetDistanceReferencePoint(camera.transform);

            // camera切替直後はnative通知を待たず全個体を新しい距離基準へ揃える
            // Rebase every object immediately after a camera switch instead of waiting for native callbacks
            for (var index = 0; index < _registeredCount; index++)
            {
                ApplyDistanceBand(index, GetDistanceBand(_boundingSpheres[index].position));
            }
        }

        public void Register(MapObjectGameObject mapObject, bool isLandmark)
        {
            if (isLandmark) return;
            if (_registeredCount == _boundingSpheres.Length)
                throw new InvalidOperationException("MapObject distance visibility capacity exceeded.");

            var index = _registeredCount;
            _registeredCount++;
            _visibilityByIndex[index] = new MapObjectRendererVisibility(mapObject);
            _boundingSpheres[index] = new BoundingSphere(mapObject.transform.position, 0f);
            _cullingGroup.SetBoundingSphereCount(_registeredCount);

            if (_camera != null) ApplyDistanceBand(index, GetDistanceBand(mapObject.transform.position));
        }

        internal void ApplyDistanceBand(int index, int distanceBand)
        {
            if (_isShutdown || _cancellationToken.IsCancellationRequested) return;

            // 中間bandは現在状態を保持するため、同frameの未反映要求も取り消す
            // The middle band preserves current state, including canceling an unapplied same-frame request
            if (distanceBand == 1)
            {
                _hasPendingVisibilityByIndex[index] = false;
                return;
            }

            _pendingVisibilityByIndex[index] = distanceBand == 0;
            _hasPendingVisibilityByIndex[index] = true;
            if (!_isQueuedByIndex[index])
            {
                _pendingIndices.Enqueue(index);
                _isQueuedByIndex[index] = true;
            }

            if (_isProcessing) return;
            _isProcessing = true;
            ProcessPendingAsync().Forget();
        }

        public void Shutdown()
        {
            if (_isShutdown) return;
            _isShutdown = true;
            _pendingIndices.Clear();
            _cullingGroup.onStateChanged = null;
            _cullingGroup.Dispose();
        }

        private void OnStateChanged(CullingGroupEvent sphereEvent)
        {
            // 視錐台の出入りだけでも通知が来るため、距離bandが変わらない通知はqueueへ積まない
            // Notifications also fire for frustum transitions, so drop events that keep the distance band
            if (sphereEvent.previousDistance == sphereEvent.currentDistance) return;
            ApplyDistanceBand(sphereEvent.index, sphereEvent.currentDistance);
        }

        private int GetDistanceBand(Vector3 position)
        {
            var distance = Vector3.Distance(_camera.transform.position, position);
            if (distance < RestoreDistance) return 0;
            return distance < HideDistance ? 1 : 2;
        }

        private async UniTask ProcessPendingAsync()
        {
            // native callback群を同frameで集約してからRenderer更新を始める
            // Batch native callbacks from the same frame before applying Renderer changes
            await UniTask.Yield();
            if (ShouldStopProcessing()) return;
            var budget = new FrameTimeBudget(FrameBudgetMilliseconds);

            while (0 < _pendingIndices.Count)
            {
                var index = _pendingIndices.Dequeue();
                _isQueuedByIndex[index] = false;
                if (_hasPendingVisibilityByIndex[index])
                {
                    _hasPendingVisibilityByIndex[index] = false;
                    _visibilityByIndex[index].SetVisible(_pendingVisibilityByIndex[index]);
                }

                if (!budget.IsExhausted || _pendingIndices.Count == 0) continue;
                await UniTask.Yield();
                if (ShouldStopProcessing()) return;
                budget.Restart();
            }

            _isProcessing = false;
        }

        private bool ShouldStopProcessing()
        {
            if (!_isShutdown && !_cancellationToken.IsCancellationRequested) return false;
            _isProcessing = false;
            return true;
        }
    }
}
