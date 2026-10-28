using System;
using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Game.Context;
using Game.Block.Interface;
using Core.Item.Interface;
using Game.Block.Blocks.Connector;
using Game.Block.Blocks.RocketLaunchPad.Save;
using Game.Block.Blocks.RocketLaunchPad.Stage;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Game.Orbit;
using Game.SpaceElevator;
using Mooresmaster.Model.BlocksModule;
using UniRx;
using UnityEngine;
using static Game.Block.Interface.BlockException;

namespace Game.Block.Blocks.RocketLaunchPad
{
    /// <summary>
    ///     - 進行へtick・合図・初発射を委譲
    ///     - 状態通知・セーブを担当
    ///     - 撤去時に積荷を返却
    ///     - Route ticks, commands and first-launch notifications to the sequence
    ///     - Publish and save state
    ///     - Refund cargo on removal
    /// </summary>
    public class RocketLaunchPadProcessorComponent : IRocketLaunchPadComponent, IUpdatableBlockComponent, IBlockStateObservable, IBlockSaveState, IGetRefundItemsInfo
    {
        private RocketLaunchPadPhase Phase => _sequence.Stage.Phase;
        private RocketLaunchPadWaitReason WaitReason => _sequence.WaitReason;
        public float RequestPower => _sequence.RequestPower;
        public IObservable<Unit> OnChangeBlockState => _changeState;
        public string SaveKey { get; } = typeof(RocketLaunchPadProcessorComponent).FullName;
        public bool IsDestroy { get; private set; }

        private readonly Subject<Unit> _changeState = new();
        private readonly RocketLaunchPadSlotLayout _layout;
        private readonly OpenableInventoryItemDataStoreService _slots;
        private readonly RocketLaunchPadCargoLoader _cargoLoader;
        private readonly RocketLaunchPadOutputSlots _outputSlots;
        private readonly RocketLaunchPadLaunchSequence _sequence;
        private readonly CompositeDisposable _subscriptions;
        private float _suppliedPower;

        public RocketLaunchPadProcessorComponent(RocketLaunchPadBlockParam param, RocketLaunchPadSlotLayout layout, OpenableInventoryItemDataStoreService slots, IBlockInventoryInserter beltOutput, IOrbitalArrivalService arrivalService, RocketLaunchHistory launchHistory, SpaceElevatorCablePool cablePool, IObservable<Unit> inventoryChanges, CompositeDisposable subscriptions)
        {
            _subscriptions = subscriptions;
            _layout = layout;
            _slots = slots;
            _outputSlots = new RocketLaunchPadOutputSlots(layout, slots, beltOutput, cablePool, arrivalService);
            _cargoLoader = new RocketLaunchPadCargoLoader(layout, slots, _outputSlots);
            _sequence = new RocketLaunchPadLaunchSequence(param, _cargoLoader, _outputSlots, launchHistory);

            // ワールドの初打ち上げで発射の合図待ちを解く。建造の合図待ちと打ち上げそのものは各台の次の Update が行う
            // On the world's first launch release the launch command wait; each pad's next update handles the build wait and the launch
            launchHistory.OnFirstLaunch.Subscribe(_ => OnWorldFirstLaunch()).AddTo(_subscriptions);
            // 在庫と上限の変化だけで待機条件を無効化する
            // Invalidate waiting conditions only when inventory or stack limits change
            inventoryChanges.Subscribe(_ => _sequence.InvalidateWait()).AddTo(_subscriptions);
            cablePool.OnAmountChanged.Subscribe(_ => _sequence.InvalidateWait()).AddTo(_subscriptions);
            ServerContext.GetService<IItemStackLevelLookup>().OnStackLevelUnlocked.Subscribe(_ => _sequence.InvalidateWait()).AddTo(_subscriptions);

            #region Internal

            void OnWorldFirstLaunch()
            {
                var previousPhase = Phase;
                _sequence.ReleaseLaunchCommandWait();
                if (previousPhase != Phase) _changeState.OnNext(Unit.Default);
            }

            #endregion
        }

