using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Fluid;
using MessagePack;
using Mooresmaster.Model.BlockConnectInfoModule;
using Newtonsoft.Json;
using UniRx;

namespace Game.Block.Blocks.Fluid
{
    public class FluidPipeComponent : IUpdatableBlockComponent, IFluidInventory, IBlockStateObservable
    {
        public IObservable<Unit> OnChangeBlockState => _onChangeBlockState;

        private readonly FluidContainer _fluidContainer;

        // ソース別の保留液体量と詰まりカウンタ。キーが FluidContainer.Empty の場合はソースなしバケット
        // Per-source pending amount and blocked-tick counter. Key == FluidContainer.Empty means the sourceless bucket
        private struct SourceBucket { public double Amount; public int BlockedTicks; }
        private readonly Dictionary<FluidContainer, SourceBucket> _pendingBySource = new();
        private readonly int _blockedRetryTicks;

        private readonly BlockConnectorComponent<IFluidInventory> _connectorComponent;
        private readonly Subject<Unit> _onChangeBlockState = new();
        private BlockPositionInfo _blockPositionInfo;

        public FluidPipeComponent(BlockPositionInfo blockPositionInfo, BlockConnectorComponent<IFluidInventory> connectorComponent, float capacity, int blockedRetryTicks, Dictionary<string, string> componentStates)
        {
            _blockPositionInfo = blockPositionInfo;
            _connectorComponent = connectorComponent;
            _fluidContainer = new FluidContainer(capacity);
            _blockedRetryTicks = blockedRetryTicks;

            // セーブデータがある場合はロード
            if (componentStates != null && componentStates.TryGetValue(FluidPipeSaveComponent.SaveKeyStatic, out var savedState))
            {
                var jsonObject = JsonConvert.DeserializeObject<FluidPipeSaveJsonObject>(savedState);
                _fluidContainer.FluidId = jsonObject.FluidId;
                _fluidContainer.Amount = jsonObject.Amount;

                // ロード時はソース情報を失っているため全量を Empty バケットへ復元
                // On load, source identities cannot be recovered, so the entire amount is restored into the sourceless bucket
                if (_fluidContainer.Amount > 0)
                {
                    _pendingBySource[FluidContainer.Empty] = new SourceBucket { Amount = _fluidContainer.Amount, BlockedTicks = 0 };
                }
            }
        }

        // パイプ内の流体ID/量/容量をBlockStateDetailとして1件返す
        // Pack the pipe's fluid id/amount/capacity into a single BlockStateDetail entry
        public BlockStateDetail[] GetBlockStateDetails()
        {
            var fluidStateDetail = GetFluidPipeStateDetail();
            var blockStateDetail = new BlockStateDetail(
                FluidPipeStateDetail.BlockStateDetailKey,
                MessagePackSerializer.Serialize(fluidStateDetail)
            );

            return new[] { blockStateDetail };
        }

        public FluidStack AddLiquid(FluidStack fluidStack, FluidContainer source)
        {
            // ソース管理はコンポーネント側のバケットで行うため、FluidContainer.AddLiquid のHashSetチェックは Empty を渡してバイパス
            // Source attribution is managed at the component level, so bypass FluidContainer.AddLiquid's HashSet check by passing Empty
            var beforeAmount = _fluidContainer.Amount;
            var remain = _fluidContainer.AddLiquid(fluidStack, FluidContainer.Empty);
            var accepted = _fluidContainer.Amount - beforeAmount;

            // 一切受け入れられなかったケース（容量満杯／FluidId不一致／投入量0等）は即返す
            // Bail out when nothing was accepted (full container, fluid id mismatch, zero stack, etc.)
            if (accepted <= 0) return remain;

            // 受け入れた分をソース別バケットに加算（source==nullはソース不明として Empty バケットへ）
            // Credit the accepted amount to the matching source bucket (null source falls into the Empty bucket)
            var key = source ?? FluidContainer.Empty;
            var bucket = _pendingBySource.GetValueOrDefault(key);
            bucket.Amount += accepted;
            
            // 新規流入があれば詰まりカウンタはリセット
            // New inflow resets the blocked-tick counter
            bucket.BlockedTicks = 0;
            
            _pendingBySource[key] = bucket;
            
            _onChangeBlockState.OnNext(Unit.Default);
            return remain;
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }

