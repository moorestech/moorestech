using System.Collections.Generic;
using Game.Block.Component;
using Game.Fluid;

namespace Game.Block.Blocks.Fluid
{
    internal class FluidPipeTransferService
    {
        private readonly FluidContainer _fluidContainer;
        private readonly Dictionary<FluidContainer, FluidPipeSourceBucket> _pendingBySource;
        private readonly FluidPipeConnectorTargetCache _targetCache;
        private readonly int _blockedRetryTicks;
        private readonly List<FluidContainer> _bucketKeys = new();
        private readonly List<FluidContainer> _orphanKeys = new();
        private readonly List<FluidPipeTransferTarget> _eligibleTargets = new();

        public FluidPipeTransferService(
            FluidContainer fluidContainer,
            Dictionary<FluidContainer, FluidPipeSourceBucket> pendingBySource,
            BlockConnectorComponent<IFluidInventory> connectorComponent,
            int blockedRetryTicks)
        {
            _fluidContainer = fluidContainer;
            _pendingBySource = pendingBySource;
            _targetCache = new FluidPipeConnectorTargetCache(connectorComponent);
            _blockedRetryTicks = blockedRetryTicks;
        }

        public bool Update()
        {
            MergeOrphanSources();
            CopyBucketKeys();

            var transferredAny = false;
            foreach (var key in _bucketKeys)
            {
                if (!_pendingBySource.TryGetValue(key, out var bucket)) continue;

                // 空バケットは即時削除して次 tick の走査対象を減らす
                // Remove empty buckets immediately to shrink later tick scans.
                if (bucket.Amount <= 0)
                {
                    _pendingBySource.Remove(key);
                    continue;
                }

                var targetCount = CollectEligibleTargets(key);
                if (targetCount == 0)
                {
                    HandleBlocked(key, bucket);
                    continue;
                }

                var sent = DistributeEqually(bucket.Amount, targetCount);
                ApplyTransferResult(key, bucket, sent);
                if (sent > 0) transferredAny = true;
            }

            return transferredAny;
        }

        public double SumBuckets()
        {
            var sum = 0.0;
            foreach (var bucket in _pendingBySource.Values) sum += bucket.Amount;
            return sum;
        }

        private void CopyBucketKeys()
        {
            _bucketKeys.Clear();
            foreach (var key in _pendingBySource.Keys) _bucketKeys.Add(key);
        }

        private void MergeOrphanSources()
        {
            _orphanKeys.Clear();
            foreach (var key in _pendingBySource.Keys)
            {
                if (key == FluidContainer.Empty) continue;
                if (key.IsEmpty) continue;
                if (_targetCache.ContainsSourceContainer(key)) continue;
                _orphanKeys.Add(key);
            }

            foreach (var orphanKey in _orphanKeys)
            {
                var bucket = _pendingBySource[orphanKey];
                _pendingBySource.Remove(orphanKey);
                AddToEmptyBucket(bucket.Amount);
            }
        }

        private int CollectEligibleTargets(FluidContainer sourceKey)
        {
            _eligibleTargets.Clear();
            var targets = _targetCache.GetTargets();
            foreach (var target in targets)
            {
                if (target.MaxFlowAmountPerTick <= 0) continue;
                if (sourceKey != FluidContainer.Empty && target.SourceContainer == sourceKey) continue;
                _eligibleTargets.Add(target);
            }

            return _eligibleTargets.Count;
        }

        private double DistributeEqually(double bucketAmount, int targetCount)
        {
            var sharePerTarget = bucketAmount / targetCount;
            var transferred = 0.0;

            // 候補数で等分し、接続ごとの最大流量で上限をかける
            // Split across candidates and cap by each connection's max flow.
            foreach (var target in _eligibleTargets)
            {
                var sendAmount = sharePerTarget < target.MaxFlowAmountPerTick ? sharePerTarget : target.MaxFlowAmountPerTick;
                if (sendAmount <= 0) continue;

                var stack = new FluidStack(sendAmount, _fluidContainer.FluidId);
                var remain = target.Inventory.AddLiquid(stack, _fluidContainer);
                transferred += sendAmount - remain.Amount;
            }

            return transferred;
        }

        private void ApplyTransferResult(FluidContainer key, FluidPipeSourceBucket bucket, double sent)
        {
            bucket.Amount -= sent;
            if (bucket.Amount <= 0)
            {
                _pendingBySource.Remove(key);
                return;
            }

            if (sent > 0)
            {
                bucket.BlockedTicks = 0;
                _pendingBySource[key] = bucket;
                return;
            }

            HandleBlocked(key, bucket);
        }

        private void HandleBlocked(FluidContainer key, FluidPipeSourceBucket bucket)
        {
            bucket.BlockedTicks++;
            _pendingBySource[key] = bucket;
            TryDemote(key, bucket);
        }

        private void TryDemote(FluidContainer key, FluidPipeSourceBucket bucket)
        {
            if (key == FluidContainer.Empty) return;
            if (bucket.BlockedTicks < _blockedRetryTicks) return;

            _pendingBySource.Remove(key);
            AddToEmptyBucket(bucket.Amount);
        }

        private void AddToEmptyBucket(double amount)
        {
            if (amount <= 0) return;
            var emptyBucket = _pendingBySource.GetValueOrDefault(FluidContainer.Empty);
            emptyBucket.Amount += amount;
            _pendingBySource[FluidContainer.Empty] = emptyBucket;
        }
    }
}