        public RocketLaunchPadProcessorComponent(Dictionary<string, object> componentStates, RocketLaunchPadBlockParam param, RocketLaunchPadSlotLayout layout, OpenableInventoryItemDataStoreService slots, IBlockInventoryInserter beltOutput, IOrbitalArrivalService arrivalService, RocketLaunchHistory launchHistory, SpaceElevatorCablePool cablePool, IObservable<Unit> inventoryChanges, CompositeDisposable subscriptions)
            : this(param, layout, slots, beltOutput, arrivalService, launchHistory, cablePool, inventoryChanges, subscriptions)
        {
            // 実在品を保全してから段階を復元
            // Restore the stage only after verifying that all existing items can be retained
            var saveData = BlockComponentStateReader.Read<RocketLaunchPadSaveJsonObject>(componentStates, SaveKey);
            RocketLaunchPadSaveConverter.RestoreSlots(saveData, _layout, _slots);
            if (!saveData.NextPayloadIndex.HasValue) Debug.LogWarning("発射台のセーブに巡回位置が無いため先頭から探します");
            _cargoLoader.SetNextPayloadIndex(saveData.NextPayloadIndex.GetValueOrDefault());
            _sequence.SetStage(RocketLaunchPadSaveConverter.RestoreStage(saveData, _layout));

            // 初打ち上げ済みのワールドで発射の合図待ちのまま保存された台は、通知を受けた台と同じく打ち上げ待ちへ移す（履歴はブロックより先にロードされる）
            // Pads saved awaiting the launch command in an already-launched world move to ready-to-launch like notified pads (history loads before blocks)
            if (launchHistory.HasLaunchedOnce) _sequence.ReleaseLaunchCommandWait();
        }

        public void SupplyPower(float power)
        {
            // 複数の電力網から供給され得るため加算し、次のUpdateで使い切る
            // Accumulate since several networks may supply this pad, then consume it on the next update
            _suppliedPower += power;
        }

        public void Update()
        {
            CheckDestroy(this);
            var previousPhase = Phase;
            var previousReason = WaitReason;
            var tickPower = _suppliedPower;
            _suppliedPower = 0f;

            // 出力口の中身を接続先へ流してから段階を進める（機械の出力と同じ順）
            // Push output contents onward before advancing the stage (same order as machine outputs)
            _outputSlots.PushToConnected();
            _sequence.Advance(tickPower);

            // 段階か待ち理由が変わったとき、進行中は毎tick状態を流す（機械の進捗と同じ）
            // Publish on a phase or reason change, and every tick while progressing (as machine progress does)
            var isProgressing = _sequence.Stage is IProgressingStage;
            if (previousPhase != Phase || previousReason != WaitReason || isProgressing) _changeState.OnNext(Unit.Default);
        }

        public RocketLaunchPadCommandResult RequestBuild()
        {
            CheckDestroy(this);
            return PublishCommandResult(_sequence.RequestBuild());
        }

        public RocketLaunchPadCommandResult RequestLaunch()
        {
            CheckDestroy(this);
            return PublishCommandResult(_sequence.RequestLaunch());
        }

        public BlockStateDetail[] GetBlockStateDetails()
        {
            CheckDestroy(this);
            return new[] { RocketLaunchPadStateDetail.CreateState(Phase, WaitReason, _sequence.CalculateProgress()) };
        }

        public IReadOnlyList<IItemStack> GetRefundItems()
        {
            // 積んだまま打ち上げていないロケットの中身を返す。出力口の成果は開けるスロットとして撤去処理が返す
            // Refund the contents of a not-yet-launched rocket; output slot contents are refunded by removal as openable slots
            return _sequence.Stage is LoadedRocketStage loaded ? loaded.Cargo.AllItems().ToList() : new List<IItemStack>();
        }

        public object GetSaveState()
        {
            CheckDestroy(this);
            return RocketLaunchPadSaveConverter.Capture(_sequence.Stage, _cargoLoader.NextPayloadIndex, _layout, _slots);
        }

        public void Destroy()
        {
            IsDestroy = true;
            _subscriptions.Dispose();
        }

        private RocketLaunchPadCommandResult PublishCommandResult(RocketLaunchPadCommandResult result)
        {
            // 受け付けた合図は段階を変えるので状態を流し、拒否は理由を開発者向けに残す
            // Accepted commands change the stage so publish it; rejections leave their reason for developers
            if (result == RocketLaunchPadCommandResult.Accepted) _changeState.OnNext(Unit.Default);
            else Debug.LogWarning($"ロケット発射台への操作を拒否しました 理由:{result} 段階:{Phase} 待ち理由:{WaitReason}");
            return result;
        }
    }
}
