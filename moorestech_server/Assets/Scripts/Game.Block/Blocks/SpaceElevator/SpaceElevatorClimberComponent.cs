using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Util;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Game.Context;
using MessagePack;
using Mooresmaster.Model.BlocksModule;
using UniRx;
using UnityEngine;

namespace Game.Block.Blocks.SpaceElevator
{
    // 拠点の昇降機。衛星を1つずつ電力で昇らせ、頂上で軌道到着サービスへ渡して成果を出口へ入れる。成果が入りきらなければ昇り始めず、頂上でも待つ
    // The base's climber: lifts satellites one at a time with power and hands each over at the top; it neither starts nor hands over while the result would not fit
    public class SpaceElevatorClimberComponent : ISpaceElevatorBase, IUpdatableBlockComponent, IBlockStateObservable, IBlockSaveState, IGetRefundItemsInfo
    {
        public bool IsConnected { get; private set; }
        public IObservable<Unit> OnChangeBlockState => _onChangeBlockState;
        public string SaveKey => "spaceElevatorClimber";
        // 昇っている間だけ電力を要求する。頂上で出口の空きを待つ間は使わない
        // Request power only while ascending; waiting at the top for outlet space uses none
        public float RequestPower => IsAscending ? _param.RequiredPower : 0f;
        private bool IsCarrying => _climbingSatellite.Id != ItemMaster.EmptyItemId;
        private bool IsAscending => IsCarrying && 0 < _remainingClimbTicks;
        private readonly Subject<Unit> _onChangeBlockState = new();
        private readonly SpaceElevatorBaseBlockParam _param;
        private readonly SpaceElevatorBaseInventoryComponent _inventory;
        private readonly uint _climbTotalTicks;
        private readonly IDisposable _inventorySubscription;
        private readonly IDisposable _stackLevelSubscription;
        private bool _inventoryChanged = true;
        private IItemStack _climbingSatellite;
        private uint _remainingClimbTicks;
        private bool _outputBlocked;
        private bool _inletNotCarried;
        private int _cablePoolAmount;
        private bool _hasEnoughCable;
        private float _suppliedPower;
        private float _latchedPower;
        public SpaceElevatorClimberComponent(SpaceElevatorBaseBlockParam param, SpaceElevatorBaseInventoryComponent inventory)
        {
            _param = param;
            _inventory = inventory;
            _climbTotalTicks = GameUpdater.SecondsToTicks(param.ClimbSeconds);
            _climbingSatellite = ServerContext.ItemStackFactory.CreatEmpty();
            // 在庫変更で待機条件を再評価する
            // Reevaluate waiting conditions when inventory changes
            _inventorySubscription = inventory.OnInventoryChanged.Subscribe(_ => _inventoryChanged = true);
            _stackLevelSubscription = ServerContext.GetService<IItemStackLevelLookup>().OnStackLevelUnlocked
                .Subscribe(_ => _inventoryChanged = true);
        }

        public SpaceElevatorClimberComponent(Dictionary<string, object> componentStates, SpaceElevatorBaseBlockParam param, SpaceElevatorBaseInventoryComponent inventory) : this(param, inventory)
        {
            var save = BlockComponentStateReader.Read<SpaceElevatorClimberSaveJsonObject>(componentStates, SaveKey);
            IsConnected = save.IsConnected;
            (_climbingSatellite, _remainingClimbTicks) = SpaceElevatorClimberSaveRestorer.Restore(save, inventory, _climbTotalTicks);
        }

