using System;
using System.Threading;
using Client.Game.Common;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.Map.MapObject
{
    /// <summary>
    ///     近傍と後着の生成loopと完了状態を所有する
    ///     Owns near-field and background loops plus their completion state
    /// </summary>
    internal sealed class MapObjectInstantiationRunner
    {
        private const double NearFieldFrameBudgetMilliseconds = 16.0;
        private const double BackgroundFrameBudgetMilliseconds = 4.0;

        private readonly MapObjectLayoutInstantiator _instantiator;
        private readonly MapObjectLayoutDistanceOrder.NearFieldOrder _nearFieldOrder;
        private readonly MapObjectRegistry _registry;
        private readonly IReadOnlyReactiveProperty<bool> _isWorldObjectActive;
        private readonly CancellationToken _cancellationToken;
        private readonly MapObjectInstantiationCompletion _nearFieldCompletion = new();
        private readonly MapObjectInstantiationCompletion _allCompletion = new();
        private bool _backgroundStarted;

        public IReadOnlyReactiveProperty<bool> IsNearFieldInstantiated => _nearFieldCompletion.GetSuccessfulCompletionState();
        public IReadOnlyReactiveProperty<bool> IsAllInstantiated => _allCompletion.GetSuccessfulCompletionState();

        public MapObjectInstantiationRunner(
            MapObjectLayoutInstantiator instantiator,
            MapObjectLayoutDistanceOrder.NearFieldOrder nearFieldOrder,
            MapObjectRegistry registry,
            IReadOnlyReactiveProperty<bool> isWorldObjectActive,
            CancellationToken cancellationToken)
        {
            _instantiator = instantiator;
            _nearFieldOrder = nearFieldOrder;
            _registry = registry;
            _isWorldObjectActive = isWorldObjectActive;
            _cancellationToken = cancellationToken;
        }

        public UniTask WaitForNearFieldInstantiationAsync()
        {
            return _nearFieldCompletion.WaitAsync();
        }

        public void StartNearFieldInstantiation()
        {
            InstantiateNearFieldAndSettleAsync().Forget(HandleNearFieldFailure);
        }

        public void StartBackgroundInstantiation()
        {
            if (_backgroundStarted)
                throw new InvalidOperationException("[MapObjectInstantiationRunner] 後着生成が二重に開始されました");

            _backgroundStarted = true;
            InstantiateBackgroundAndSettleAsync().Forget(HandleBackgroundFailure);
        }

        private async UniTask InstantiateNearFieldAndSettleAsync()
        {
            var failureCount = await InstantiateRangeAsync(
                0, _nearFieldOrder.NearFieldCount, NearFieldFrameBudgetMilliseconds, false);
            if (0 < failureCount)
            {
                var exception = CreateIncompleteException("near-field", failureCount);
                _nearFieldCompletion.Fail(exception);
                FailAll(exception);
                return;
            }

            _nearFieldCompletion.Complete();
        }

        private async UniTask InstantiateBackgroundAndSettleAsync()
        {
            // 明示開始後に開始イベントを待つ
            // Await the start event after the explicit push
            await GameInitializedEvent.OnGameInitialized.Take(1).ToUniTask(cancellationToken: _cancellationToken);
            var failureCount = await InstantiateRangeAsync(
                _nearFieldOrder.NearFieldCount,
                _nearFieldOrder.Entries.Count,
                BackgroundFrameBudgetMilliseconds,
                true);

            if (0 < failureCount)
            {
                FailAll(CreateIncompleteException("background", failureCount));
                return;
            }

            _allCompletion.Complete();
        }

        private void HandleNearFieldFailure(Exception exception)
        {
            if (exception is OperationCanceledException)
            {
                _nearFieldCompletion.Cancel();
                _allCompletion.Cancel();
                return;
            }

            _nearFieldCompletion.Fail(exception);
            FailAll(exception);
        }

        private void HandleBackgroundFailure(Exception exception)
        {
            if (exception is OperationCanceledException)
            {
                _allCompletion.Cancel();
                return;
            }

            FailAll(exception);
        }

        private void FailAll(Exception exception)
        {
            // 全量faultを観測して記録する
            // Observe and report the full-set fault
            _allCompletion.Fail(exception);
            _allCompletion.WaitAsync().Forget(failure => Debug.LogError($"MapObject full instantiation failed. {failure}"));
        }

        private async UniTask<int> InstantiateRangeAsync(
            int startIndex,
            int endIndexExclusive,
            double frameBudgetMilliseconds,
            bool waitsForWorldObjectActive)
        {
            var failureCount = 0;
            var budget = new FrameTimeBudget(frameBudgetMilliseconds);
            for (var index = startIndex; index < endIndexExclusive; index++)
            {
                // load前後で活性を確定する
                // Confirm activity on both sides of prefab loading
                var layout = _nearFieldOrder.Entries[index].Layout;
                await WaitForWorldObjectActiveAsync();
                var prefab = await _instantiator.ResolvePrefabOrNullAsync(layout, _cancellationToken);
                await WaitForWorldObjectActiveAsync();

                if (!_instantiator.InstantiateFromLayout(layout, prefab))
                {
                    _registry.DiscardPendingState(layout.InstanceId);
                    failureCount++;
                }

                if (!budget.IsExhausted) continue;
                await UniTask.Yield(_cancellationToken);
                budget.Restart();
            }

            return failureCount;

            #region Internal

            async UniTask WaitForWorldObjectActiveAsync()
            {
                if (!waitsForWorldObjectActive || _isWorldObjectActive.Value) return;

                // 復帰待ちで積んだ経過時間を捨てる
                // Drop elapsed time accumulated while awaiting reactivation
                await _isWorldObjectActive.Where(static isActive => isActive).ToUniTask(true, _cancellationToken);
                budget.Restart();
            }

            #endregion
        }

        private static InvalidOperationException CreateIncompleteException(string phase, int failureCount)
        {
            return new InvalidOperationException($"MapObject {phase} instantiation skipped {failureCount} instance(s).");
        }
    }
}
