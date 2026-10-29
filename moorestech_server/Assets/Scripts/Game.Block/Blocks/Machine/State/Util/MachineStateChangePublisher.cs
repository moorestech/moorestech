using System;
using UniRx;

namespace Game.Block.Blocks.Machine.State.Util
{
    // 通常機械と清浄室機械の状態イベントを出す口。前回通知した状態と出力空き不足の理由を覚え、変化端で通知する
    // The state-event outlet shared by normal and clean-room machines; remembers the last notified state and output-space reason and notifies on edges
    internal sealed class MachineStateChangePublisher
    {
        public IObservable<Unit> OnChangeBlockState => _changeState;
        private readonly Subject<Unit> _changeState = new();

        // 状態詳細へ「直前の状態」として載る値。通知の後に更新するので、購読側は通知時点で遷移前の状態を読める
        // Published as the previous state in the detail; updated after notifying so subscribers read the pre-transition state at notice time
        public ProcessState LastState { get; private set; } = ProcessState.Idle;
        private bool _lastOutputSpaceShort;

        // tick外で状態を書き換えた操作の直後に、変化の有無を問わず通知する
        // Notify unconditionally right after an off-tick operation rewrote the state
        public void Publish()
        {
            _changeState.OnNext(Unit.Default);
        }

        // tick内の遷移の後、状態か出力空き不足の理由が変わった時・処理中なら通知して記録を進める
        // After the tick's transition, notify on a state or output-space reason change or while processing, then advance the record
        public void PublishIfChanged(ProcessState currentState, IdleMachineProcessState idleState)
        {
            var outputSpaceShort = MachineStateDetailFactory.IsOutputSpaceShort(currentState, idleState);
            if (LastState == currentState && _lastOutputSpaceShort == outputSpaceShort && currentState != ProcessState.Processing) return;
            _changeState.OnNext(Unit.Default);
            LastState = currentState;
            _lastOutputSpaceShort = outputSpaceShort;
        }
    }
}