        public void Update()
        {
            BlockException.CheckDestroy(this);
            // tick内の供給を確定してから進める
            // Latch this tick's supply before advancing
            _latchedPower = _suppliedPower;
            _suppliedPower = 0f;
            var wasOutputBlocked = _outputBlocked;
            var shouldEvaluate = IsAscending || _inventoryChanged;
            var inventoryChanged = _inventoryChanged;
            _inventoryChanged = false;
            var changed = shouldEvaluate && (IsCarrying ? AdvanceClimb() : TryStartClimb());
            // 在庫通知は初回tick前の入口変更も含むため、キャッシュ差分に依存せず配信する
            // Inventory events include inlet changes before the first tick, so publish without relying on a cached difference
            changed |= wasOutputBlocked != _outputBlocked || inventoryChanged;
            // 出口待ちに入ったtickに1回だけ理由をログへ出す（以降はUIの状態表示で伝える）
            // Log the reason once on the tick the outlet wait begins; the UI status carries it afterwards
            if (!wasOutputBlocked && _outputBlocked) Debug.Log("[SpaceElevatorBase] 衛星1機の成果が出口に入りきらないため、出口が空くまで待ちます");
            // 昇っている間は進みが毎tick動くので毎tick通知する（同一ペイロードはイベント側が送らない）
            // While ascending the progress moves every tick, so notify every tick (the event packet drops identical payloads)
            if (changed || IsAscending) _onChangeBlockState.OnNext(Unit.Default);
            #region Internal
            bool AdvanceClimb()
            {
                // 電力不足なら供給率に比例して遅くなる
                // A power shortage slows the climb in proportion to the supply rate
                if (0 < _remainingClimbTicks)
                {
                    var subTicks = MachineCurrentPowerToSubSecond.GetSubTicks(_latchedPower, _param.RequiredPower);
                    _remainingClimbTicks = _remainingClimbTicks <= subTicks ? 0 : _remainingClimbTicks - subTicks;
                }
                if (0 < _remainingClimbTicks) return false;
                // 頂上では成果が出口へ入りきるときだけ渡す。入らなければ頂上で待つ
                // At the top, hand over only when the result fits the outlets; otherwise wait at the top
                _inventoryChanged = true;
                _outputBlocked = !_inventory.TryCompleteArrival(_climbingSatellite.Id);
                // 容量不足は実通知まで待つ。例外時の再試行予約は到着呼出し前に残す
                // Wait for a real event when full; reserve retries before arrival so exceptions retain them
                _inventoryChanged = !_outputBlocked;
                // silent-ok: 出口待ちは初回ログと状態詳細で通知 / プレイヤーは拠点UIで気づく
                if (_outputBlocked) return false;
                _climbingSatellite = ServerContext.ItemStackFactory.CreatEmpty();
                return true;
            }
            bool TryStartClimb()
            {
                // silent-ok: 未接続・衛星なしは通常の待機 / プレイヤーが拠点UIの状態表示（ReadyToConnect・InsufficientCable・WaitingSatellite）で気づく
                _outputBlocked = false;
                var wasInletNotCarried = _inletNotCarried;
                IItemStack satellite = null;
                ItemId satelliteId;
                var result = IsConnected
                    ? _inventory.TryTakeSatelliteForClimb(out satellite, out satelliteId)
                    : _inventory.PeekSatellite(out satelliteId) == SatelliteInletState.NotCarried ? SatelliteTakeResult.NotCarried : SatelliteTakeResult.Empty;
                _inletNotCarried = result == SatelliteTakeResult.NotCarried;
                // 運べないアイテムが入口を塞いだら、その tick に1回だけ開発者ログへ出す
                // Log once on the tick a non-carried item starts blocking the inlet
                if (_inletNotCarried && !wasInletNotCarried) Debug.LogWarning($"[SpaceElevatorBase] 入口の {satelliteId} は今のマスタで運ぶ積荷ではないため昇れません。入口から取り出してください");
                _outputBlocked = result == SatelliteTakeResult.OutputBlocked;
                // silent-ok: 在庫が返す待機理由は状態詳細へ反映 / プレイヤーは拠点UIで気づく
                if (result != SatelliteTakeResult.Taken) return false;
                _climbingSatellite = satellite;
                _remainingClimbTicks = _climbTotalTicks;
                return true;
            }
            #endregion
        }

        internal void SupplyPower(float power)
        {
            BlockException.CheckDestroy(this);
            // 複数の電力セグメントから供給され得るため加算する
            // Accumulate because several electric segments may supply this base
            _suppliedPower += power;
        }

        public void MarkConnected()
        {
            BlockException.CheckDestroy(this);
            IsConnected = true;
            _inventoryChanged = true;
            _onChangeBlockState.OnNext(Unit.Default);
        }
        // 接続できるかはプールが判定して渡す。拠点は判定せず状態に載せるだけ
        // The pool judges connectability and pushes it; the base only carries it in its state
        public void SetCablePoolAvailability(int poolAmount, bool hasEnoughCable)
        {
            BlockException.CheckDestroy(this);
            _cablePoolAmount = poolAmount;
            _hasEnoughCable = hasEnoughCable;
            _onChangeBlockState.OnNext(Unit.Default);
        }

        public BlockStateDetail[] GetBlockStateDetails()
        {
            BlockException.CheckDestroy(this);
            var progress = IsCarrying ? 1f - (float)_remainingClimbTicks / _climbTotalTicks : 0f;
            var status = ResolveStatus();
            var detail = new SpaceElevatorBaseStateDetail(IsConnected, _cablePoolAmount, progress, status);
            return new[] { new BlockStateDetail(SpaceElevatorBaseStateDetail.BlockStateDetailKey, MessagePackSerializer.Serialize(detail)) };
            #region Internal
            SpaceElevatorBaseStatus ResolveStatus()
            {
                if (!IsConnected) return _hasEnoughCable ? SpaceElevatorBaseStatus.ReadyToConnect : SpaceElevatorBaseStatus.InsufficientCable;
                if (_outputBlocked) return SpaceElevatorBaseStatus.OutputBlocked;
                if (!IsCarrying) return _inventory.PeekSatellite(out _) == SatelliteInletState.NotCarried ? SpaceElevatorBaseStatus.InletBlocked : SpaceElevatorBaseStatus.WaitingSatellite;
                return _latchedPower < _param.RequiredPower ? SpaceElevatorBaseStatus.LowPower : SpaceElevatorBaseStatus.Climbing;
            }
            #endregion
        }
        // 撤去時は昇降中の衛星をプレイヤーへ返す
        // On removal, return the satellite being carried to the player
        public IReadOnlyList<IItemStack> GetRefundItems()
        {
            return IsCarrying ? new List<IItemStack> { _climbingSatellite } : new List<IItemStack>();
        }

        public object GetSaveState()
        {
            BlockException.CheckDestroy(this);
            return SpaceElevatorClimberSaveRestorer.ToSave(IsConnected, _climbingSatellite, _remainingClimbTicks);
        }

        public bool IsDestroy { get; private set; }
        public void Destroy() { IsDestroy = true; _inventorySubscription.Dispose(); _stackLevelSubscription.Dispose(); _onChangeBlockState.Dispose(); }
    }
}
