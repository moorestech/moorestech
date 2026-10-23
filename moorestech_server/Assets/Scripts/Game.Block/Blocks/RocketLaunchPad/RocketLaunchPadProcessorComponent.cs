using System;
using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Item.Interface;
using Game.Block.Blocks.Connector;
using Game.Block.Blocks.RocketLaunchPad.Save;
using Game.Block.Blocks.RocketLaunchPad.Stage;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Game.Orbit;
using Mooresmaster.Model.BlocksModule;
using UniRx;
using UnityEngine;
using static Game.Block.Interface.BlockException;

namespace Game.Block.Blocks.RocketLaunchPad
{
    /// <summary>
    ///     発射台の部品としての口。tick・合図・初打ち上げ通知を段階の進行へ渡し、状態の通知・セーブ・撤去返却を受け持つ
    ///     The pad's component surface: routes ticks, commands and the first-launch notice to the sequence, and owns state publishing, saving and refunds
    /// </summary>
    public class RocketLaunchPadProcessorComponent : IRocketLaunchPadComponent, IUpdatableBlockComponent, IBlockStateObservable, IBlockSaveState, IGetRefundItemsInfo
    {
        public RocketLaunchPadPhase Phase => _sequence.Stage.Phase;
        public RocketLaunchPadWaitReason WaitReason => _sequence.WaitReason;
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
        private readonly IDisposable _firstLaunchSubscription;
        private float _suppliedPower;

        public RocketLaunchPadProcessorComponent(RocketLaunchPadBlockParam param, RocketLaunchPadSlotLayout layout, OpenableInventoryItemDataStoreService slots, IBlockInventoryInserter beltOutput, IOrbitalArrivalService arrivalService, IRocketLaunchHistory launchHistory)
        {
            _layout = layout;
            _slots = slots;
            _cargoLoader = new RocketLaunchPadCargoLoader(layout, slots);
            _outputSlots = new RocketLaunchPadOutputSlots(layout, slots, beltOutput);
            _sequence = new RocketLaunchPadLaunchSequence(param, layout, _cargoLoader, _outputSlots, arrivalService, launchHistory);

            // ワールドの初打ち上げで発射の合図待ちを解く。建造の合図待ちと打ち上げそのものは各台の次の Update が行う
            // On the world's first launch release the launch command wait; each pad's next update handles the build wait and the launch
            _firstLaunchSubscription = launchHistory.OnFirstLaunch.Subscribe(_ => OnWorldFirstLaunch());
        }

        public RocketLaunchPadProcessorComponent(Dictionary<string, object> componentStates, RocketLaunchPadBlockParam param, RocketLaunchPadSlotLayout layout, OpenableInventoryItemDataStoreService slots, IBlockInventoryInserter beltOutput, IOrbitalArrivalService arrivalService, IRocketLaunchHistory launchHistory)
            : this(param, layout, slots, beltOutput, arrivalService, launchHistory)
        {
            // スロット・次に探す積荷・段階を戻す。今の積荷表で打ち上げられない積荷はコンバーターが警告して捨てる
            // Restore slots, the next payload index and the stage; the converter warns about and drops cargo the current table cannot launch
            var saveData = BlockComponentStateReader.Read<RocketLaunchPadSaveJsonObject>(componentStates, SaveKey);
            RocketLaunchPadSaveConverter.RestoreSlots(saveData, _layout, _slots);
            _cargoLoader.SetNextPayloadIndex(saveData.NextPayloadIndex);
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
            _firstLaunchSubscription.Dispose();
        }

        private void OnWorldFirstLaunch()
        {
            var previousPhase = Phase;
            _sequence.ReleaseLaunchCommandWait();
            if (previousPhase != Phase) _changeState.OnNext(Unit.Default);
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