        public void Update()
        {
            // 切断されたソースのバケットを Empty バケットへ合流させる（孤児回収）
            // Merge buckets whose source has been disconnected into the sourceless bucket
            MergeOrphanSources();

            var totalTransferred = 0.0;

            // 各ソース別バケットを独立に配分
            // Distribute each per-source bucket independently
            foreach (var key in _pendingBySource.Keys.ToList())
            {
                if (!_pendingBySource.TryGetValue(key, out var bucket)) continue;

                // 残量0の空バケットは掃除して次へ
                // Drop empty buckets eagerly so they don't accumulate
                if (bucket.Amount <= 0)
                {
                    _pendingBySource.Remove(key);
                    continue;
                }

                // 流せる隣接が無いtickは詰まりカウンタを進めて降格判定だけ行う
                // No eligible neighbors this tick: only advance the blocked counter and check for demotion
                var eligible = GetEligibleTargets(key);
                if (eligible.Count == 0)
                {
                    bucket.BlockedTicks++;
                    _pendingBySource[key] = bucket;
                    TryDemote(key, bucket);
                    continue;
                }

                var sent = DistributeEqually(bucket.Amount, eligible);
                bucket.Amount -= sent;
                totalTransferred += sent;

                if (bucket.Amount <= 0)
                {
                    _pendingBySource.Remove(key);
                }
                else if (sent > 0)
                {
                    // 部分的にでも進捗があれば詰まりカウンタはリセット
                    // Any forward progress resets the blocked-tick counter
                    bucket.BlockedTicks = 0;
                    _pendingBySource[key] = bucket;
                }
                else
                {
                    bucket.BlockedTicks++;
                    _pendingBySource[key] = bucket;
                    TryDemote(key, bucket);
                }
            }

            // FluidContainer 側の同tick重複受信防止記録は引き続きクリア（他コンポーネント互換）
            // Continue clearing the FluidContainer's same-tick source record for compatibility with other consumers
            _fluidContainer.ClearPreviousSources();

            // 全バケットの合計を FluidContainer.Amount に反映、空になった場合は流体IDもクリア
            // Reflect the bucket total into FluidContainer.Amount and reset the fluid id when fully empty
            _fluidContainer.Amount = SumBuckets();
            if (_fluidContainer.Amount <= 0) _fluidContainer.FluidId = FluidMaster.EmptyFluidId;

            // 実際に流れた分があるtickだけ状態変更を通知
            // Notify state change only on ticks where actual transfer happened
            if (totalTransferred > 0)
            {
                _onChangeBlockState.OnNext(Unit.Default);
            }

            #region Internal

            void MergeOrphanSources()
            {
                // ソース別バケットを走査し、対応する隣接が現在の接続先一覧から消えたものを孤児として収集
                // Scan per-source buckets and collect ones whose source neighbor is no longer in the connection set
                List<FluidContainer> orphans = null;
                foreach (var k in _pendingBySource.Keys)
                {
                    if (k == FluidContainer.Empty) continue;
                    if (k.IsEmpty) continue;

                    var stillConnected = false;
                    foreach (var connected in _connectorComponent.ConnectedTargets.Keys)
                    {
                        if (TryGetContainer(connected, out var container) && container == k)
                        {
                            stillConnected = true;
                            break;
                        }
                    }
                    if (!stillConnected)
                    {
                        orphans ??= new List<FluidContainer>();
                        orphans.Add(k);
                    }
                }
                if (orphans == null) return;

                // 孤児バケットの残量を Empty バケットへ合算し、ソース除外なしで再配分対象にする
                // Fold orphan amounts into the Empty bucket so they become redistributable without source exclusion
                foreach (var orphan in orphans)
                {
                    var bucket = _pendingBySource[orphan];
                    _pendingBySource.Remove(orphan);
                    if (bucket.Amount <= 0) continue;
                    
                    var emptyBucket = _pendingBySource.GetValueOrDefault(FluidContainer.Empty);
                    emptyBucket.Amount += bucket.Amount;
                    
                    _pendingBySource[FluidContainer.Empty] = emptyBucket;
                }
            }

            // バケットのソースを除外した上で、流量>0の隣接だけを配分対象として返す
            // Return neighbors with positive flow rate, excluding the bucket's own source
            List<(IFluidInventory inventory, double maxFlowRate)> GetEligibleTargets(FluidContainer sourceKey)
            {
                var result = new List<(IFluidInventory inventory, double maxFlowRate)>();
                foreach (var kvp in _connectorComponent.ConnectedTargets)
                {
                    var targetInventory = kvp.Key;
                    var connectedInfo = kvp.Value;

                    var maxFlowRate = GetMaxFlowRateFromConnection(connectedInfo);
                    if (maxFlowRate <= 0) continue;

                    // ソースに該当する隣接は除外（Empty バケットは除外なし＝全隣接対象）
                    // Exclude the neighbor that matches the source (Empty bucket excludes nothing — all neighbors are eligible)
                    if (sourceKey != FluidContainer.Empty && TryGetContainer(targetInventory, out var targetContainer) && targetContainer == sourceKey)
                    {
                        continue;
                    }

                    result.Add((targetInventory, maxFlowRate));
                }
                return result;
            }

            double DistributeEqually(double bucketAmount, List<(IFluidInventory inventory, double maxFlowRate)> targets)
            {
                // 候補数で割って各候補へ等分。各候補は maxFlowRate で打ち切り
                // Divide the bucket equally across candidates; each is capped by its maxFlowRate
                var sharePerTarget = bucketAmount / targets.Count;
                var transferred = 0.0;

                foreach (var (inventory, maxFlowRate) in targets)
                {
                    var sendAmount = Math.Min(sharePerTarget, maxFlowRate);
                    if (sendAmount <= 0) continue;

                    var stack = new FluidStack(sendAmount, _fluidContainer.FluidId);
                    var remain = inventory.AddLiquid(stack, _fluidContainer);
                    transferred += sendAmount - remain.Amount;
                }
                return transferred;
            }

            void TryDemote(FluidContainer key, SourceBucket bucket)
            {
                // Empty バケット自身は降格対象外、また閾値未満なら何もしない
                // The Empty bucket cannot be demoted further, and we wait until the blocked threshold is hit
                if (key == FluidContainer.Empty) return;
                if (bucket.BlockedTicks < _blockedRetryTicks) return;

                // Nティック詰まったソース別バケットを Empty バケットへ降格
                // Demote a bucket that has been blocked for N ticks into the sourceless bucket
                _pendingBySource.Remove(key);
                if (bucket.Amount <= 0) return;
                if (!_pendingBySource.TryGetValue(FluidContainer.Empty, out var emptyBucket)) emptyBucket = default;
                emptyBucket.Amount += bucket.Amount;
                _pendingBySource[FluidContainer.Empty] = emptyBucket;
            }

            double SumBuckets()
            {
                var sum = 0.0;
                foreach (var b in _pendingBySource.Values) sum += b.Amount;
                return sum;
            }
            
            // 2つのIFluidInventory間の最大流体搬送速度を取得する。搬送速度は、2つのIFluidInventoryの流体搬送能力の最小値に、1ゲームアップデートの時間(秒)を乗じた値
            // Get the maximum fluid transfer rate between two IFluidInventories. The transfer rate is the minimum of the two IFluidInventories' flow capacities multiplied by the time per game update (in seconds).
            double GetMaxFlowRateFromConnection(ConnectedInfo connectedInfo)
            {
                var selfOption = connectedInfo.SelfConnector?.ConnectOption as FluidConnectOption;
                var targetOption = connectedInfo.TargetConnector?.ConnectOption as FluidConnectOption;
                
                if (selfOption == null || targetOption == null) throw new ArgumentException();
                
                return Math.Min(selfOption.FlowCapacity, targetOption.FlowCapacity) * GameUpdater.SecondsPerTick;
            }

            #endregion
        }

        public FluidPipeStateDetail GetFluidPipeStateDetail()
        {
            var fluidId = _fluidContainer.FluidId;
            var amount = _fluidContainer.Amount;
            var capacity = _fluidContainer.Capacity;
            return new FluidPipeStateDetail(fluidId, (float)amount, (float)capacity);
        }

        // パイプは単一流体しか保持しないため、残量があれば1要素、無ければ空のリストを返す
        // A pipe only ever holds a single fluid; return one stack when present, otherwise an empty list
        public List<FluidStack> GetFluidInventory()
        {
            var fluidStacks = new List<FluidStack>();
            if (_fluidContainer.Amount > 0)
            {
                fluidStacks.Add(new FluidStack(_fluidContainer.Amount, _fluidContainer.FluidId));
            }
            return fluidStacks;
        }

        // ソース照合のため、隣接 IFluidInventory が単一の FluidContainer を持つ場合のみ取得
        // Resolve a neighbor's primary FluidContainer for source identity checks (skip if not pipe-like)
        private static bool TryGetContainer(IFluidInventory inventory, out FluidContainer container)
        {
            if (inventory is FluidPipeComponent pipe)
            {
                container = pipe._fluidContainer;
                return true;
            }
            container = null;
            return false;
        }
    }
}
